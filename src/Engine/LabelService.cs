using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using MedGas2024.Core;
namespace MedGas2024.Engine
{
    public sealed class LabelTypePlan
    {
        public ObjectId Source,Style,Target;public string Fingerprint,Name,Text,Description,StyleName,Input;
        public bool FreshR,Create,NewFont,Centered,InPlace;public double S,Dash,Before,After,X,Y;
    }
    public sealed class LabelChange
    {
        public ObjectId Id,Type;public string Handle,Label,Layer,Before,After;public double Scale;
    }
    public sealed class LabelPlan
    {
        public string CatalogState,OptionsState,Intent;public readonly List<LabelChange> Items=new List<LabelChange>();public readonly List<LabelTypePlan> Types=new List<LabelTypePlan>();public readonly List<string> Skipped=new List<string>();public int Matched,WidthUnsupported,Linked,SharedReferences;
        public string Summary{get{return Intent+"\r\nKhớp nhãn: "+Matched+" đường; xử lý: "+Items.Count+"; bỏ qua: "+Skipped.Count+".\r\nÁp trực tiếp trên mọi đường khớp nhãn, kể cả khác Layer và có ghi đè riêng.\r\nĐịnh nghĩa sửa giữ tên: "+Types.Count(t=>t.InPlace)+"; tham chiếu nét dùng chung (ít nhất): "+SharedReferences+".\r\nLinetype R=0 mới: "+Types.Count(t=>t.Create&&t.FreshR)+"; đường gỡ nhãn MText liên kết: "+Linked+". Width không hỗ trợ: "+WidthUnsupported+".\r\nGiữ hình học, Layer, lineweight, font/chữ và các thuộc tính chưa chọn. Generation Disabled trên polyline xử lý.\r\nGiữ tên Layer/Linetype khi chuyển R=0. Định nghĩa nét dùng chung được cập nhật; mọi tham chiếu cùng nét (kể cả ngoài bộ lọc/block) sẽ dùng định nghĩa mới. Chỉ màu/Scale/Width áp trên các đường trong phạm vi đã chọn.\r\nR=0 theo chiều tuyến; REVERSE có tác dụng, nhưng không tự làm mọi nhánh polyline cùng thuận hướng.\r\n"+String.Join("\r\n",Skipped.Take(10));}}
    }
    public static class LabelService
    {
        const string RKey="MRT_NATIVE_R0_V1",RMark="MrT.NativeR:1";
        sealed class Entry{public ObjectId Id,Type,Layer;public LabelMember Info;public string Blocked;}
        sealed class Scan{public LabelCatalog View=new LabelCatalog();public readonly List<Entry> Entries=new List<Entry>();}
        static bool Curve(Entity e){return e is Line||e is Polyline||e is Polyline2d||e is Polyline3d||e is Arc||e is Spline;}
        static string ColorText(Color c){return c.ColorMethod==ColorMethod.ByAci?"ACI "+c.ColorIndex:c.ColorMethod==ColorMethod.ByLayer?"ByLayer":c.ColorMethod==ColorMethod.ByBlock?"ByBlock":"RGB "+c.Red+","+c.Green+","+c.Blue;}
        static string TextAt(LinetypeTableRecord t,int i){if(t.ShapeStyleAt(i).IsNull||t.ShapeNumberAt(i)!=0)return "";try{return t.TextAt(i)??"";}catch(Autodesk.AutoCAD.Runtime.Exception ex){if(ex.ErrorStatus!=Autodesk.AutoCAD.Runtime.ErrorStatus.NotApplicable)throw;return "";}}
        internal static DrawingLayerInfo Definition(LinetypeTableRecord t,Transaction tr)
        {
            var row=new DrawingLayerInfo{UseLabelIdentity=true,LayerName=t.Name,LinetypeName=t.Name,Text="",Description=t.Comments,Writable=true};var sig=new StringBuilder(t.Handle+"|"+t.Name+"|"+t.Comments+"|"+t.NumDashes+"|"+Profile.N(t.PatternLength)+"|"+t.IsScaledToFit);int count=0;
            for(int i=0;i<t.NumDashes;i++)
            {
                string text=TextAt(t,i);sig.Append('|').Append(Profile.N(t.DashLengthAt(i))).Append('|').Append(text);var id=t.ShapeStyleAt(i);if(id.IsNull)continue;sig.Append('|').Append(id.Handle).Append('|').Append(t.ShapeNumberAt(i)).Append('|').Append(t.ShapeOffsetAt(i)).Append('|').Append(t.ShapeRotationAt(i)).Append('|').Append(t.ShapeIsUcsOrientedAt(i)).Append('|').Append(t.ShapeScaleAt(i));
                if(text.Length==0)continue;count++;row.Text+=(row.Text.Length==0?"":" | ")+text;var st=(TextStyleTableRecord)tr.GetObject(id,OpenMode.ForRead);row.NativeTextHeight=t.ShapeScaleAt(i)*(st.TextSize>0?st.TextSize:1);row.Font=st.Name+" • "+st.FileName+" • "+st.Font.TypeFace+" • Bold="+st.Font.Bold+" • H="+st.TextSize+" • W="+st.XScale;sig.Append('|').Append(row.Font).Append(st.Font.Italic).Append(st.ObliquingAngle).Append(st.IsVertical);
            }
            row.Editable=!t.IsScaledToFit&&t.NumDashes==3&&t.ShapeStyleAt(0).IsNull&&t.ShapeStyleAt(2).IsNull&&count==1&&TextAt(t,1).Length>0&&t.DashLengthAt(0)>0&&t.DashLengthAt(1)<0&&t.DashLengthAt(2)<0;
            if(count!=1)row.NativeTextHeight=0;if(row.Editable){row.Height=t.ShapeScaleAt(1);row.Dash=t.DashLengthAt(0);row.Before=-t.DashLengthAt(1);row.After=-t.DashLengthAt(2);row.X=t.ShapeOffsetAt(1).X;row.Y=t.ShapeOffsetAt(1).Y;}row.Fingerprint=Profile.Hash(sig.ToString());return row;
        }
        static Scan ReadScan(Database db)
        {
            var scan=new Scan();var defs=new Dictionary<ObjectId,DrawingLayerInfo>();using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);foreach(ObjectId b in blocks)
                {
                    var space=(BlockTableRecord)tr.GetObject(b,OpenMode.ForRead);if(!space.IsLayout||space.IsFromExternalReference||space.IsFromOverlayReference)continue;
                    foreach(ObjectId id in space)
                    {
                        var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||!Curve(e))continue;var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);if(layer.IsDependent||DrawingSearch.Preview(layer.Name)){scan.View.Ignored++;continue;}
                        ObjectId type=DirectionService.NativeSource(e,db,tr);if(type==db.ByLayerLinetype)type=layer.LinetypeObjectId;if(type==db.ByBlockLinetype||type.IsNull||!type.IsValid||type.IsErased){scan.View.NoLabel++;continue;}
                        DrawingLayerInfo definition;if(!defs.TryGetValue(type,out definition)){definition=Definition((LinetypeTableRecord)tr.GetObject(type,OpenMode.ForRead),tr);defs.Add(type,definition);}
                        if(String.IsNullOrWhiteSpace(definition.Text)){scan.View.NoLabel++;continue;}bool linked=DirectionService.HasLinked(e,tr);string linkedState="",blocked=null;try{linkedState=DirectionService.LinkedState(e,db,tr);}catch(InvalidOperationException ex){blocked=ex.Message;}
                        var color=e.Color.ColorMethod==ColorMethod.ByLayer?layer.Color:e.Color;
                        var info=new LabelMember{Handle=id.Handle.ToString(),Layer=layer.Name,Type=definition.LinetypeName,Text=definition.Text,Color=ColorText(color),Scale=e.LinetypeScale,NativeHeight=definition.NativeTextHeight,Locked=layer.IsLocked||blocked!=null,Linked=linked};
                        info.State=Profile.Hash(e.OwnerId.Handle+"|"+e.LayerId.Handle+"|"+layer.IsLocked+"|"+layer.IsOff+"|"+layer.IsFrozen+"|"+ColorText(layer.Color)+"|"+e.LinetypeId.Handle+"|"+definition.Fingerprint+"|"+ColorText(e.Color)+"|"+e.LinetypeScale+"|"+ScaleService.WidthState(e,tr)+"|"+ScaleService.GenerationOn(e)+"|"+linkedState+"|"+blocked);
                        scan.View.Members.Add(info);scan.Entries.Add(new Entry{Id=id,Layer=e.LayerId,Type=type,Info=info,Blocked=blocked});
                    }
                }
            }scan.View.Definitions.AddRange(defs.Values.Where(x=>!String.IsNullOrWhiteSpace(x.Text)));scan.View.Fingerprint=Profile.Hash(String.Join("|",scan.View.Members.OrderBy(m=>m.Handle).Select(m=>m.Handle+":"+m.State)));return scan;
        }
        public static LabelCatalog Read(Database db){return ReadScan(db).View;}
        public static string KeyFromPipe(Database db,ObjectId id,bool system)
        {
            var scan=ReadScan(db);var entry=scan.Entries.SingleOrDefault(e=>e.Id==id);if(entry==null)throw new InvalidOperationException("Đường này chưa có nhãn linetype nhận diện được; chọn đường trực tiếp Model/Layout.");return LabelRules.Key(entry.Info.Text,system);
        }
        static TypedValue[] Marker(LinetypeTableRecord type,Transaction tr)
        {
            if(type.ExtensionDictionary.IsNull)return null;var d=(DBDictionary)tr.GetObject(type.ExtensionDictionary,OpenMode.ForRead);if(!d.Contains(RKey))return null;var x=(Xrecord)tr.GetObject(d.GetAt(RKey),OpenMode.ForRead);using(var data=x.Data)return data==null?null:data.AsArray();
        }
        static bool NativeR(LinetypeTableRecord type,Transaction tr,out bool centered)
        {
            centered=false;var values=Marker(type,tr);if(values==null||values.Length!=4||Convert.ToString(values[0].Value)!=RMark)return false;centered=Convert.ToInt32(values[3].Value)==1;return Convert.ToString(values[1].Value)==Definition(type,tr).Fingerprint;
        }
        public static bool CanRestoreLegacyR(LinetypeTableRecord type,Transaction tr){bool centered;return NativeR(type,tr,out centered);}
        static void Mark(LinetypeTableRecord type,Transaction tr,string input,bool centered)
        {
            if(type.ExtensionDictionary.IsNull)type.CreateExtensionDictionary();var d=(DBDictionary)tr.GetObject(type.ExtensionDictionary,OpenMode.ForWrite);Xrecord x;if(d.Contains(RKey))x=(Xrecord)tr.GetObject(d.GetAt(RKey),OpenMode.ForWrite);else{x=new Xrecord();d.SetAt(RKey,x);tr.AddNewlyCreatedDBObject(x,true);}using(var data=new ResultBuffer(new TypedValue(1000,RMark),new TypedValue(1000,Definition(type,tr).Fingerprint),new TypedValue(1000,input),new TypedValue(1070,(short)(centered?1:0))))x.Data=data;
        }
        static LabelTypePlan TypePlan(Database db,ObjectId source,Transaction tr,LabelEdit edit)
        {
            var type=(LinetypeTableRecord)tr.GetObject(source,OpenMode.ForRead);var row=Definition(type,tr);bool oldCentered;bool owned=NativeR(type,tr,out oldCentered);bool force=edit.ForceR||edit.Pattern.ForceR;
            var plan=new LabelTypePlan{Source=source,Target=source,Fingerprint=row.Fingerprint,Create=edit.ChangesType,FreshR=force||owned,Centered=oldCentered,Text=row.Text,Description=type.Comments};
            if(force&&!edit.Pattern.ChangesPattern&&owned&&(!edit.Recenter||oldCentered)){plan.Create=false;return plan;}
            if(!plan.Create)return plan;if(type.IsDependent)throw new InvalidOperationException("Nét phụ thuộc Xref không được sửa định nghĩa.");var p=edit.Pattern;bool pattern=p.ChangeLabel||p.ChangeText||p.ChangeHeight||p.ChangeDash||p.ChangeGaps||p.AutoFit||p.ChangeFont||force;
            if(!row.Editable&&pattern)throw new InvalidOperationException("R/chữ/font cần nét ba thành phần, đúng một cụm chữ; kiểu này chỉ đổi màu/scale.");
            if(row.Editable)
            {
                plan.Style=type.ShapeStyleAt(1);var st=(TextStyleTableRecord)tr.GetObject(plan.Style,OpenMode.ForRead);plan.S=p.ChangeHeight?p.Height:row.Height*(p.ChangeFont&&st.TextSize>0?st.TextSize:1);plan.Dash=p.ChangeDash?p.Dash:row.Dash;plan.Before=p.ChangeGaps?p.Before:row.Before;plan.After=p.ChangeGaps?p.After:row.After;plan.X=row.X;plan.Y=row.Y;plan.Text=p.NewText(new DrawingLayerInfo{UseLabelIdentity=true,Text=row.Text,LayerName=type.Name});
                if(st.IsVertical&&pattern)throw new InvalidOperationException("Style chữ xếp dọc chưa hỗ trợ đổi R/font.");new DefinitionEdit{Text=plan.Text,Description="",Height=plan.S,Dash=plan.Dash,Before=plan.Before,After=plan.After,X=plan.X,Y=plan.Y}.Validate(true);
                bool fit=p.AutoFit||force&&edit.Recenter;bool measure=fit||p.ChangeText||p.ChangeLabel||p.ChangeHeight||p.ChangeFont||p.ChangeGaps;
                if(measure)
                {
                    if(!force&&(type.ShapeIsUcsOrientedAt(1)||Math.Abs(type.ShapeRotationAt(1))>1e-10))throw new InvalidOperationException("Tự căn chỉ hỗ trợ R=0/U=0; chọn Chuyển R=0.");double h=plan.S*(!p.ChangeFont&&st.TextSize>0?st.TextSize:1);
                    var measured=BatchService.Measure(db,new BatchItem{Style=plan.Style,Height=h,Text=plan.Text},p);plan.X=measured.OffsetX;plan.Y=measured.OffsetY;
                    if(fit){plan.Before=measured.GapBefore;plan.After=measured.GapAfter;plan.Centered=true;}else if(plan.Before<measured.GapBefore||plan.After<measured.GapAfter)throw new InvalidOperationException("Khoảng trống không đủ chữ; chọn Tự căn.");
                }
                plan.NewFont=p.ChangeFont;if(plan.NewFont){HostApplicationServices.Current.FindFile(p.FontFile,db,FindFileHint.FontFile);plan.StyleName="MrT_GROUP_"+Guid.NewGuid().ToString("N").Substring(0,12);}
            }
            if(p.ChangeDescription)plan.Description=p.Expand(p.DescriptionTemplate,new DrawingLayerInfo{UseLabelIdentity=true,Text=row.Text,LayerName=type.Name});if(p.ChangeDescription&&((plan.Description??"").Length>200||(plan.Description??"").Any(Char.IsControl)))throw new InvalidOperationException("Mô tả tối đa 200 ký tự, một dòng.");
            if(force||owned){plan.Create=false;plan.InPlace=true;plan.Name=type.Name;plan.Target=source;plan.Input=Profile.Hash(row.Fingerprint+"|"+edit.Signature);return plan;}
            plan.Input=Profile.Hash(row.Fingerprint+"|"+edit.ForceR+"|"+edit.Recenter+"|"+p.Signature);string name=(plan.FreshR?"Mr.T_R0_":"Mr.T_Net_")+plan.Input.Substring(0,16);var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);
            if(types.Has(name)){var existing=(LinetypeTableRecord)tr.GetObject(types[name],OpenMode.ForRead);bool centered;var marker=Marker(existing,tr);if(plan.FreshR&&NativeR(existing,tr,out centered)&&marker!=null&&Convert.ToString(marker[2].Value)==plan.Input){plan.Create=false;plan.Target=existing.ObjectId;plan.Name=name;return plan;}name+="_"+Guid.NewGuid().ToString("N").Substring(0,8);}
            plan.Name=name;return plan;
        }
        public static LabelPlan Inspect(Database db,LabelScope scope,LabelEdit edit)
        {
            edit.Validate();if(!scope.All&&(scope.Keys==null||scope.Keys.Length==0))throw new ArgumentException("Chọn một nhãn hoặc Toàn bộ nhãn.");var scan=ReadScan(db);if(scope.Fingerprint!=null&&scope.Fingerprint!=scan.View.Fingerprint)throw new InvalidOperationException("Bản vẽ đã đổi; Đọc lại nhóm nhãn.");
            var plan=new LabelPlan{CatalogState=scan.View.Fingerprint,OptionsState=edit.Signature,Intent="Yêu cầu: "+(edit.ForceR||edit.Pattern.ForceR?"R=0; ":"")+(edit.ChangeColor?(edit.ByLayer?"màu ByLayer; ":"màu ACI "+edit.Aci+"; "):"")+(edit.ChangeScale?"Scale "+(edit.Multiply?"× ":"= ")+Profile.N(edit.Scale)+"; ":"")+(edit.ChangeWidth?"Width="+Profile.N(edit.Width)+"; ":"")+(edit.Pattern.ChangesPattern?"có sửa chữ/font/thông số nét":"giữ chữ/font/thông số nét nguồn")};var keys=new HashSet<string>(scope.Keys??new string[0],StringComparer.OrdinalIgnoreCase);var types=new Dictionary<ObjectId,LabelTypePlan>();var invalid=new Dictionary<ObjectId,string>();
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())foreach(var e in scan.Entries.Where(e=>LabelBrowse.LayerMatches(e.Info.Layer,scope.LayerPrefix,scope.LayerSuffix)&&(scope.All||keys.Contains(LabelRules.Key(e.Info.Text,scope.SystemMode)))))
            {
                plan.Matched++;try
                {
                    if(e.Info.Locked)throw new InvalidOperationException(e.Blocked??"Layer khóa.");if(e.Info.Linked&&!edit.ChangesType)throw new InvalidOperationException("Có MText liên kết cũ; chuyển R=0 để đưa về nét native.");
                    LabelTypePlan type;if(!types.TryGetValue(e.Type,out type)){if(invalid.ContainsKey(e.Type))throw new InvalidOperationException(invalid[e.Type]);try{type=TypePlan(db,e.Type,tr,edit);types.Add(e.Type,type);plan.Types.Add(type);}catch(InvalidOperationException ex){invalid.Add(e.Type,ex.Message);throw;}}
                    var curve=(Entity)tr.GetObject(e.Id,OpenMode.ForRead);bool width=ScaleService.HasWidth(curve);if(edit.ChangeWidth&&!width){if(!edit.ChangeScale&&!edit.ChangeColor&&!edit.ChangesType)throw new InvalidOperationException("Loại đường không có Global Width.");plan.WidthUnsupported++;}
                    double scale=edit.ChangeScale?LabelRules.Scale(e.Info.Scale,edit):e.Info.Scale;
                    plan.Items.Add(new LabelChange{Id=e.Id,Type=e.Type,Scale=scale,Handle=e.Info.Handle,Label=e.Info.Text,Layer=e.Info.Layer,Before=e.Info.Color+" | Scale="+Profile.N(e.Info.Scale)+(edit.ChangeWidth?" | Width="+ScaleService.WidthState(curve,tr):"")+" | "+e.Info.Type,After=(edit.ChangeColor?(edit.ByLayer?"ByLayer":"ACI "+edit.Aci):e.Info.Color)+" | Scale="+Profile.N(scale)+(edit.ChangeWidth&&width?" | Width="+Profile.N(edit.Width):"")+" | "+(type.Create?type.Name:edit.ChangesType?"R/nét đích "+(type.Name??e.Info.Type):e.Info.Type)+(edit.ChangesType?" | chữ="+type.Text+(type.Create&&type.S>0?" | S="+Profile.N(type.S)+" | nét/trống="+Profile.N(type.Dash)+"/"+Profile.N(type.Before)+"/"+Profile.N(type.After):"")+(edit.Pattern.ChangeFont?" | "+edit.Pattern.FontFile+" Bold="+edit.Pattern.Bold:""):"")+(e.Info.Linked?" | gỡ đúng nhãn liên kết cũ":"")});if(e.Info.Linked)plan.Linked++;
                }
                catch(System.Exception ex){if(!(ex is InvalidOperationException)&&!(ex is ArgumentException))throw;plan.Skipped.Add(e.Info.Handle+" ["+e.Info.Text+"]: "+ex.Message);}
            }
            plan.Types.RemoveAll(t=>!plan.Items.Any(i=>i.Type==t.Source));if(plan.Types.Any(t=>t.InPlace)){var affected=new HashSet<ObjectId>(plan.Types.Where(t=>t.InPlace).Select(t=>t.Source));using(var countTr=db.TransactionManager.StartOpenCloseTransaction()){var blocks=(BlockTable)countTr.GetObject(db.BlockTableId,OpenMode.ForRead);foreach(ObjectId bid in blocks){var b=(BlockTableRecord)countTr.GetObject(bid,OpenMode.ForRead);if(b.IsFromExternalReference||b.IsFromOverlayReference)continue;foreach(ObjectId id in b){var entity=countTr.GetObject(id,OpenMode.ForRead) as Entity;if(entity==null)continue;var tid=entity.LinetypeId;if(tid==db.ByLayerLinetype)tid=((LayerTableRecord)countTr.GetObject(entity.LayerId,OpenMode.ForRead)).LinetypeObjectId;if(affected.Contains(tid))plan.SharedReferences++;}}}}return plan;
        }
        public static void RestoreLegacyR(Database db,Transaction tr,ObjectId sourceId,ObjectId targetId)
        {
            var source=(LinetypeTableRecord)tr.GetObject(sourceId,OpenMode.ForRead);bool centered;if(!NativeR(source,tr,out centered))throw new InvalidOperationException("Nét mã không có dấu xác nhận R=0 hợp lệ; không tự hợp nhất.");
            var target=(LinetypeTableRecord)tr.GetObject(targetId,OpenMode.ForWrite);target.NumDashes=0;target.NumDashes=3;target.IsScaledToFit=source.IsScaledToFit;target.Comments=source.Comments;target.PatternLength=source.PatternLength;for(int i=0;i<3;i++)target.SetDashLengthAt(i,source.DashLengthAt(i));target.SetShapeStyleAt(1,source.ShapeStyleAt(1));target.SetTextAt(1,source.TextAt(1));target.SetShapeScaleAt(1,source.ShapeScaleAt(1));target.SetShapeOffsetAt(1,source.ShapeOffsetAt(1));target.SetShapeRotationAt(1,0);target.SetShapeIsUcsOrientedAt(1,false);Mark(target,tr,"restore:"+sourceId.Handle,centered);
        }
        public static int Apply(Database db,LabelPlan plan,LabelEdit edit,bool injectFailure=false)
        {
            edit.Validate();if(plan.OptionsState!=edit.Signature||Read(db).Fingerprint!=plan.CatalogState)throw new InvalidOperationException("Phạm vi/thuộc tính đã đổi sau khi xem trước; chưa ghi.");if(plan.Items.Count==0)return 0;
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);var styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForRead);var mapped=new Dictionary<ObjectId,ObjectId>();
                foreach(var item in plan.Types)
                {
                    if(!item.Create&&!item.InPlace){mapped[item.Source]=item.Target;continue;}if(!item.InPlace&&types.Has(item.Name))throw new InvalidOperationException("Tên nét đích bị chiếm; kiểm tra lại.");var source=(LinetypeTableRecord)tr.GetObject(item.Source,item.InPlace?OpenMode.ForWrite:OpenMode.ForRead);var type=item.InPlace?source:item.FreshR?new LinetypeTableRecord():(LinetypeTableRecord)source.Clone();if(item.InPlace&&item.FreshR)type.NumDashes=0;type.Name=item.Name;if(!item.InPlace){if(!types.IsWriteEnabled)types.UpgradeOpen();types.Add(type);tr.AddNewlyCreatedDBObject(type,true);}type.Comments=item.Description??"";
                    if(!item.Style.IsNull)
                    {
                        ObjectId style=item.Style;if(item.NewFont){var st=(TextStyleTableRecord)((TextStyleTableRecord)tr.GetObject(style,OpenMode.ForRead)).Clone();st.Name=item.StyleName;if(!styles.IsWriteEnabled)styles.UpgradeOpen();styles.Add(st);tr.AddNewlyCreatedDBObject(st,true);BatchService.Font(st,edit.Pattern);style=st.ObjectId;}
                        if(item.FreshR)type.NumDashes=3;type.SetDashLengthAt(0,item.Dash);type.SetDashLengthAt(1,-item.Before);type.SetDashLengthAt(2,-item.After);type.PatternLength=item.Dash+item.Before+item.After;type.SetShapeStyleAt(1,style);type.SetTextAt(1,item.Text);type.SetShapeScaleAt(1,item.S);type.SetShapeOffsetAt(1,new Vector2d(item.X,item.Y));if(item.FreshR){type.SetShapeIsUcsOrientedAt(1,false);type.SetShapeRotationAt(1,0);}
                    }if(item.FreshR)Mark(type,tr,item.Input,item.Centered);mapped[item.Source]=type.ObjectId;
                }
                foreach(var item in plan.Items)
                {
                    var e=(Entity)tr.GetObject(item.Id,OpenMode.ForWrite);if(edit.ChangesType){DirectionService.RemoveForNative(e,db,tr);var targetType=mapped[item.Type];var actualLayer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);e.LinetypeId=actualLayer.LinetypeObjectId==targetType?db.ByLayerLinetype:targetType;}if(edit.ChangeColor)e.Color=edit.ByLayer?Color.FromColorIndex(ColorMethod.ByLayer,256):Color.FromColorIndex(ColorMethod.ByAci,(short)edit.Aci);if(edit.ChangeScale)e.LinetypeScale=item.Scale;if(edit.ChangeWidth&&ScaleService.HasWidth(e))ScaleService.SetWidth(e,tr,edit.Width);ScaleService.DisableGeneration(e);if(injectFailure)throw new InvalidOperationException("Injected label-group rollback.");
                }tr.Commit();
            }db.Plinegen=false;return plan.Items.Count;
        }
    }
}
