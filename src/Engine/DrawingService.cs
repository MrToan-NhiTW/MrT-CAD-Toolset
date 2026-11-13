using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MedGas2024.Core;

namespace MedGas2024.Engine
{
    public sealed class PickedPipe
    {
        public ObjectId Id{get;set;} public string LayerName{get;set;} public string Summary{get;set;} public string Fingerprint{get;set;}
    }
    public sealed class AssignmentResult
    {
        public int Changed,Unchanged,Skipped,WidthUnsupported;public readonly List<ObjectId> AssignedIds=new List<ObjectId>();
        public override string ToString(){return "Đã gán "+Changed+"; đã đúng "+Unchanged+"; bỏ qua "+Skipped+" đối tượng (loại khác / layer khóa / không thuộc layout). Width không hỗ trợ: "+WidthUnsupported+".";}
    }
    public static class DrawingService
    {
        const string FamilyKey="MRT_FAMILY_DEFAULTS_V1";
        public static void WriteFamilyDefaults(LayerTableRecord layer,Transaction tr,double scale,double? width,string handle,string text)
        {
            layer.CreateExtensionDictionary();var d=(DBDictionary)tr.GetObject(layer.ExtensionDictionary,OpenMode.ForWrite);var x=new Xrecord();d.SetAt(FamilyKey,x);tr.AddNewlyCreatedDBObject(x,true);using(var data=new ResultBuffer(new TypedValue(1000,"MrT.Family.1"),new TypedValue(1040,scale),new TypedValue(1070,(short)(width.HasValue?1:0)),new TypedValue(1040,width??0),new TypedValue(1000,handle),new TypedValue(1000,text)))x.Data=data;
        }
        static void ReadFamilyDefaults(LayerTableRecord layer,Transaction tr,DrawingLayerInfo row)
        {
            if(layer.ExtensionDictionary.IsNull)return;var d=(DBDictionary)tr.GetObject(layer.ExtensionDictionary,OpenMode.ForRead);if(!d.Contains(FamilyKey))return;var x=(Xrecord)tr.GetObject(d.GetAt(FamilyKey),OpenMode.ForRead);using(var data=x.Data){var v=data==null?new TypedValue[0]:data.AsArray();if(v.Length!=6||Convert.ToString(v[0].Value)!="MrT.Family.1")return;double scale=Convert.ToDouble(v[1].Value),width=Convert.ToDouble(v[3].Value);if(!Profile.Finite(scale)||scale<1e-9||scale>1e6||!Profile.Finite(width)||width<0||width>1e9)return;row.HasFamilyDefaults=true;row.FamilyScale=scale;row.FamilyWidth=Convert.ToInt32(v[2].Value)==1?(double?)width:null;row.FamilySource=Convert.ToString(v[5].Value);}
        }
        private static bool Managed(LayerTableRecord layer,Transaction tr)
        {
            if(layer.Name.StartsWith("Mr.T_",StringComparison.OrdinalIgnoreCase))return true;
            if(layer.ExtensionDictionary.IsNull)return false;
            var dict=(DBDictionary)tr.GetObject(layer.ExtensionDictionary,OpenMode.ForRead);
            if(!dict.Contains("MEDGAS2024_OWNER_V1"))return false;
            var x=tr.GetObject(dict.GetAt("MEDGAS2024_OWNER_V1"),OpenMode.ForRead) as Xrecord;
            if(x==null||x.Data==null)return false;
            using(var data=x.Data)return data.AsArray().Any(v=>Convert.ToString(v.Value)=="MedGas2024:1");
        }
        private static string ColorText(Color c){return c.ColorMethod==ColorMethod.ByAci?"ACI "+c.ColorIndex:c.ColorMethod==ColorMethod.ByLayer?"ByLayer":c.ColorMethod==ColorMethod.ByBlock?"ByBlock":"RGB "+c.Red+","+c.Green+","+c.Blue;}
        private static string SegmentText(LinetypeTableRecord type,int index,ObjectId style,int shapeNumber)
        {
            // ShapeStyleAt is null for plain dashes/gaps; ShapeNumberAt is nonzero for SHX shapes.
            // TextAt throws eNotApplicable when the segment does not contain text.
            try{return LinetypeSegments.ReadText(!style.IsNull,shapeNumber,()=>type.TextAt(index));}
            catch(Autodesk.AutoCAD.Runtime.Exception ex){if(ex.ErrorStatus!=Autodesk.AutoCAD.Runtime.ErrorStatus.NotApplicable)throw;return "";}
        }
        public static List<DrawingLayerInfo> Read(Database db)
        {
            var result=new List<DrawingLayerInfo>();
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                var typeUsers=new Dictionary<ObjectId,List<DrawingLayerInfo>>();var byLayer=new Dictionary<ObjectId,DrawingLayerInfo>();
                foreach(ObjectId id in layers)
                {
                    var layer=(LayerTableRecord)tr.GetObject(id,OpenMode.ForRead);
                    if(layer.IsDependent||DrawingSearch.Preview(layer.Name))continue;
                    var lt=(LinetypeTableRecord)tr.GetObject(layer.LinetypeObjectId,OpenMode.ForRead);
                    var row=new DrawingLayerInfo{LayerName=layer.Name,LinetypeName=lt.Name,Description=lt.Comments,ColorText=ColorText(layer.Color),Aci=layer.Color.ColorMethod==ColorMethod.ByAci?layer.Color.ColorIndex:7,Managed=Managed(layer,tr),Assignable=!layer.IsLocked&&!layer.IsDependent,Writable=!layer.IsLocked&&!layer.IsOff&&!layer.IsFrozen,Text="",Font="",Details=""};
                    ReadFamilyDefaults(layer,tr,row);row.State=layer.IsLocked?"Layer khóa":layer.IsFrozen?"Đóng băng":layer.IsOff?"Layer tắt":"Sẵn sàng";
                    var signature=new StringBuilder(row.HasFamilyDefaults+"|"+Profile.N(row.FamilyScale)+"|"+(row.FamilyWidth.HasValue?Profile.N(row.FamilyWidth.Value):"-")+"|"+layer.Handle+"|"+lt.Handle+"|"+row.ColorText+"|"+row.State+"|"+lt.Comments+"|"+lt.NumDashes);
                    var details=new StringBuilder();
                    bool shape=false;int textCount=0;string middleText="";
                    for(int i=0;i<lt.NumDashes;i++)
                    {
                        ObjectId styleId=lt.ShapeStyleAt(i);int shapeNumber=styleId.IsNull?0:lt.ShapeNumberAt(i);
                        string text=SegmentText(lt,i,styleId,shapeNumber);if(i==1)middleText=text;
                        details.Append("Đoạn ").Append(i).Append(": ").Append(Profile.N(lt.DashLengthAt(i)));
                        signature.Append('|').Append(Profile.N(lt.DashLengthAt(i))).Append('|').Append(text);
                        signature.Append('|').Append(styleId.IsNull?"none":styleId.Handle.ToString()).Append('|').Append(shapeNumber);
                        if(!styleId.IsNull)
                        {
                            if(String.IsNullOrEmpty(text)){shape=true;details.Append(" • hình SHX");}
                            else
                            {
                                textCount++;row.Text+=(row.Text.Length==0?"":" | ")+text;
                                var st=(TextStyleTableRecord)tr.GetObject(styleId,OpenMode.ForRead);var offset=lt.ShapeOffsetAt(i);row.NativeTextHeight=lt.ShapeScaleAt(i)*(st.TextSize>0?st.TextSize:1);
                                string font=st.Name+" • "+st.FileName+" • "+st.Font.TypeFace+" • Bold="+st.Font.Bold+" • style H="+Profile.N(st.TextSize)+", W="+Profile.N(st.XScale)+", oblique="+Profile.N(st.ObliquingAngle)+", vertical="+st.IsVertical+", bigfont="+st.BigFontFileName;
                                row.Font=font;
                                details.Append(" • ").Append(text).Append(" • S=").Append(Profile.N(lt.ShapeScaleAt(i))).Append(" • X/Y=").Append(offset).Append(" • style ").Append(st.Name);
                                signature.Append('|').Append(font).Append('|').Append(lt.ShapeScaleAt(i)).Append('|').Append(offset).Append('|').Append(lt.ShapeRotationAt(i)).Append('|').Append(lt.ShapeIsUcsOrientedAt(i));
                            }
                        }
                        details.AppendLine();
                    }
                    if(textCount!=1)row.NativeTextHeight=0;
                    row.Editable=!lt.IsScaledToFit&&lt.NumDashes==3&&textCount==1&&!shape&&!String.IsNullOrEmpty(middleText)&&lt.ShapeScaleAt(1)>0&&lt.DashLengthAt(0)>0&&lt.DashLengthAt(1)<0&&lt.DashLengthAt(2)<0&&!lt.ShapeIsUcsOrientedAt(1)&&Math.Abs(lt.ShapeRotationAt(1))<1e-10;
                    if(row.Editable)
                    {
                        var style=(TextStyleTableRecord)tr.GetObject(lt.ShapeStyleAt(1),OpenMode.ForRead);
                        row.Editable=Math.Abs(style.TextSize)<1e-10;
                        row.Dash=lt.DashLengthAt(0);row.Before=-lt.DashLengthAt(1);row.After=-lt.DashLengthAt(2);row.Height=lt.ShapeScaleAt(1);row.X=lt.ShapeOffsetAt(1).X;row.Y=lt.ShapeOffsetAt(1).Y;
                    }
                    row.Details=details.ToString()+(row.Editable?"Giữ nguyên text style và cờ hướng chữ R/U đang có.":"Chỉ xem: cấu trúc nét chưa hỗ trợ chỉnh trực tiếp; vẫn có thể gán cho ống.");
                    row.Fingerprint=signature.ToString();result.Add(row);byLayer.Add(id,row);
                    List<DrawingLayerInfo> users;if(!typeUsers.TryGetValue(layer.LinetypeObjectId,out users)){users=new List<DrawingLayerInfo>();typeUsers.Add(layer.LinetypeObjectId,users);}users.Add(row);
                }
                var typeCount=new Dictionary<ObjectId,int>();
                var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
                foreach(ObjectId blockId in blocks)
                {
                    var block=(BlockTableRecord)tr.GetObject(blockId,OpenMode.ForRead);if(block.IsFromExternalReference||block.IsFromOverlayReference)continue;
                    foreach(ObjectId id in block)
                    {
                        var entity=tr.GetObject(id,OpenMode.ForRead) as Entity;if(entity==null)continue;
                        DrawingLayerInfo item;if(byLayer.TryGetValue(entity.LayerId,out item)){item.UsedCount++;if(entity.LinetypeId!=db.ByLayerLinetype||entity.Color.ColorMethod!=ColorMethod.ByLayer)item.OverrideCount++;}
                        ObjectId effective=entity.LinetypeId;if(effective==db.ByLayerLinetype)effective=((LayerTableRecord)tr.GetObject(entity.LayerId,OpenMode.ForRead)).LinetypeObjectId;
                        if(!typeCount.ContainsKey(effective))typeCount[effective]=0;typeCount[effective]++;
                    }
                }
                foreach(var pair in typeUsers)foreach(var row in pair.Value)
                {
                    row.SharedLayers=pair.Value.Count;row.AffectedCount=typeCount.ContainsKey(pair.Key)?typeCount[pair.Key]:0;
                    row.Fingerprint=Profile.Hash(row.Fingerprint+"|"+row.UsedCount+"|"+row.SharedLayers+"|"+row.AffectedCount);
                }
            }
            return result;
        }
        private static bool Pipe(Entity e){return e is Line||e is Polyline||e is Polyline2d||e is Polyline3d||e is Arc||e is Spline;}
        private static string SourceSignature(Entity e){return Profile.Hash(e.Layer+"|"+e.LinetypeId.Handle+"|"+ColorText(e.Color)+"|"+Profile.N(e.LinetypeScale));}
        public static PickedPipe Pick(Database db,ObjectId id)
        {
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var e=tr.GetObject(id,OpenMode.ForRead) as Entity;
                if(e==null||!Pipe(e)||DrawingSearch.Preview(e.Layer))throw new InvalidOperationException("Chọn LINE, POLYLINE, ARC hoặc SPLINE hiện hành; không chọn đường mẫu tách riêng.");
                var owner=tr.GetObject(e.OwnerId,OpenMode.ForRead) as BlockTableRecord;if(owner==null||!owner.IsLayout)throw new InvalidOperationException("Chỉ lấy từ đường trực tiếp trong Model/Layout, không lấy bên trong block/Xref.");
                return new PickedPipe{Id=id,LayerName=e.Layer,Fingerprint=SourceSignature(e),Summary="Layer: "+e.Layer+"\r\nNét đối tượng: "+e.Linetype+" • màu: "+ColorText(e.Color)+" • hệ số nét: "+Profile.N(e.LinetypeScale)+"\r\nSao chép đúng 4 thuộc tính trên, kể cả ghi đè riêng."};
            }
        }
        public static AssignmentResult Assign(Database db,IEnumerable<ObjectId> ids,string layerName,bool colorByLayer,bool keepScale,double scale,PickedPipe source=null,bool injectFailure=false,DrawingLayerInfo expected=null,double? globalWidth=null)
        {
            if(globalWidth.HasValue&&(!Profile.Finite(globalWidth.Value)||globalWidth.Value<0||globalWidth.Value>1e9))throw new ArgumentException("Global Width không hợp lệ.");
            if(!keepScale&&(!Profile.Finite(scale)||scale<=0||scale>1e6))throw new ArgumentException("Hệ số nét không hợp lệ.");
            if(expected!=null){var now=Read(db).SingleOrDefault(r=>r.LayerName==expected.LayerName);if(now==null||now.Fingerprint!=expected.Fingerprint)throw new InvalidOperationException("Layer/nét đã đổi sau khi đọc. Hãy mở lại cửa sổ gán.");}
            var result=new AssignmentResult();
            using(var tr=db.TransactionManager.StartTransaction())
            {
                Entity sample=null;if(source!=null){sample=tr.GetObject(source.Id,OpenMode.ForRead) as Entity;if(sample==null||SourceSignature(sample)!=source.Fingerprint)throw new InvalidOperationException("Ống nguồn đã đổi. Hãy lấy lại thuộc tính.");layerName=sample.Layer;}
                if(DrawingSearch.Preview(layerName))throw new InvalidOperationException("Không dùng Layer mẫu riêng để gán ống hiện hành.");
                var table=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);if(!table.Has(layerName))throw new InvalidOperationException("Layer không còn trong bản vẽ. Hãy đọc lại DWG.");
                var target=(LayerTableRecord)tr.GetObject(table[layerName],OpenMode.ForRead);
                if(target.IsLocked||target.IsDependent)throw new InvalidOperationException("Layer đích khóa hoặc thuộc Xref; chưa gán.");
                foreach(ObjectId id in ids.Distinct())
                {
                    if(id.IsNull||!id.IsValid||id.IsErased||id.Database.UnmanagedObject!=db.UnmanagedObject){result.Skipped++;continue;}
                    var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||!Pipe(e)){result.Skipped++;continue;}
                    var owner=tr.GetObject(e.OwnerId,OpenMode.ForRead) as BlockTableRecord;var old=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);
                    if(owner==null||!owner.IsLayout||old.IsLocked){result.Skipped++;continue;}
                    ObjectId type=sample==null?db.ByLayerLinetype:sample.LinetypeId;
                    bool colorChange=sample!=null?ColorText(e.Color)!=ColorText(sample.Color):colorByLayer&&e.Color.ColorMethod!=ColorMethod.ByLayer;
                    double nextScale=sample!=null?sample.LinetypeScale:keepScale?e.LinetypeScale:scale;
                    if(globalWidth.HasValue&&!ScaleService.HasWidth(e))result.WidthUnsupported++;bool widthChange=globalWidth.HasValue&&!ScaleService.WidthMatches(e,tr,globalWidth.Value);
                    if(!ScaleService.GenerationOn(e)&&!widthChange&&e.LayerId==target.ObjectId&&e.LinetypeId==type&&!colorChange&&e.LinetypeScale==nextScale){result.Unchanged++;result.AssignedIds.Add(id);continue;}
                    e.UpgradeOpen();ScaleService.DisableGeneration(e);e.LayerId=target.ObjectId;e.LinetypeId=type;e.LinetypeScale=nextScale;
                    if(sample!=null)e.Color=sample.Color;else if(colorByLayer)e.Color=Color.FromColorIndex(ColorMethod.ByLayer,256);
                    if(globalWidth.HasValue&&ScaleService.HasWidth(e))ScaleService.SetWidth(e,tr,globalWidth.Value);
                    result.Changed++;result.AssignedIds.Add(id);if(injectFailure)throw new InvalidOperationException("Injected pipe assignment rollback.");
                }
                tr.Commit();
            }
            if(result.Changed+result.Unchanged>0)db.Plinegen=false;return result;
        }
        public static string EditPlan(DrawingLayerInfo row,DefinitionEdit edit)
        {
            edit.Validate(row.Editable);
            return row.LayerName+" ← "+row.LinetypeName+"\r\nChữ: "+row.Text+" → "+edit.Text+"\r\nCao chữ: "+Profile.N(row.Height)+" → "+Profile.N(edit.Height)+"\r\nNét / trống trước / sau: "+Profile.N(row.Dash)+" / "+Profile.N(row.Before)+" / "+Profile.N(row.After)+" → "+Profile.N(edit.Dash)+" / "+Profile.N(edit.Before)+" / "+Profile.N(edit.After)+"\r\nMô tả: "+row.Description+" → "+edit.Description+"\r\nMàu Layer: "+row.ColorText+" → "+(edit.ChangeColor?"ACI "+edit.Aci:"giữ nguyên")+"\r\n"+(edit.AutoFit?"Tự căn theo font/style thực trong DWG.\r\n":"Offset X/Y: "+row.X+" / "+row.Y+" → "+edit.X+" / "+edit.Y+"\r\n")+"Ảnh hưởng: "+row.SharedLayers+" Layer dùng chung nét; "+row.AffectedCount+" đối tượng dùng nét (kể cả trong định nghĩa block). Layer có "+row.UsedCount+" đối tượng; "+row.OverrideCount+" có ghi đè nét/màu.\r\nChỉ cập nhật định nghĩa; không đặt lại ghi đè riêng trên ống. Font/style và hướng chữ giữ nguyên.";
        }
        public static void PrepareEdit(Database db,DrawingLayerInfo row,DefinitionEdit edit)
        {
            edit.Validate(row.Editable);if(!edit.AutoFit)return;
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);var layer=(LayerTableRecord)tr.GetObject(layers[row.LayerName],OpenMode.ForRead);var lt=(LinetypeTableRecord)tr.GetObject(layer.LinetypeObjectId,OpenMode.ForRead);
                var fit=Measure(db,lt.ShapeStyleAt(1),edit.Text,edit.Height,edit.Dash);edit.X=fit.OffsetX;edit.Y=fit.OffsetY;edit.Before=fit.GapBefore;edit.After=fit.GapAfter;edit.AutoFit=false;
            }
        }
        public static void Update(Database db,DrawingLayerInfo expected,DefinitionEdit edit)
        {
            edit.Validate(expected.Editable);if(!expected.Editable)throw new InvalidOperationException("Kiểu nét này chỉ xem, không ghi lại từ mô hình đơn giản.");
            var current=Read(db).SingleOrDefault(r=>r.LayerName==expected.LayerName);
            if(current==null||current.Fingerprint!=expected.Fingerprint)throw new InvalidOperationException("DWG đã đổi sau lúc đọc. Hãy Đọc lại DWG trước khi cập nhật.");
            if(!current.Writable)throw new InvalidOperationException("Layer khóa/tắt/đóng băng; chưa cập nhật.");
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);var layer=(LayerTableRecord)tr.GetObject(layers[current.LayerName],OpenMode.ForRead);
                var lt=(LinetypeTableRecord)tr.GetObject(layer.LinetypeObjectId,OpenMode.ForRead);
                double x=edit.X,y=edit.Y,before=edit.Before,after=edit.After;
                if(edit.AutoFit){var fit=Measure(db,lt.ShapeStyleAt(1),edit.Text,edit.Height,edit.Dash);x=fit.OffsetX;y=fit.OffsetY;before=fit.GapBefore;after=fit.GapAfter;}
                lt.UpgradeOpen();lt.Comments=edit.Description??"";lt.SetDashLengthAt(0,edit.Dash);lt.SetDashLengthAt(1,-before);lt.SetDashLengthAt(2,-after);lt.PatternLength=edit.Dash+before+after;
                lt.SetTextAt(1,edit.Text);lt.SetShapeScaleAt(1,edit.Height);lt.SetShapeOffsetAt(1,new Vector2d(x,y));
                // Do not reset NumDashes, text style or R/U native flags.
                if(edit.ChangeColor){layer.UpgradeOpen();layer.Color=Color.FromColorIndex(ColorMethod.ByAci,(short)edit.Aci);}
                tr.Commit();
            }
        }
        private static GasRow Measure(Database source,ObjectId style,string text,double height,double dash)
        {
            var previous=HostApplicationServices.WorkingDatabase;
            using(var scratch=new Database(true,true))
            {
                try
                {
                    ObjectId mapped;using(var map=new IdMapping()){source.WblockCloneObjects(new ObjectIdCollection(new[]{style}),scratch.TextStyleTableId,map,DuplicateRecordCloning.Replace,false);mapped=map[style].Value;}
                    HostApplicationServices.WorkingDatabase=scratch;
                    using(var tr=scratch.TransactionManager.StartTransaction())
                    {
                        var bt=(BlockTable)tr.GetObject(scratch.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
                        using(var t=new DBText()){t.SetDatabaseDefaults(scratch);model.AppendEntity(t);tr.AddNewlyCreatedDBObject(t,true);t.TextStyleId=mapped;var actualStyle=(TextStyleTableRecord)tr.GetObject(mapped,OpenMode.ForRead);t.WidthFactor=actualStyle.XScale;t.Oblique=actualStyle.ObliquingAngle;t.TextString=text;t.Height=height;t.Position=Point3d.Origin;var ext=t.GeometricExtents;var row=new GasRow{TextScale=height,Dash=dash,RotationMode="R",RotationDegrees=0};Spacing.FitSymmetric(row,ext.MinPoint.X,ext.MinPoint.Y,ext.MaxPoint.X,ext.MaxPoint.Y,.35);return row;}
                    }
                }
                finally{HostApplicationServices.WorkingDatabase=previous;}
            }
        }
    }
}
