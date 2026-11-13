using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using MedGas2024.Core;
namespace MedGas2024.Engine
{
    public sealed class ScalePlan
    {
        public readonly Dictionary<ObjectId,string> States=new Dictionary<ObjectId,string>();
        public int Skipped,UnsupportedWidth,Unchanged;public double BeforeMin=Double.MaxValue,BeforeMax=Double.MinValue;
        public string OptionsSignature;
        public string Summary(ScaleOptions o){return "Sẽ xử lý "+States.Count+" đường; bỏ qua "+Skipped+" đối tượng/Layer khóa. Đã đúng: "+Unchanged+".\r\nScale hiện tại: "+(States.Count==0?"—":Profile.N(BeforeMin)+"…"+Profile.N(BeforeMax))+"\r\nScale: "+(o.ApplyScale?"đặt = "+Profile.N(o.EntityScale):"giữ nguyên")+"\r\nGlobal Width: "+(o.ApplyWidth?"đặt = "+Profile.N(o.GlobalWidth)+" đơn vị DWG (chuẩn hóa mọi đoạn polyline)":"giữ nguyên")+"\r\n"+(o.ApplyWidth?UnsupportedWidth+" đường không có Width: chỉ xử lý Scale nếu đã chọn.\r\n":"")+"Giữ nguyên Layer, Linetype, màu, lineweight và tọa độ. Không sửa LTSCALE/MSLTSCALE/PSLTSCALE.\r\nGeneration Disabled trên polyline xử lý; PLINEGEN=0 cho đường vẽ tiếp.\r\nSau đổi Scale/Width trên tuyến đã có nhãn liên kết, chạy lại MRTFIXTEXT.\r\nPhạm vi theo Layer chỉ gồm đường trực tiếp trong Model/Layout, không sửa bên trong block/Xref.";}
    }
    public static class ScaleService
    {
        public static bool GenerationOn(Entity e){var p=e as Polyline;if(p!=null)return p.Plinegen;var old=e as Polyline2d;return old!=null&&old.LinetypeGenerationOn;}
        public static void DisableGeneration(Entity e){var p=e as Polyline;if(p!=null)p.Plinegen=false;var old=e as Polyline2d;if(old!=null)old.LinetypeGenerationOn=false;}
        private static bool Curve(Entity e){return e is Line||e is Polyline||e is Polyline2d||e is Polyline3d||e is Arc||e is Spline;}
        public static bool HasWidth(Entity e){return e is Polyline||e is Polyline2d;}
        public static string WidthState(Entity e,Transaction tr)
        {
            var values=new List<string>();var p=e as Polyline;if(p!=null){for(int i=0;i<p.NumberOfVertices;i++)values.Add(Profile.N(p.GetStartWidthAt(i))+":"+Profile.N(p.GetEndWidthAt(i)));return String.Join(",",values);}
            var old=e as Polyline2d;if(old!=null){values.Add(Profile.N(old.DefaultStartWidth)+":"+Profile.N(old.DefaultEndWidth));foreach(ObjectId id in old){var v=(Vertex2d)tr.GetObject(id,OpenMode.ForRead);values.Add(Profile.N(v.StartWidth)+":"+Profile.N(v.EndWidth));}return String.Join(",",values);}return "n/a";
        }
        public static void SetWidth(Entity e,Transaction tr,double width)
        {
            var p=e as Polyline;if(p!=null){p.ConstantWidth=width;return;}var old=e as Polyline2d;if(old!=null){old.DefaultStartWidth=old.DefaultEndWidth=width;foreach(ObjectId id in old){var v=(Vertex2d)tr.GetObject(id,OpenMode.ForWrite);v.StartWidth=v.EndWidth=width;}}
        }
        public static bool WidthMatches(Entity e,Transaction tr,double width)
        {
            if(!HasWidth(e))return true;var p=e as Polyline;if(p!=null){for(int i=0;i<p.NumberOfVertices;i++)if(p.GetStartWidthAt(i)!=width||p.GetEndWidthAt(i)!=width)return false;return true;}var old=(Polyline2d)e;if(old.DefaultStartWidth!=width||old.DefaultEndWidth!=width)return false;foreach(ObjectId id in old){var v=(Vertex2d)tr.GetObject(id,OpenMode.ForRead);if(v.StartWidth!=width||v.EndWidth!=width)return false;}return true;
        }
        private static string State(Entity e,Transaction tr){return e.OwnerId.Handle+"|"+e.LayerId.Handle+"|"+Profile.N(e.LinetypeScale)+"|"+GenerationOn(e)+"|"+WidthState(e,tr);}
        public static ObjectId[] OnLayers(Database db,IEnumerable<string> names)
        {
            var selected=new HashSet<string>(names,StringComparer.OrdinalIgnoreCase);if(selected.Count==0)throw new ArgumentException("Chọn ít nhất một Layer.");var ids=new List<ObjectId>();using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);foreach(ObjectId b in bt){var space=(BlockTableRecord)tr.GetObject(b,OpenMode.ForRead);if(!space.IsLayout||space.IsFromExternalReference||space.IsFromOverlayReference)continue;foreach(ObjectId id in space){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e!=null&&selected.Contains(e.Layer)&&!DrawingSearch.Preview(e.Layer))ids.Add(id);}}}return ids.ToArray();
        }
        public static ScalePlan Inspect(Database db,IEnumerable<ObjectId> ids,ScaleOptions options)
        {
            options.Validate();var plan=new ScalePlan{OptionsSignature=options.Signature};using(var tr=db.TransactionManager.StartOpenCloseTransaction())foreach(ObjectId id in ids.Distinct())
            {
                if(id.IsNull||!id.IsValid||id.IsErased||id.Database.UnmanagedObject!=db.UnmanagedObject){plan.Skipped++;continue;}
                var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||!Curve(e)||DrawingSearch.Preview(e.Layer)){plan.Skipped++;continue;}
                var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);var owner=tr.GetObject(e.OwnerId,OpenMode.ForRead) as BlockTableRecord;if(layer.IsLocked||layer.IsDependent||owner==null||!owner.IsLayout){plan.Skipped++;continue;}
                if(!HasWidth(e)){plan.UnsupportedWidth++;if(!options.ApplyScale){plan.Skipped++;continue;}}plan.BeforeMin=Math.Min(plan.BeforeMin,e.LinetypeScale);plan.BeforeMax=Math.Max(plan.BeforeMax,e.LinetypeScale);if(!GenerationOn(e)&&(!options.ApplyScale||e.LinetypeScale==options.EntityScale)&&(!options.ApplyWidth||WidthMatches(e,tr,options.GlobalWidth)))plan.Unchanged++;plan.States.Add(id,State(e,tr));
            }return plan;
        }
        public static int Apply(Database db,ScalePlan plan,ScaleOptions options,bool injectFailure=false)
        {
            options.Validate();if(options.Signature!=plan.OptionsSignature)throw new InvalidOperationException("Thông số đã đổi sau khi xem kế hoạch.");int count=0;using(var tr=db.TransactionManager.StartTransaction())
            {
                foreach(var pair in plan.States)
                {
                    if(pair.Key.IsErased)throw new InvalidOperationException("Đối tượng đã bị xóa. Kiểm tra lại.");var e=(Entity)tr.GetObject(pair.Key,OpenMode.ForRead);var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);if(layer.IsLocked||State(e,tr)!=pair.Value)throw new InvalidOperationException("Đối tượng/Layer đã đổi sau khi kiểm tra. Chưa đồng bộ.");
                    bool changed=GenerationOn(e)||options.ApplyScale&&e.LinetypeScale!=options.EntityScale||options.ApplyWidth&&!WidthMatches(e,tr,options.GlobalWidth);if(!changed)continue;e.UpgradeOpen();DisableGeneration(e);if(options.ApplyScale)e.LinetypeScale=options.EntityScale;if(options.ApplyWidth&&HasWidth(e))SetWidth(e,tr,options.GlobalWidth);count++;if(injectFailure)throw new InvalidOperationException("Injected scale rollback.");
                }tr.Commit();
            }if(plan.States.Count>0)db.Plinegen=false;return count;
        }
    }
}
