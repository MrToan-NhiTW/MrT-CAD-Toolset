using System;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using MedGas2024.Core;
namespace MedGas2024.Engine
{
    public static class FamilyService
    {
        static ObjectId Id(Database db,string handle){return db.GetObjectId(false,new Handle(Int64.Parse(handle,NumberStyles.HexNumber,CultureInfo.InvariantCulture)),0);}
        static ObjectId Type(Database db,Entity e,Transaction tr){var id=DirectionService.NativeSource(e,db,tr);return id==db.ByLayerLinetype?((LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead)).LinetypeObjectId:id;}
        static Color Effective(Entity e,LayerTableRecord layer){var c=e.Color.ColorMethod==ColorMethod.ByLayer?layer.Color:e.Color;return c.ColorMethod==ColorMethod.ByBlock?Color.FromColorIndex(ColorMethod.ByAci,7):c;}
        static string ColorName(Color c){return c.ColorMethod==ColorMethod.ByAci?"ACI "+c.ColorIndex:"RGB "+c.Red+","+c.Green+","+c.Blue;}
        static double? Width(Entity e,Transaction tr){if(!ScaleService.HasWidth(e))return null;var values=Regex.Split(ScaleService.WidthState(e,tr),@"[:,]").Where(s=>s.Length>0).Select(s=>Double.Parse(s,CultureInfo.InvariantCulture)).Distinct().ToList();return values.Count==1?(double?)values[0]:null;}
        public static FamilySource Read(Database db,ObjectId id)
        {
            if(id.IsNull||!id.IsValid||id.IsErased||id.Database.UnmanagedObject!=db.UnmanagedObject)throw new InvalidOperationException("Ống mẫu không hợp lệ.");var catalog=LabelService.Read(db);var entry=catalog.Members.SingleOrDefault(m=>String.Equals(m.Handle,id.Handle.ToString(),StringComparison.OrdinalIgnoreCase));if(entry==null)throw new InvalidOperationException("Chọn ống trực tiếp có chữ linetype và cỡ nhận diện được; không lấy chữ rời/block/Xref/đường mẫu.");var canonical=LabelRules.Canonical(entry.Text);var match=Regex.Match(canonical,@"^D([0-9]+)\.(.+)$");if(!match.Success)throw new InvalidOperationException("Chưa nhận ra cỡ trong chữ '"+entry.Text+"'. Dùng Tạo hệ mới cho nhãn chưa có cỡ.");
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var e=(Entity)tr.GetObject(id,OpenMode.ForRead);if(DirectionService.HasLinked(e,tr))throw new InvalidOperationException("Ống có nhãn MText liên kết cũ. Chuyển về nét native R=0 trước khi lấy làm mẫu.");var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);var type=(LinetypeTableRecord)tr.GetObject(Type(db,e,tr),OpenMode.ForRead);var definition=catalog.Definitions.Single(d=>d.LinetypeName==type.Name);if(!definition.Editable||type.ShapeIsUcsOrientedAt(1)||Math.Abs(type.ShapeRotationAt(1))>1e-10)throw new InvalidOperationException("Thêm cỡ cần nét ba thành phần, một cụm chữ, U=0/R=0. Kiểu khác dùng Tạo hệ mới.");var style=(TextStyleTableRecord)tr.GetObject(type.ShapeStyleAt(1),OpenMode.ForRead);if(style.IsVertical)throw new InvalidOperationException("Chưa hỗ trợ style chữ xếp dọc.");
                var parts=LayerParts.Read(new DrawingLayerInfo{LayerName=layer.Name,Text=canonical});var width=Width(e,tr);var source=new FamilySource{Handle=id.Handle.ToString(),Layer=layer.Name,Type=type.Name,Text=entry.Text,Label=match.Groups[2].Value,Prefix=parts.Canonical?(String.Equals(parts.Prefix,"MRG",StringComparison.OrdinalIgnoreCase)?"Mr.T":parts.Prefix):"Mr.T",Code=parts.Canonical?parts.System:Regex.Replace(match.Groups[2].Value.ToUpperInvariant(),@"[^A-Z0-9_]","_"),Suffix=parts.Canonical?parts.Suffix:"BASE",Font=definition.Font,Color=ColorName(Effective(e,layer)),Height=definition.NativeTextHeight,Dash=definition.Dash,Scale=e.LinetypeScale,Width=width,WidthNote=width.HasValue?Profile.N(width.Value):ScaleService.HasWidth(e)?"Biến thiên — giữ Width ống đích":"Không có — giữ Width ống đích",Direction="Giữ hướng chữ của mẫu"};
                string recordedPrefix,recordedCode,recordedSuffix;if(FamilyNaming.Read(layer,tr,out recordedPrefix,out recordedCode,out recordedSuffix)){source.Prefix=recordedPrefix;source.Code=recordedCode;source.Suffix=recordedSuffix;}
                if(!Profile.Finite(source.Scale)||source.Scale<1e-9||source.Scale>1e6||source.Width.HasValue&&(!Profile.Finite(source.Width.Value)||source.Width.Value<0||source.Width.Value>1e9))throw new InvalidOperationException("Scale/Width mẫu ngoài phạm vi hỗ trợ.");var measured=BatchService.Measure(db,new BatchItem{Style=style.ObjectId,Text=entry.Text,Height=source.Height},new BatchEdit{Padding=.35});double glyphWidth=2*(measured.GapBefore-source.Height*.35);double inheritedPadding=(definition.Before+definition.After-glyphWidth)/(2*source.Height);source.Padding=Profile.Finite(inheritedPadding)&&inheritedPadding>0?Math.Max(.05,Math.Min(5,inheritedPadding)):.35;if(String.IsNullOrEmpty(source.Code))source.Code="SYS";source.Fingerprint=Profile.Hash(entry.State+"|"+definition.Fingerprint+"|"+definition.Font+"|"+source.Color+"|"+layer.LineWeight+"|"+layer.IsPlottable+"|"+layer.Transparency+"|"+source.Prefix+"|"+source.Code+"|"+source.Suffix);return source;
            }
        }
        static string PlanSignature(FamilySizePlan p){return Profile.Hash(p.SourceHandle+"|"+p.SourceFingerprint+"|"+p.RequestSignature+"|"+String.Join(";",p.Rows.Select(r=>r.Name+"|"+r.TypeName+"|"+r.Text+"|"+r.Create+"|"+r.State+"|"+Profile.N(r.Before)+"|"+Profile.N(r.After)+"|"+Profile.N(r.X)+"|"+Profile.N(r.Y))));}
        public static FamilySizePlan Inspect(Database db,FamilySource source,FamilyRequest request)
        {
            var now=Read(db,Id(db,source.Handle));if(source.Fingerprint!=now.Fingerprint)throw new InvalidOperationException("Ống mẫu đã đổi; lấy lại mẫu trước khi tạo.");source=now;var plan=new FamilySizePlan{SourceHandle=source.Handle,SourceFingerprint=source.Fingerprint,RequestSignature=request.Signature,Rows=request.Rows(source)};
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);var e=(Entity)tr.GetObject(Id(db,source.Handle),OpenMode.ForRead);var t=(LinetypeTableRecord)tr.GetObject(Type(db,e,tr),OpenMode.ForRead);
                foreach(var row in plan.Rows){if(layers.Has(row.Name)||types.Has(row.TypeName)){row.State="Đã có Layer/nét — giữ nguyên";continue;}var fit=BatchService.Measure(db,new BatchItem{Style=t.ShapeStyleAt(1),Text=row.Text,Height=source.Height},new BatchEdit{Padding=request.Padding});row.Before=fit.GapBefore;row.After=fit.GapAfter;row.X=fit.OffsetX;row.Y=fit.OffsetY;row.Create=true;row.State="Sẽ tạo; kế thừa mẫu, đã căn chữ";}
            }plan.Signature=PlanSignature(plan);return plan;
        }
        public static int Apply(Database db,FamilySource source,FamilyRequest request,FamilySizePlan reviewed,bool injectFailure=false)
        {
            string phase="Kiểm tra lại nguồn và tên trước khi tạo";var previous=HostApplicationServices.WorkingDatabase;
            try
            {
                HostApplicationServices.WorkingDatabase=db;
                if(reviewed==null||reviewed.SourceHandle!=source.Handle||reviewed.Signature!=PlanSignature(reviewed)||reviewed.RequestSignature!=request.Signature||reviewed.SourceFingerprint!=source.Fingerprint)throw new InvalidOperationException("Thông số/kế hoạch đã đổi; kiểm tra lại.");
                source=Read(db,Id(db,source.Handle));var fresh=Inspect(db,source,request);if(fresh.Signature!=reviewed.Signature)throw new InvalidOperationException("Tên hoặc nguồn đã đổi sau khi kiểm tra; chưa tạo.");if(!fresh.Rows.Any(r=>r.Create))return 0;
                using(var tr=db.TransactionManager.StartTransaction())
                {
                    var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForWrite);var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForWrite);var e=(Entity)tr.GetObject(Id(db,source.Handle),OpenMode.ForRead);var oldLayer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);var oldType=(LinetypeTableRecord)tr.GetObject(Type(db,e,tr),OpenMode.ForRead);
                    foreach(var row in fresh.Rows.Where(r=>r.Create))
                    {
                        phase="Tạo Linetype "+row.TypeName;
                        var t=(LinetypeTableRecord)oldType.Clone();t.Name=row.TypeName;types.Add(t);tr.AddNewlyCreatedDBObject(t,true);
                        t.Comments=row.Text+" | Theo "+source.Text;t.SetTextAt(1,row.Text);t.SetShapeOffsetAt(1,new Vector2d(row.X,row.Y));t.SetDashLengthAt(1,-row.Before);t.SetDashLengthAt(2,-row.After);t.PatternLength=oldType.DashLengthAt(0)+row.Before+row.After;
                        phase="Tạo Layer "+row.Name;
                        // A new layer must belong to this Database before accepting database ObjectIds.
                        var layer=new LayerTableRecord{Name=row.Name};layers.Add(layer);tr.AddNewlyCreatedDBObject(layer,true);
                        phase="Gán nét/màu cho Layer "+row.Name;
                        layer.LinetypeObjectId=t.ObjectId;layer.Color=Effective(e,oldLayer);layer.LineWeight=oldLayer.LineWeight;layer.IsPlottable=oldLayer.IsPlottable;layer.Transparency=oldLayer.Transparency;
                        phase="Lưu Scale/Width mẫu cho "+row.Name;
                        DrawingService.WriteFamilyDefaults(layer,tr,source.Scale,source.Width,source.Handle,source.Text);FamilyNaming.Write(layer,tr,request.Prefix,request.Code,request.Suffix,row.Diameter);if(injectFailure)throw new InvalidOperationException("Injected family creation rollback.");
                    }
                    phase="Hoàn tất batch thêm cỡ";tr.Commit();
                }
                return fresh.Rows.Count(r=>r.Create);
            }
            catch(System.Exception ex){ex.Data["MrT.Stage"]=phase;throw;}
            finally{HostApplicationServices.WorkingDatabase=previous;}
        }
    }
}
