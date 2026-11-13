using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
namespace MedGas2024.Engine
{
    public sealed class BatchItem
    {
        public DrawingLayerInfo Expected;public BatchPreviewRow Preview;public ObjectId Layer,OldType,BaseType,Style;
        public string SourceFingerprint,Entities;public bool Clone,Pattern,NewStyle;public string StyleName;
        public double Dash,Before,After,Height,X,Y;public string Text,Description;
    }
    public sealed class BatchPlan
    {
        public string Signature;public readonly List<BatchItem> Items=new List<BatchItem>();
        public List<BatchPreviewRow> Rows{get{return Items.Select(x=>x.Preview).ToList();}}
        public bool CanApply{get{return Items.Count>0&&Items.All(x=>!x.Preview.Blocked);}}
        public string Summary{get{return Items.Count+" Layer; "+Items.Count(x=>x.Preview.Blocked)+" dòng chưa hợp lệ.\r\nĐổi tên giữ LayerId và toàn bộ hình học. Nét cần sửa được sao riêng theo Layer, không ghi đè nét dùng chung.\r\nMàu ghi đè chỉ đổi nếu chọn ByLayer; cập nhật ghi đè nét chỉ cho đường trực tiếp Model/Layout khớp nét cũ.\r\nKhông xóa hình ống; chỉ cho xóa Layer trống và được AutoCAD cho phép purge. Không tự xóa Linetype/style cũ.\r\nCác thuộc tính chưa chọn được giữ nguyên. Xem cột chi tiết; có thể hủy trước khi ghi.";}}
    }
    public static class BatchService
    {
        static bool Curve(Entity e){return e is Line||e is Polyline||e is Polyline2d||e is Polyline3d||e is Arc||e is Spline;}
        static string EntityState(Database db,ObjectId layer,Transaction tr)
        {
            var values=new List<string>();var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
            foreach(ObjectId b in blocks){var space=(BlockTableRecord)tr.GetObject(b,OpenMode.ForRead);if(!space.IsLayout||space.IsFromExternalReference||space.IsFromOverlayReference)continue;foreach(ObjectId id in space){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e!=null&&e.LayerId==layer&&Curve(e))values.Add(id.Handle+":"+e.LinetypeId.Handle+":"+e.Color.ColorMethod+":"+e.Color.Red+","+e.Color.Green+","+e.Color.Blue+":"+ScaleService.GenerationOn(e));}}
            return Profile.Hash(String.Join("|",values.OrderBy(x=>x)));
        }
        static string TypeState(LinetypeTableRecord lt,Transaction tr)
        {
            var s=new StringBuilder(lt.Handle+"|"+lt.Name+"|"+lt.Comments+"|"+lt.PatternLength+"|"+lt.IsScaledToFit+"|"+lt.NumDashes);
            for(int i=0;i<lt.NumDashes;i++){s.Append('|').Append(lt.DashLengthAt(i));var st=lt.ShapeStyleAt(i);if(st.IsNull)continue;s.Append('|').Append(st.Handle).Append('|').Append(lt.ShapeNumberAt(i));if(lt.ShapeNumberAt(i)!=0)continue;string text;try{text=lt.TextAt(i);}catch(Autodesk.AutoCAD.Runtime.Exception ex){if(ex.ErrorStatus!=ErrorStatus.NotApplicable)throw;text="";}s.Append('|').Append(text).Append('|').Append(lt.ShapeOffsetAt(i)).Append('|').Append(lt.ShapeScaleAt(i)).Append('|').Append(lt.ShapeRotationAt(i)).Append('|').Append(lt.ShapeIsUcsOrientedAt(i));var font=(TextStyleTableRecord)tr.GetObject(st,OpenMode.ForRead);s.Append('|').Append(font.FileName).Append('|').Append(font.TextSize).Append('|').Append(font.XScale).Append('|').Append(font.ObliquingAngle).Append('|').Append(font.Font.TypeFace).Append(font.Font.Bold).Append(font.Font.Italic);}
            return Profile.Hash(s.ToString());
        }
        static string Labels(LinetypeTableRecord lt)
        {
            var texts=new List<string>();for(int i=0;i<lt.NumDashes;i++){if(lt.ShapeStyleAt(i).IsNull||lt.ShapeNumberAt(i)!=0)continue;try{string text=lt.TextAt(i);if(!String.IsNullOrEmpty(text))texts.Add(text);}catch(Autodesk.AutoCAD.Runtime.Exception ex){if(ex.ErrorStatus!=ErrorStatus.NotApplicable)throw;}}return String.Join(" | ",texts);
        }
        static bool Simple(LinetypeTableRecord lt)
        {
            if(lt.NumDashes!=3||lt.IsScaledToFit||lt.DashLengthAt(0)<=0||lt.DashLengthAt(1)>=0||lt.DashLengthAt(2)>=0)return false;
            var st=lt.ShapeStyleAt(1);return !st.IsNull&&lt.ShapeNumberAt(1)==0&&!String.IsNullOrEmpty(lt.TextAt(1));
        }
        internal static void Font(TextStyleTableRecord style,BatchEdit edit)
        {
            style.FileName=edit.FontFile;style.TextSize=0;style.XScale=1;style.ObliquingAngle=0;style.IsVertical=false;style.BigFontFileName="";
            string name=Path.GetFileName(edit.FontFile).ToLowerInvariant();if(name.EndsWith(".shx"))return;
            string face=name=="bahnschrift.ttf"?"Bahnschrift":name=="arial.ttf"||name=="arialbd.ttf"?"Arial":name=="segoeui.ttf"||name=="segoeuib.ttf"?"Segoe UI":null;
            if(face==null)throw new ArgumentException("Đổi TTF hiện hỗ trợ Bahnschrift, Arial hoặc Segoe UI; SHX có thể nhập tên file đã cài.");
            style.Font=new Autodesk.AutoCAD.GraphicsInterface.FontDescriptor(face,edit.Bold||name=="arialbd.ttf"||name=="segoeuib.ttf",false,0,34);
        }
        internal static GasRow Measure(Database db,BatchItem item,BatchEdit edit)
        {
            var previous=HostApplicationServices.WorkingDatabase;using(var scratch=new Database(true,true))try
            {
                ObjectId style;using(var map=new IdMapping()){db.WblockCloneObjects(new ObjectIdCollection(new[]{item.Style}),scratch.TextStyleTableId,map,DuplicateRecordCloning.Replace,false);style=map[item.Style].Value;}HostApplicationServices.WorkingDatabase=scratch;
                using(var tr=scratch.TransactionManager.StartTransaction())
                {
                    var st=(TextStyleTableRecord)tr.GetObject(style,OpenMode.ForWrite);if(edit.ChangeFont)Font(st,edit);var bt=(BlockTable)tr.GetObject(scratch.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var text=new DBText();text.SetDatabaseDefaults(scratch);ms.AppendEntity(text);tr.AddNewlyCreatedDBObject(text,true);text.TextStyleId=style;text.WidthFactor=st.XScale;text.Oblique=st.ObliquingAngle;text.TextString=item.Text;text.Height=item.Height;text.Position=Point3d.Origin;var bounds=text.GeometricExtents;var row=new GasRow{TextScale=item.Height,RotationMode="R",RotationDegrees=0};Spacing.FitSymmetric(row,bounds.MinPoint.X,bounds.MinPoint.Y,bounds.MaxPoint.X,bounds.MaxPoint.Y,edit.Padding);return row;
                }
            }finally{HostApplicationServices.WorkingDatabase=previous;}
        }
        public static BatchPlan Inspect(Database db,IEnumerable<DrawingLayerInfo> selected,BatchEdit edit)
        {
            var expected=selected.GroupBy(x=>x.LayerName,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).ToList();edit.Validate(expected.Count);var plan=new BatchPlan{Signature=edit.Signature};var current=DrawingService.Read(db);var reserved=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var newLayers=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if(edit.ChangeFont)HostApplicationServices.Current.FindFile(edit.FontFile,db,FindFileHint.FontFile);
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);
                foreach(var old in expected)
                {
                    var item=new BatchItem{Expected=old,Preview=new BatchPreviewRow{LayerBefore=old.LayerName,LayerAfter=old.LayerName,TypeBefore=old.LinetypeName,TypeAfter=old.LinetypeName,TextBefore=old.Text,TextAfter=old.Text,ColorBefore=old.ColorText,ColorAfter=old.ColorText}};plan.Items.Add(item);
                    try
                    {
                        var row=current.SingleOrDefault(r=>String.Equals(r.LayerName,old.LayerName,StringComparison.OrdinalIgnoreCase));if(row==null||row.Fingerprint!=old.Fingerprint)throw new InvalidOperationException("DWG đã đổi, cần Đọc lại.");
                        item.Layer=layers[row.LayerName];var layer=(LayerTableRecord)tr.GetObject(item.Layer,OpenMode.ForRead);if(layer.IsLocked||layer.IsDependent)throw new InvalidOperationException("Layer khóa/Xref không sửa.");item.OldType=layer.LinetypeObjectId;item.BaseType=item.OldType;item.Entities=EntityState(db,item.Layer,tr);
                        if(edit.Delete)
                        {
                            if(row.UsedCount>0)throw new InvalidOperationException("Có "+row.UsedCount+" đối tượng; giữ lại Layer và hình ống.");if(item.Layer==db.Clayer||row.LayerName=="0"||row.LayerName.Equals("Defpoints",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Layer hiện hành/Layer đặc biệt không xóa.");var purge=new ObjectIdCollection(new[]{item.Layer});db.Purge(purge);if(purge.Count!=1)throw new InvalidOperationException("AutoCAD còn tham chiếu; không cho xóa.");item.Preview.LayerAfter="[XÓA LAYER TRỐNG]";item.Preview.Detail="Không xóa Linetype/style và không xóa hình học.";continue;
                        }
                        string name=edit.NewLayer(row);if(!newLayers.Add(name))throw new InvalidOperationException("Trùng tên Layer trong nhóm: "+name);if(!String.Equals(name,row.LayerName,StringComparison.OrdinalIgnoreCase)&&layers.Has(name))throw new InvalidOperationException("Layer đích đã có: "+name);if((row.LayerName=="0"||row.LayerName.Equals("Defpoints",StringComparison.OrdinalIgnoreCase))&&name!=row.LayerName)throw new InvalidOperationException("Không đổi tên Layer đặc biệt.");item.Preview.LayerAfter=name;
                        if(edit.ChangeColor)item.Preview.ColorAfter="ACI "+edit.Aci;
                        if(edit.ChangeLinetype){if(!types.Has(edit.AssignLinetype))throw new InvalidOperationException("Nét chọn lại không còn tồn tại.");item.BaseType=types[edit.AssignLinetype];}
                        var lt=(LinetypeTableRecord)tr.GetObject(item.BaseType,OpenMode.ForRead);item.SourceFingerprint=TypeState(lt,tr);item.Preview.TextAfter=Labels(lt);item.Pattern=edit.ChangeDiameter||edit.ChangeLabel||edit.ChangeText||edit.ChangeHeight||edit.ChangeDash||edit.ChangeGaps||edit.AutoFit||edit.ChangeFont||edit.ForceR;
                        item.Clone=edit.ChangesPattern||edit.Renames||edit.ExactType;item.Description=edit.ChangeDescription?edit.Expand(edit.DescriptionTemplate,row):lt.Comments;
                        if((item.Description??"").Length>200||(item.Description??"").Any(Char.IsControl))throw new InvalidOperationException("Mô tả tối đa 200 ký tự, một dòng.");
                        if(item.Pattern)
                        {
                            if(!Simple(lt))throw new InvalidOperationException("Nét không theo cấu trúc một đoạn + một chữ + hai khoảng; chỉ đổi màu/tên/chọn lại nét.");item.Style=lt.ShapeStyleAt(1);var st=(TextStyleTableRecord)tr.GetObject(item.Style,OpenMode.ForRead);if((Math.Abs(st.TextSize)>1e-10&&!edit.ChangeFont)||st.IsVertical)throw new InvalidOperationException("Style cao cố định/xếp dọc: cần font riêng phù hợp.");
                            item.Height=edit.ChangeHeight?edit.Height:lt.ShapeScaleAt(1)*(edit.ChangeFont&&st.TextSize>0?st.TextSize:1);item.Dash=edit.ChangeDash?edit.Dash:lt.DashLengthAt(0);item.Before=edit.ChangeGaps?edit.Before:-lt.DashLengthAt(1);item.After=edit.ChangeGaps?edit.After:-lt.DashLengthAt(2);item.X=lt.ShapeOffsetAt(1).X;item.Y=lt.ShapeOffsetAt(1).Y;
                            var input=new DrawingLayerInfo{LayerName=row.LayerName,Text=lt.TextAt(1)};item.Text=edit.NewText(input);new DefinitionEdit{Text=item.Text,Description=item.Description,Height=item.Height,Dash=item.Dash,Before=item.Before,After=item.After,X=item.X,Y=item.Y}.Validate(true);
                            bool measure=edit.ChangeGaps||edit.AutoFit||edit.ChangeDiameter||edit.ChangeLabel||edit.ChangeText||edit.ChangeFont||edit.ChangeHeight;
                            if(measure){if(!edit.ForceR&&(lt.ShapeIsUcsOrientedAt(1)||Math.Abs(lt.ShapeRotationAt(1))>1e-10))throw new InvalidOperationException("Tự căn hỗ trợ U=0/R=0; chọn R=0 để đổi từ góc khác.");var fit=Measure(db,item,edit);item.X=fit.OffsetX;item.Y=fit.OffsetY;if(edit.AutoFit){item.Before=fit.GapBefore;item.After=fit.GapAfter;}else if(item.Before<fit.GapBefore||item.After<fit.GapAfter)throw new InvalidOperationException("Khoảng trống hiện tại/nhập tay không đủ chứa chữ. Tích Tự căn hoặc tăng trống trước/sau.");}
                            item.Preview.TextAfter=item.Text;item.NewStyle=edit.ChangeFont;if(item.NewStyle)item.StyleName="MrT_EDIT_"+Guid.NewGuid().ToString("N").Substring(0,12);
                        }
                        if(item.Clone)
                        {
                            string desired=edit.ExactType?edit.TypeName:name;BatchEdit.Name(desired);if(types.Has(desired)||reserved.Contains(desired)){if(edit.ExactType&&desired!=lt.Name)throw new InvalidOperationException("Linetype đích đã tồn tại; dùng Chọn lại nét nếu muốn dùng nó.");desired+="_E_"+Guid.NewGuid().ToString("N").Substring(0,8);}reserved.Add(desired);item.Preview.TypeAfter=desired;
                        }else item.Preview.TypeAfter=lt.Name;
                        item.Preview.Detail=(item.Clone?"Sao nét riêng; giữ định nghĩa cũ. ":"")+(edit.ColorByLayer?"Đường trực tiếp: màu → ByLayer. ":"Giữ ghi đè màu. ")+(edit.RebindOldOverrides&&(item.Clone||edit.ChangeLinetype)?"Ghi đè nét khớp nét cũ → nét mới. ":"Giữ ghi đè nét. ")+"Layer có "+row.UsedCount+" đối tượng; "+row.OverrideCount+" ghi đè. Nét cũ dùng bởi "+row.SharedLayers+" Layer.";
                        item.Preview.BeforeState="Mô tả: "+row.Description+"; S="+Profile.N(row.Height)+"; nét/trống="+Profile.N(row.Dash)+"/"+Profile.N(row.Before)+"/"+Profile.N(row.After)+"; "+row.Font;
                        item.Preview.AfterState="Mô tả: "+item.Description+(item.Pattern?"; S="+Profile.N(item.Height)+"; nét/trống="+Profile.N(item.Dash)+"/"+Profile.N(item.Before)+"/"+Profile.N(item.After):"; giữ thông số nét của nguồn")+(edit.ChangeFont?"; "+edit.FontFile+"; Bold="+edit.Bold:"; giữ font")+(edit.ForceR?"; R=0":"; giữ hướng chữ");
                    }
                    catch(System.Exception ex){if(!(ex is ArgumentException)&&!(ex is InvalidOperationException))throw;item.Preview.Blocked=true;item.Preview.Detail=ex.Message;}
                }
            }return plan;
        }
        public static int Apply(Database db,BatchPlan plan,BatchEdit edit,bool injectFailure=false)
        {
            edit.Validate(plan.Items.Count);if(!plan.CanApply||edit.Signature!=plan.Signature)throw new InvalidOperationException("Kế hoạch không hợp lệ hoặc thông số đã đổi.");var current=DrawingService.Read(db);
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);var styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForRead);
                // Validate the entire batch before the first write.
                foreach(var item in plan.Items)
                {
                    var now=current.SingleOrDefault(r=>r.LayerName==item.Expected.LayerName);if(now==null||((LayerTableRecord)tr.GetObject(item.Layer,OpenMode.ForRead)).IsLocked||now.Fingerprint!=item.Expected.Fingerprint||EntityState(db,item.Layer,tr)!=item.Entities)throw new InvalidOperationException("DWG đã đổi sau khi xem kế hoạch; kiểm tra lại.");
                    if(edit.Delete){var purge=new ObjectIdCollection(new[]{item.Layer});db.Purge(purge);if(now.UsedCount>0||item.Layer==db.Clayer||purge.Count!=1)throw new InvalidOperationException("Layer không còn trống/không xóa được.");continue;}
                    if(TypeState((LinetypeTableRecord)tr.GetObject(item.BaseType,OpenMode.ForRead),tr)!=item.SourceFingerprint)throw new InvalidOperationException("Nét nguồn đã đổi.");
                    if(item.Preview.LayerAfter!=now.LayerName&&layers.Has(item.Preview.LayerAfter))throw new InvalidOperationException("Tên Layer đích mới bị chiếm.");if(item.Clone&&types.Has(item.Preview.TypeAfter))throw new InvalidOperationException("Tên Linetype đích mới bị chiếm.");
                }
                foreach(var item in plan.Items)
                {
                    var layer=(LayerTableRecord)tr.GetObject(item.Layer,OpenMode.ForWrite);if(edit.Delete){layer.Erase();if(injectFailure)throw new InvalidOperationException("Injected batch rollback.");continue;}
                    ObjectId target=item.BaseType;
                    if(item.Clone)
                    {
                        var source=(LinetypeTableRecord)tr.GetObject(item.BaseType,OpenMode.ForRead);var type=edit.ForceR?new LinetypeTableRecord():(LinetypeTableRecord)source.Clone();type.Name=item.Preview.TypeAfter;if(!types.IsWriteEnabled)types.UpgradeOpen();types.Add(type);tr.AddNewlyCreatedDBObject(type,true);target=type.ObjectId;type.Comments=item.Description??"";
                        if(item.Pattern)
                        {
                            ObjectId style=item.Style;if(item.NewStyle){var st=(TextStyleTableRecord)((TextStyleTableRecord)tr.GetObject(item.Style,OpenMode.ForRead)).Clone();st.Name=item.StyleName;if(!styles.IsWriteEnabled)styles.UpgradeOpen();styles.Add(st);tr.AddNewlyCreatedDBObject(st,true);Font(st,edit);style=st.ObjectId;}
                            if(edit.ForceR){type.NumDashes=3;type.SetShapeStyleAt(1,style);type.SetShapeIsUcsOrientedAt(1,false);type.SetShapeRotationAt(1,0);}else if(item.NewStyle)type.SetShapeStyleAt(1,style);
                            type.SetDashLengthAt(0,item.Dash);type.SetDashLengthAt(1,-item.Before);type.SetDashLengthAt(2,-item.After);type.PatternLength=item.Dash+item.Before+item.After;type.SetTextAt(1,item.Text);type.SetShapeScaleAt(1,item.Height);type.SetShapeOffsetAt(1,new Vector2d(item.X,item.Y));
                        }
                    }
                    layer.Name=item.Preview.LayerAfter;layer.LinetypeObjectId=target;if(edit.ChangeColor)layer.Color=Color.FromColorIndex(ColorMethod.ByAci,(short)edit.Aci);
                    if(edit.ColorByLayer||edit.RebindOldOverrides&&target!=item.OldType)
                    {
                        var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);foreach(ObjectId b in blocks){var space=(BlockTableRecord)tr.GetObject(b,OpenMode.ForRead);if(!space.IsLayout||space.IsFromExternalReference||space.IsFromOverlayReference)continue;foreach(ObjectId id in space){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||e.LayerId!=item.Layer||!Curve(e))continue;bool rebind=edit.RebindOldOverrides&&target!=item.OldType&&e.LinetypeId==item.OldType;bool color=edit.ColorByLayer&&e.Color.ColorMethod!=ColorMethod.ByLayer;if(!rebind&&!color)continue;e.UpgradeOpen();if(rebind)e.LinetypeId=target;if(color)e.Color=Color.FromColorIndex(ColorMethod.ByLayer,256);}}
                    }
                    if(injectFailure)throw new InvalidOperationException("Injected batch rollback.");
                }tr.Commit();
            }return plan.Items.Count;
        }
    }
}
