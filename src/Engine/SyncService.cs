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
    public sealed class PlanRow
    {
        public string Name { get; set; }
        public string LayerName { get; set; }
        public string LegacyLayerName { get; set; }
        public string LegacyLinetypeName { get; set; }
        public string Linetype { get; set; }
        public string Layer { get; set; }
        public string Detail { get; set; }
        public bool Conflict { get; set; }
    }
    public static class SyncService
    {
        private const string OwnerKey="MEDGAS2024_OWNER_V1";
        private const string OwnerValue="MedGas2024:1";
        public static string ResolvedFont(Database db, Profile p)
        {
            if(Path.GetExtension(p.FontFile).Equals(".ttf",StringComparison.OrdinalIgnoreCase))
            {
                string installed=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),Path.GetFileName(p.FontFile));
                if(File.Exists(installed)) return installed;
            }
            return HostApplicationServices.Current.FindFile(p.FontFile,db,FindFileHint.FontFile);
        }
        public static string FontSummary(Database db,Profile p)
        {
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForRead);
                if(!styles.Has(p.StyleName)) return "Style mới chưa được tạo trong DWG.";
                var style=(TextStyleTableRecord)tr.GetObject(styles[p.StyleName],OpenMode.ForRead);
                return "Font thực trong DWG: "+style.Font.TypeFace+" | Bold="+style.Font.Bold+" | File="+style.FileName;
            }
        }
        private static void ConfigureStyle(TextStyleTableRecord s,Profile p)
        {
            s.FileName=p.FontFile; s.TextSize=0; s.XScale=1; s.ObliquingAngle=0; s.IsVertical=false; s.BigFontFileName="";
            if(p.FontFile.EndsWith(".ttf",StringComparison.OrdinalIgnoreCase))
            {
                string filename=Path.GetFileName(p.FontFile).ToLowerInvariant();
                string face=filename=="bahnschrift.ttf"?"Bahnschrift":filename=="arial.ttf" || filename=="arialbd.ttf"?"Arial":s.Font.TypeFace;
                if(!String.IsNullOrEmpty(face)) s.Font=new Autodesk.AutoCAD.GraphicsInterface.FontDescriptor(face,p.FontBold || filename=="arialbd.ttf",false,0,34);
            }
        }
        private sealed class DatabaseScope : IDisposable
        {
            private readonly Database previous;
            public DatabaseScope(Database db) { previous=HostApplicationServices.WorkingDatabase; HostApplicationServices.WorkingDatabase=db; }
            public void Dispose() { HostApplicationServices.WorkingDatabase=previous; }
        }
        private static void RequireDatabase(ObjectId id,Database db,string purpose)
        {
            if(id.IsNull || !id.IsValid || id.Database.UnmanagedObject!=db.UnmanagedObject)
                throw new InvalidOperationException(purpose+": tham chiếu không thuộc bản vẽ đích.");
        }
        private static ObjectId EnsureStyle(Database db,Transaction tr,Profile p)
        {
            var styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForRead);
            TextStyleTableRecord style;
            if(styles.Has(p.StyleName)) style=(TextStyleTableRecord)tr.GetObject(styles[p.StyleName],OpenMode.ForWrite);
            else
            {
                styles.UpgradeOpen(); style=new TextStyleTableRecord {Name=p.StyleName};
                styles.Add(style); tr.AddNewlyCreatedDBObject(style,true);
            }
            // Configure the font after the record is resident in its owning database.
            ConfigureStyle(style,p); Stamp(style,tr,Profile.Hash(p.FontFile+"|"+p.FontBold));
            return style.ObjectId;
        }
        public static void Prepare(Database db,Profile p)
        {
            try { if(p.AutoFitOnSync) FitTextSpacing(db,p); p.RefreshNames(); }
            catch(System.Exception ex) { ex.Data["MrT.Stage"]="Đo font / tự căn chữ"; throw; }
        }
        private static string Mark(DBObject obj, Transaction tr)
        {
            if (obj.ExtensionDictionary.IsNull) return "";
            var d=(DBDictionary)tr.GetObject(obj.ExtensionDictionary,OpenMode.ForRead);
            if (!d.Contains(OwnerKey)) return "";
            var x=tr.GetObject(d.GetAt(OwnerKey),OpenMode.ForRead) as Xrecord;
            if (x==null || x.Data==null) return "";
            using(var b=x.Data) return String.Join("|",b.AsArray().Select(v=>Convert.ToString(v.Value)).ToArray());
        }
        private static bool Owned(DBObject obj, Transaction tr) { return Mark(obj,tr).StartsWith(OwnerValue+"|",StringComparison.Ordinal); }
        private static void Stamp(DBObject obj, Transaction tr, string sig)
        {
            if (!obj.IsWriteEnabled) obj.UpgradeOpen();
            if (obj.ExtensionDictionary.IsNull) obj.CreateExtensionDictionary();
            var d=(DBDictionary)tr.GetObject(obj.ExtensionDictionary,OpenMode.ForWrite);
            Xrecord x;
            if (d.Contains(OwnerKey)) x=(Xrecord)tr.GetObject(d.GetAt(OwnerKey),OpenMode.ForWrite);
            else { x=new Xrecord(); d.SetAt(OwnerKey,x); tr.AddNewlyCreatedDBObject(x,true); }
            using(var b=new ResultBuffer(new TypedValue(1000,OwnerValue),new TypedValue(1000,sig))) x.Data=b;
        }
        private static bool Near(double a,double b) { return Math.Abs(a-b)<=1e-10*Math.Max(1,Math.Max(Math.Abs(a),Math.Abs(b))); }
        private static bool StyleMatches(TextStyleTableRecord s,Profile p)
        {
            bool bold=p.FontBold || Path.GetFileName(p.FontFile).Equals("arialbd.ttf",StringComparison.OrdinalIgnoreCase);
            return String.Equals(Path.GetFileName(s.FileName),Path.GetFileName(p.FontFile),StringComparison.OrdinalIgnoreCase) &&
                (!p.FontFile.EndsWith(".ttf",StringComparison.OrdinalIgnoreCase) || s.Font.Bold==bold) && String.IsNullOrEmpty(s.BigFontFileName) && Near(s.TextSize,0) && Near(s.XScale,1) && Near(s.ObliquingAngle,0) && !s.IsVertical;
        }
        private static bool TypeMatches(LinetypeTableRecord lt,Profile p,GasRow r,ObjectId style,Transaction tr)
        {
            if (!Mark(lt,tr).Equals(OwnerValue+"|"+p.Signature(r),StringComparison.Ordinal) || lt.NumDashes!=3 || lt.IsScaledToFit || lt.Comments!=r.DescriptionText) return false;
            double k=p.GeometryFactor;
            return Near(lt.PatternLength,r.Period*k) && Near(lt.DashLengthAt(0),r.Dash*k) && Near(lt.DashLengthAt(1),-r.GapBefore*k) && Near(lt.DashLengthAt(2),-r.GapAfter*k)
                && lt.TextAt(1)==r.Text && lt.ShapeStyleAt(1)==style && Near(lt.ShapeScaleAt(1),r.TextScale*k)
                && Near(lt.ShapeOffsetAt(1).X,r.OffsetX*k) && Near(lt.ShapeOffsetAt(1).Y,r.OffsetY*k)
                && Near(lt.ShapeRotationAt(1),r.RotationDegrees*Math.PI/180) && lt.ShapeIsUcsOrientedAt(1)==(r.RotationMode=="A");
        }
        public static List<PlanRow> Inspect(Database db,Profile p)
        {
            p.RequireValid(); ResolvedFont(db,p); Prepare(db,p);
            var result=new List<PlanRow>();
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                var styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForRead);
                ObjectId style=styles.Has(p.StyleName)?styles[p.StyleName]:ObjectId.Null;
                bool styleConflict=false, styleChanged=false;
                if (!style.IsNull)
                {
                    var st=(TextStyleTableRecord)tr.GetObject(style,OpenMode.ForRead);
                    styleConflict=!Owned(st,tr); styleChanged=!StyleMatches(st,p);
                }
                foreach(var r in p.Selected)
                {
                    string name=p.NameFor(r), layerName=p.LayerNameFor(r), legacyName=p.LegacyNameFor(r); var item=new PlanRow {Name=name,LayerName=layerName,Detail=""};
                    item.Conflict=styleConflict;
                    if(styleConflict) item.Detail="Trùng tên text style không thuộc add-in. ";
                    if(types.Has(name))
                    {
                        var t=(LinetypeTableRecord)tr.GetObject(types[name],OpenMode.ForRead);
                        if(!Owned(t,tr) || t.IsDependent) { item.Linetype="Xung đột"; item.Conflict=true; }
                        else item.Linetype=(!styleChanged && TypeMatches(t,p,r,style,tr))?"Giữ nguyên":"Cập nhật";
                    }
                    else
                    {
                        item.Linetype="Tạo mới";
                        if(legacyName!=name && types.Has(legacyName))
                        {
                            var old=(LinetypeTableRecord)tr.GetObject(types[legacyName],OpenMode.ForRead);
                            if(Owned(old,tr) && !old.IsDependent) { item.Linetype="Đổi tên"; item.LegacyLinetypeName=legacyName; item.Detail+="Linetype "+legacyName+" → "+name+". "; }
                        }
                    }
                    if(layers.Has(layerName))
                    {
                        var l=(LayerTableRecord)tr.GetObject(layers[layerName],OpenMode.ForRead);
                        if(!Owned(l,tr) || l.IsDependent) { item.Layer="Xung đột"; item.Conflict=true; }
                        else item.Layer=types.Has(name) && l.LinetypeObjectId==types[name] && l.Color.ColorMethod==ColorMethod.ByAci && l.Color.ColorIndex==r.ColorIndex ? "Giữ nguyên":"Cập nhật";
                        if(l.IsLocked || l.IsFrozen || l.IsOff) item.Detail+="Layer đang khóa/đóng băng/tắt; giữ trạng thái. ";
                    }
                    else
                    {
                        item.Layer="Tạo mới";
                        if(layerName!=legacyName && layers.Has(legacyName))
                        {
                            var legacy=(LayerTableRecord)tr.GetObject(layers[legacyName],OpenMode.ForRead);
                            if(Owned(legacy,tr) && !legacy.IsDependent)
                            {
                                item.Layer="Đổi tên"; item.LegacyLayerName=legacyName; item.Detail+=legacyName+" → "+layerName+"; giữ đối tượng trên layer. ";
                            }
                        }
                    }
                    if(item.Conflict) item.Detail+="Không ghi đè đối tượng ngoài add-in.";
                    result.Add(item);
                }
            }
            return result;
        }
        public static int Apply(Database db,Profile p,bool injectFailure)
        {
            var plan=Inspect(db,p);
            if(plan.Any(x=>x.Conflict)) throw new InvalidOperationException("Có tên xung đột; kiểm tra kế hoạch trước khi đồng bộ.");
            using(var scope=new DatabaseScope(db))
            using(var tr=db.TransactionManager.StartTransaction())
            {
                int count=ApplyDefinitions(db,p,plan,tr,injectFailure); tr.Commit(); return count;
            }
        }
        private static int ApplyDefinitions(Database db,Profile p,List<PlanRow> plan,Transaction tr,bool injectFailure)
        {
            return p.Selected.Any(r=>r.RotationMode=="U") ? ApplyViaLinStage(db,p,plan,tr,injectFailure) : ApplyDirect(db,p,plan,tr,injectFailure);
        }
        private static int ApplyDirect(Database db,Profile p,List<PlanRow> plan,Transaction tr,bool injectFailure)
        {
            string phase="Tạo style trong bản vẽ đích";
            try
            {
                var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                ObjectId style=ObjectId.Null;
                foreach(var r in p.Selected)
                {
                    var item=plan.First(x=>x.Name==p.NameFor(r));
                    if(item.Linetype=="Giữ nguyên") continue;
                    if(style.IsNull) style=EnsureStyle(db,tr,p);
                    phase="Tạo/cập nhật linetype "+p.NameFor(r);
                    LinetypeTableRecord lt;
                    if(types.Has(item.Name)) lt=(LinetypeTableRecord)tr.GetObject(types[item.Name],OpenMode.ForWrite);
                    else if(item.LegacyLinetypeName!=null)
                    { lt=(LinetypeTableRecord)tr.GetObject(types[item.LegacyLinetypeName],OpenMode.ForWrite); lt.Name=item.Name; }
                    else
                    {
                        if(!types.IsWriteEnabled) types.UpgradeOpen();
                        lt=new LinetypeTableRecord {Name=item.Name}; types.Add(lt); tr.AddNewlyCreatedDBObject(lt,true);
                    }
                    RequireDatabase(style,db,"Text style"); RequireDatabase(lt.ObjectId,db,"Linetype");
                    // Reset all segment flags, including any upright flag from an earlier definition.
                    lt.NumDashes=0; lt.NumDashes=3; lt.IsScaledToFit=false;
                    double k=p.GeometryFactor;
                    lt.PatternLength=r.Period*k; lt.Comments=r.DescriptionText;
                    lt.SetDashLengthAt(0,r.Dash*k); lt.SetDashLengthAt(1,-r.GapBefore*k); lt.SetDashLengthAt(2,-r.GapAfter*k);
                    lt.SetShapeStyleAt(1,style); lt.SetShapeNumberAt(1,0); lt.SetTextAt(1,r.Text);
                    lt.SetShapeScaleAt(1,r.TextScale*k); lt.SetShapeOffsetAt(1,new Vector2d(r.OffsetX*k,r.OffsetY*k));
                    lt.SetShapeIsUcsOrientedAt(1,r.RotationMode=="A"); lt.SetShapeRotationAt(1,r.RotationDegrees*Math.PI/180);
                    Stamp(lt,tr,p.Signature(r));
                }
                if(injectFailure) throw new InvalidOperationException("Injected rollback test.");
                int count=0;
                foreach(var r in p.Selected)
                {
                    var item=plan.First(x=>x.Name==p.NameFor(r));
                    if(item.Layer=="Giữ nguyên" && item.Linetype=="Giữ nguyên") continue;
                    phase="Đồng bộ layer "+item.LayerName;
                    LayerTableRecord layer;
                    if(layers.Has(item.LayerName)) layer=(LayerTableRecord)tr.GetObject(layers[item.LayerName],OpenMode.ForWrite);
                    else if(item.LegacyLayerName!=null)
                    { layer=(LayerTableRecord)tr.GetObject(layers[item.LegacyLayerName],OpenMode.ForWrite); layer.Name=item.LayerName; }
                    else
                    {
                        if(!layers.IsWriteEnabled) layers.UpgradeOpen();
                        layer=new LayerTableRecord {Name=item.LayerName}; layers.Add(layer); tr.AddNewlyCreatedDBObject(layer,true);
                    }
                    ObjectId type=types[item.Name]; RequireDatabase(type,db,"Linetype của layer");
                    layer.LinetypeObjectId=type; layer.Color=Color.FromColorIndex(ColorMethod.ByAci,(short)r.ColorIndex);
                    Stamp(layer,tr,p.Signature(r)+":"+r.ColorIndex);FamilyNaming.Write(layer,tr,p.PrefixFor(r),r.SystemCode,p.SuffixFor(r),r.Diameter); count++;
                }
                return count;
            }
            catch(System.Exception ex) { ex.Data["MrT.Stage"]=phase; throw; }
        }
        // The 2024 managed API has no U setter. Load in a scratch database and clone
        // complete native records so the upright bit survives, under the target transaction.
        private static int ApplyViaLinStage(Database db,Profile p,List<PlanRow> plan,Transaction target,bool injectFailure)
        {
            var changed=p.Selected.Where(r=>plan.First(x=>x.Name==p.NameFor(r)).Linetype!="Giữ nguyên").ToList();
            // U must first be loaded from LIN. Use temporary ASCII identifiers and text;
            // then set the actual Unicode text and final name via the native database API.
            var staging=new Profile{SchemaVersion=p.SchemaVersion,UnitTag=p.UnitTag,GeometryFactor=p.GeometryFactor,FontFile=p.FontFile,FontBold=p.FontBold,PaddingRatio=p.PaddingRatio,LayerPrefix="MRTSTAGE",SessionStyleName=p.StyleName,Rows=new List<GasRow>()};
            for(int i=0;i<changed.Count;i++){var row=changed[i].Copy();row.SystemCode="S"+i;row.LabelPrefix="TEMP";row.CustomText=null;row.Description="TEMP";row.PrefixOverride=null;row.SuffixOverride=null;row.LinetypeNameOverride=null;row.Enabled=true;staging.Rows.Add(row);}
            string temp=Path.Combine(Path.GetTempPath(),"MedGas2024-"+Guid.NewGuid().ToString("N")+".lin");
            string phase="Nạp linetype U=0 trong bản vẽ tạm";
            try
            {
                using(var stage=new Database(true,true))
                {
                    if(changed.Count>0)
                    {
                        using(var scope=new DatabaseScope(stage))
                        using(var tr=stage.TransactionManager.StartTransaction())
                        {
                            EnsureStyle(stage,tr,p);
                            tr.Commit();
                        }
                        File.WriteAllText(temp,staging.ExportLin(),Encoding.ASCII);
                        using(var scope=new DatabaseScope(stage))
                            foreach(var r in staging.Rows) stage.LoadLineTypeFile(staging.NameFor(r),temp);
                        using(var scope=new DatabaseScope(stage))
                        using(var tr=stage.TransactionManager.StartTransaction())
                        {
                            var lt=(LinetypeTable)tr.GetObject(stage.LinetypeTableId,OpenMode.ForRead);
                            for(int i=0;i<changed.Count;i++)
                            {
                                var r=changed[i];var type=(LinetypeTableRecord)tr.GetObject(lt[staging.NameFor(staging.Rows[i])],OpenMode.ForWrite);
                                type.Name=p.NameFor(r);type.SetTextAt(1,r.Text);type.Comments=r.DescriptionText; Stamp(type,tr,p.Signature(r));
                            }
                            tr.Commit();
                        }
                    }
                    // The caller owns DatabaseScope(db) and the atomic target transaction.
                    if(changed.Count>0)
                    {
                        phase="Đổi tên linetype cũ do add-in quản lý";
                        using(var rename=db.TransactionManager.StartTransaction())
                        {
                            var oldTypes=(LinetypeTable)rename.GetObject(db.LinetypeTableId,OpenMode.ForRead);
                            foreach(var item in plan.Where(x=>x.LegacyLinetypeName!=null))
                            {
                                var legacy=(LinetypeTableRecord)rename.GetObject(oldTypes[item.LegacyLinetypeName],OpenMode.ForWrite);
                                legacy.Name=item.Name;
                            }
                            rename.Commit();
                        }
                        phase="Chuyển định nghĩa U=0 vào bản vẽ đích";
                        var ids=new ObjectIdCollection();
                        using(var tr=stage.TransactionManager.StartTransaction())
                        {
                            var table=(LinetypeTable)tr.GetObject(stage.LinetypeTableId,OpenMode.ForRead);
                            foreach(var r in changed) ids.Add(table[p.NameFor(r)]);
                        }
                        using(var map=new IdMapping()) stage.WblockCloneObjects(ids,db.LinetypeTableId,map,DuplicateRecordCloning.Replace,false);
                    }
                    phase="Kiểm tra tham chiếu style và linetype đích";
                    var types=(LinetypeTable)target.GetObject(db.LinetypeTableId,OpenMode.ForRead);
                    if(changed.Count>0)
                    {
                        ObjectId style=EnsureStyle(db,target,p); RequireDatabase(style,db,"Style chữ U");
                        foreach(var r in changed)
                        {
                            var type=(LinetypeTableRecord)target.GetObject(types[p.NameFor(r)],OpenMode.ForWrite);
                            RequireDatabase(type.ObjectId,db,"Linetype U"); RequireDatabase(type.ShapeStyleAt(1),db,"Style sau nạp U");
                            type.SetShapeStyleAt(1,style); type.Comments=r.DescriptionText; Stamp(type,target,p.Signature(r));
                        }
                    }
                    if(injectFailure) throw new InvalidOperationException("Injected rollback test.");
                    phase="Đồng bộ layer với linetype U";
                    var layers=(LayerTable)target.GetObject(db.LayerTableId,OpenMode.ForRead);
                    int count=0;
                    foreach(var r in p.Selected)
                    {
                        var status=plan.First(x=>x.Name==p.NameFor(r));
                        if(status.Layer=="Giữ nguyên" && status.Linetype=="Giữ nguyên") continue;
                        LayerTableRecord layer;
                        if(layers.Has(status.LayerName)) layer=(LayerTableRecord)target.GetObject(layers[status.LayerName],OpenMode.ForWrite);
                        else if(status.LegacyLayerName!=null)
                        { layer=(LayerTableRecord)target.GetObject(layers[status.LegacyLayerName],OpenMode.ForWrite); layer.Name=status.LayerName; }
                        else
                        {
                            if(!layers.IsWriteEnabled) layers.UpgradeOpen();
                            layer=new LayerTableRecord {Name=status.LayerName}; layers.Add(layer); target.AddNewlyCreatedDBObject(layer,true);
                        }
                        ObjectId type=types[status.Name]; RequireDatabase(type,db,"Linetype của layer U");
                        layer.LinetypeObjectId=type; layer.Color=Color.FromColorIndex(ColorMethod.ByAci,(short)r.ColorIndex);
                        Stamp(layer,target,p.Signature(r)+":"+r.ColorIndex);FamilyNaming.Write(layer,target,p.PrefixFor(r),r.SystemCode,p.SuffixFor(r),r.Diameter); count++;
                    }
                    return count;
                }
            }
            catch(System.Exception ex) { ex.Data["MrT.Stage"]=phase; throw; }
            finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        public static int Apply(Database db,Profile p) { return Apply(db,p,false); }
        public static string FitTextSpacing(Database context,Profile p)
        {
            p.RequireValid(); ResolvedFont(context,p);
            if(p.Selected.Any(r=>(r.RotationMode!="R" && r.RotationMode!="U") || Math.Abs(r.RotationDegrees)>1e-10))
                throw new InvalidOperationException("Tự căn đối xứng hỗ trợ U=0 hoặc R=0. Chọn hướng chữ tương ứng hoặc tắt Tự căn để giữ thông số nhập tay.");
            var dimensions=new List<Extents3d>();
            using(var stage=new Database(true,true))
            {
                var prev=HostApplicationServices.WorkingDatabase;
                try
                {
                    HostApplicationServices.WorkingDatabase=stage;
                    using(var tr=stage.TransactionManager.StartTransaction())
                    {
                        var styles=(TextStyleTable)tr.GetObject(stage.TextStyleTableId,OpenMode.ForWrite);
                        ObjectId styleId=EnsureStyle(stage,tr,p);
                        var bt=(BlockTable)tr.GetObject(stage.BlockTableId,OpenMode.ForRead);
                        var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
                        foreach(var r in p.Selected)
                        {
                            var text=new DBText(); text.SetDatabaseDefaults(stage);
                            ms.AppendEntity(text); tr.AddNewlyCreatedDBObject(text,true);
                            RequireDatabase(styleId,stage,"Style đo chữ"); text.TextStyleId=styleId;
                            text.TextString=r.Text; text.Height=r.TextScale; text.WidthFactor=1; text.Position=Point3d.Origin;
                            var bounds=text.GeometricExtents;
                            if(bounds.MaxPoint.X-bounds.MinPoint.X<=0) throw new InvalidOperationException("Không đo được chiều rộng chữ: "+r.Text);
                            dimensions.Add(bounds);
                        }
                    }
                }
                finally { HostApplicationServices.WorkingDatabase=prev; }
            }
            var selected=p.Selected;
            for(int i=0;i<selected.Count;i++)
            {
                var r=selected[i]; var b=dimensions[i];
                FitSymmetricLayout(r,b.MinPoint.X,b.MinPoint.Y,b.MaxPoint.X,b.MaxPoint.Y,p.PaddingRatio);
            }
            return "Đã đo chữ bằng font "+p.FontFile+" và tính khoảng hở hai bên = "+Profile.N(p.PaddingRatio)+" × cao chữ cho "+selected.Count+" dòng. Chỉ sửa cấu hình trên giao diện; kiểm tra rồi đồng bộ khi phù hợp.";
        }
        private static void FitSymmetricLayout(GasRow r,double minX,double minY,double maxX,double maxY,double paddingRatio)
        {
            Spacing.FitSymmetric(r,minX,minY,maxX,maxY,paddingRatio);
        }
        public static int CreateSamples(Database db,Profile p,Point3d origin)
        {
            var plan=Inspect(db,p);
            if(plan.Any(x=>x.Conflict)) throw new InvalidOperationException("Có tên xung đột; chưa thể vẽ mẫu.");
            using(var scope=new DatabaseScope(db))
            using(var tr=db.TransactionManager.StartTransaction())
            {
                ApplyDefinitions(db,p,plan,tr,false);
                int count=AppendSamples(db,p,origin,tr); tr.Commit(); return count;
            }
        }
        public static void MakeSamples(Database db,Profile p,Point3d origin)
        {
            using(var scope=new DatabaseScope(db))
            using(var tr=db.TransactionManager.StartTransaction())
            { AppendSamples(db,p,origin,tr); tr.Commit(); }
        }
        private static int AppendSamples(Database db,Profile p,Point3d origin,Transaction tr)
        {
            string phase="Mở Model của bản vẽ đích";
            try
            {
                var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
                var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                int i=0; double k=p.GeometryFactor;double sampleY=origin.Y;
                ObjectId byLayer=db.ByLayerLinetype; RequireDatabase(byLayer,db,"ByLayer của đường mẫu");
                foreach(var r in p.Selected)
                {
                    phase="Vẽ hai hướng: "+p.LayerNameFor(r);
                    ObjectId layer=layers[p.LayerNameFor(r)]; RequireDatabase(layer,db,"Layer đường mẫu");
                    double y=sampleY;double reverseGap=Math.Max(1.1*k,1.5*r.TextScale*k),rowGap=Math.Max(1.9*k,1.5*r.TextScale*k),length=Math.Max(40,3*r.Period)*k;
                    AppendLine(db,tr,ms,layer,byLayer,new Point3d(origin.X,y,origin.Z),new Point3d(origin.X+length,y,origin.Z));
                    AppendLine(db,tr,ms,layer,byLayer,new Point3d(origin.X+length,y-reverseGap,origin.Z),new Point3d(origin.X,y-reverseGap,origin.Z));
                    sampleY=y-reverseGap-rowGap;i++;
                }
                return i*2;
            }
            catch(System.Exception ex) { ex.Data["MrT.Stage"]=phase; throw; }
        }
        private static void AppendLine(Database db,Transaction tr,BlockTableRecord owner,ObjectId layer,ObjectId type,Point3d start,Point3d end)
        {
            var line=new Line(start,end); line.SetDatabaseDefaults(db);
            owner.AppendEntity(line); tr.AddNewlyCreatedDBObject(line,true);
            // Set foreign-key properties only after this entity belongs to the target database.
            RequireDatabase(line.ObjectId,db,"Đường mẫu");
            line.LayerId=layer; line.LinetypeId=type; line.LinetypeScale=1;
            line.Color=Color.FromColorIndex(ColorMethod.ByLayer,256);
        }
    }
}
