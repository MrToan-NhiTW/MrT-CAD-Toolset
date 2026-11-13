using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MedGas2024.Core;

namespace MedGas2024.Engine
{
    public sealed class JoinRoute
    {
        public ObjectId Id; public bool IsLine, Flatten; public string Style, Handle;
        public double MinZ,MaxZ; public string OwnerGroup; public List<JoinVertex> Points=new List<JoinVertex>();
    }
    public sealed class JoinPlan
    {
        public readonly List<JoinRoute> Routes=new List<JoinRoute>();
        public readonly List<List<Tuple<int,bool>>> Chains=new List<List<Tuple<int,bool>>>();
        public readonly Dictionary<string,int> Skipped=new Dictionary<string,int>();
        public readonly List<string> Diagnostics=new List<string>();
        public int Selected,Flattened,Links,StyleMerges; public double MaxGap,MaxZ;
        public void Skip(string s){int n;Skipped.TryGetValue(s,out n);Skipped[s]=n+1;}
        public string Summary(){var s=new StringBuilder();s.AppendLine("Hỗ trợ LINE và LWPOLYLINE hở; giữ cao độ, chỉ nối cùng Z. Khe XY lớn nhất được nối: "+MaxGap.ToString("0.#########")+".");if(StyleMerges>0)s.AppendLine("Có "+StyleMerges+" tuyến nối các đường khác thuộc tính: dùng Layer/màu/linetype của đường đầu tuyến.");foreach(var kv in Skipped)s.AppendLine("Bỏ qua "+kv.Value+": "+kv.Key);foreach(var detail in Diagnostics)s.AppendLine(detail);s.AppendLine("Nhánh mơ hồ không tự nối. Khe hở được nối bằng đoạn thẳng; đảo tuyến có thể đổi chiều chữ R.");return s.ToString();}
    }
    public static class QuickJoinService
    {
        static string Style(Entity e){var p=e as Polyline;var t=e.Transparency;return e.OwnerId.ToString()+"|"+e.LayerId.ToString()+"|"+e.LinetypeId.ToString()+"|"+e.Color.ToString()+"|"+e.LineWeight+"|"+e.LinetypeScale.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+t.IsByLayer+"|"+t.IsByBlock+"|"+t.IsByAlpha+"|"+(t.IsByAlpha?t.Alpha:0)+"|"+e.Visible+"|"+e.MaterialId.ToString()+"|"+e.PlotStyleName+"|"+(p!=null&&p.Plinegen);}
        static bool HasMetadata(Entity e){
            using(var x=e.XData){if(x!=null){var v=x.AsArray();if(!JoinMetadataPolicy.IsSupported(v.Select(t=>(int)t.TypeCode).ToArray(),v.Select(t=>Convert.ToString(t.Value)).ToArray()))return true;}}
            if(!e.ExtensionDictionary.IsNull)return true;
            var m=e.GetType().GetMethod("GetPersistentReactorIds",Type.EmptyTypes);
            if(m==null)return true; // Cannot prove absence of persistent links: fail closed.
            var ids=m.Invoke(e,null) as ObjectIdCollection;if(ids!=null&&ids.Count>0)return true;
            return false;
        }
        static bool NormalOk(Vector3d n){return Math.Abs(n.X)<1e-9&&Math.Abs(n.Y)<1e-9&&Math.Abs(n.Z-1)<1e-9;}
        public static JoinPlan Inspect(Database db,ObjectId[] ids,double tolerance,bool zero)
        {
            if(ids.Length>2000)throw new InvalidOperationException("Chọn tối đa 2.000 đối tượng mỗi lần để kiểm tra chính xác; hãy chia vùng bản vẽ.");
            var plan=new JoinPlan{Selected=ids.Length};using(var tr=db.TransactionManager.StartOpenCloseTransaction())foreach(var id in ids.Distinct()){
                if(id.IsNull||!id.IsValid||id.IsErased){plan.Skip("Đối tượng không còn tồn tại");continue;}
                var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null){plan.Skip("Không phải đối tượng hình học");continue;}
                var layer=tr.GetObject(e.LayerId,OpenMode.ForRead) as LayerTableRecord;
                if(layer==null||layer.IsLocked||layer.IsDependent){plan.Skip("Layer khóa hoặc phụ thuộc Xref");continue;}
                var line=e as Line;var pl=e as Polyline;
                if(line==null&&pl==null){plan.Skip("Không phải LINE/LWPOLYLINE");continue;}
                if(HasMetadata(e)){plan.Skip("Có dữ liệu mở rộng/liên kết, cần xử lý riêng");continue;}
                if(line!=null&&(!NormalOk(line.Normal)||Math.Abs(line.Thickness)>1e-9)){plan.Skip("LINE có normal nghiêng hoặc thickness");continue;}
                if(pl!=null&&(pl.Closed||pl.NumberOfVertices<2||!NormalOk(pl.Normal)||Math.Abs(pl.Thickness)>1e-9)){plan.Skip("Polyline kín, thiếu đỉnh, nghiêng hoặc có thickness");continue;}
                var r=new JoinRoute{Id=id,IsLine=line!=null,Style=Style(e),OwnerGroup=e.OwnerId.ToString(),Handle=e.Handle.ToString()};
                if(line!=null){var a=line.StartPoint;var b=line.EndPoint;r.MinZ=Math.Min(a.Z,b.Z);r.MaxZ=Math.Max(a.Z,b.Z);r.Points.Add(new JoinVertex{X=a.X,Y=a.Y});r.Points.Add(new JoinVertex{X=b.X,Y=b.Y});}
                else {r.MinZ=r.MaxZ=pl.Elevation;double? constant=null;try{constant=pl.ConstantWidth;}catch(Autodesk.AutoCAD.Runtime.Exception){}for(int i=0;i<pl.NumberOfVertices;i++){var p=pl.GetPoint2dAt(i);bool segment=i<pl.NumberOfVertices-1;r.Points.Add(new JoinVertex{X=p.X,Y=p.Y,Bulge=segment?pl.GetBulgeAt(i):0,StartWidth=segment?(constant??pl.GetStartWidthAt(i)):0,EndWidth=segment?(constant??pl.GetEndWidthAt(i)):0});}}
                bool degenerate=false;for(int i=0;i+1<r.Points.Count;i++){var a=r.Points[i];var b=r.Points[i+1];if(Math.Abs(a.X-b.X)<1e-10&&Math.Abs(a.Y-b.Y)<1e-10){degenerate=true;break;}}
                if(degenerate){plan.Skip("Có đoạn thu về chiều dài 0 trên mặt bằng");continue;}
                if(!zero&&Math.Abs(r.MaxZ-r.MinZ)>1e-9){plan.Skip("LINE dốc; dùng MRTZ0 trước nếu muốn đưa về Z=0");continue;}
                r.Flatten=zero&&(Math.Abs(r.MinZ)>1e-9||Math.Abs(r.MaxZ)>1e-9);if(r.Flatten)plan.Flattened++;plan.MaxZ=Math.Max(plan.MaxZ,Math.Max(Math.Abs(r.MinZ),Math.Abs(r.MaxZ)));plan.Routes.Add(r);
            }
            var endpoints=new List<JoinEndpoint>();for(int i=0;i<plan.Routes.Count;i++){var r=plan.Routes[i];var a=r.Points[0];var b=r.Points[r.Points.Count-1];double z=zero?0:r.MinZ;endpoints.Add(new JoinEndpoint{Route=i,X=a.X,Y=a.Y,Z=z,Style=r.OwnerGroup,Curved=Math.Abs(a.Bulge)>1e-12});endpoints.Add(new JoinEndpoint{Route=i,End=true,X=b.X,Y=b.Y,Z=z,Style=r.OwnerGroup,Curved=Math.Abs(r.Points[r.Points.Count-2].Bulge)>1e-12});}
            var links=JoinPlanner.Find(endpoints,tolerance);foreach(var link in links)plan.MaxGap=Math.Max(plan.MaxGap,link.Gap);
            var grouped=JoinPlanner.Chains(plan.Routes.Count,links);foreach(var chain in grouped.Chains){plan.Links+=chain.Count-1;if(chain.Select(x=>plan.Routes[x.Item1].Style).Distinct().Count()>1)plan.StyleMerges++;plan.Chains.Add(chain);}for(int i=0;i<grouped.Cycles;i++)plan.Skip("Tuyến khép vòng: không tự nối vòng");
            if(plan.Chains.Count>1){
                var group=new int[plan.Routes.Count];for(int i=0;i<plan.Chains.Count;i++)foreach(var item in plan.Chains[i])group[item.Item1]=i;
                for(int i=0;i<plan.Chains.Count&&plan.Diagnostics.Count<6;i++){
                    var chain=plan.Chains[i];var a=chain[0];var b=chain[chain.Count-1];var tips=new[]{a.Item1*2+(a.Item2?1:0),b.Item1*2+(b.Item2?0:1)};
                    foreach(int tip in tips){if(plan.Diagnostics.Count>=6)break;var p=endpoints[tip];int closest=-1;double min=Double.MaxValue;for(int j=0;j<endpoints.Count;j++){var q=endpoints[j];if(group[q.Route]==i||q.Style!=p.Style)continue;double dx=p.X-q.X,dy=p.Y-q.Y,d=Math.Sqrt(dx*dx+dy*dy);if(d<min){min=d;closest=j;}}
                        if(closest<0)continue;var q2=endpoints[closest];double dz=Math.Abs(p.Z-q2.Z);string reason=min>tolerance?"quá khe cho phép":dz>1e-9?"khác cao độ":"cần kiểm tra các đầu mút trùng/gần hoặc vòng khép";
                        plan.Diagnostics.Add("Đầu "+plan.Routes[p.Route].Handle+" gần đầu "+plan.Routes[q2.Route].Handle+": XY="+min.ToString("0.#########")+", lệch Z="+dz.ToString("0.#########")+" — "+reason+".");
                    }
                }
            }
            return plan;
        }
        public static Dictionary<ObjectId,ObjectId[]> Apply(Database db,JoinPlan plan,bool zero)
        {
            var replacements=new Dictionary<ObjectId,ObjectId[]>();using(var tr=db.TransactionManager.StartTransaction()){
                foreach(var chain in plan.Chains){var first=plan.Routes[chain[0].Item1];if(chain.Count==1&&!first.IsLine&&!first.Flatten)continue;
                    var source=(Entity)tr.GetObject(first.Id,OpenMode.ForRead);var owner=(BlockTableRecord)tr.GetObject(source.OwnerId,OpenMode.ForWrite);var points=new List<JoinVertex>();
                    foreach(var part in chain){var r=plan.Routes[part.Item1];JoinGeometry.Append(points,JoinGeometry.Ordered(r.Points,part.Item2));}
                    using(var output=new Polyline()){output.SetDatabaseDefaults(db);output.SetPropertiesFrom(source);output.Normal=Vector3d.ZAxis;output.Elevation=zero?0:first.MinZ;for(int i=0;i<points.Count;i++){var v=points[i];output.AddVertexAt(i,new Point2d(v.X,v.Y),v.Bulge,v.StartWidth,v.EndWidth);}var sourcePl=source as Polyline;if(sourcePl!=null)output.Plinegen=sourcePl.Plinegen;owner.AppendEntity(output);tr.AddNewlyCreatedDBObject(output,true);
                        // All accepted nonempty metadata is the same exact empty ETag; preserve
                        // one copy even when the first input had no XData.
                        foreach(var part in chain){var item=(Entity)tr.GetObject(plan.Routes[part.Item1].Id,OpenMode.ForRead);using(var x=item.XData){if(x!=null){using(var copy=new ResultBuffer(x.AsArray()))output.XData=copy;break;}}}
                        foreach(var part in chain){var r=plan.Routes[part.Item1];var old=(Entity)tr.GetObject(r.Id,OpenMode.ForWrite);old.Erase();replacements[r.Id]=new[]{output.ObjectId};}}
                }tr.Commit();}return replacements;
        }
    }
}
