using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
namespace MedGas2024.Engine
{
    // MRTZ0PL: put linear pipe geometry on Z=0 and optionally turn LINEs into LWPOLYLINEs.
    // Lines whose two ends had different Z looked joined in plan but were skew in 3D, so
    // JOIN/OSNAP/direction tools failed or snapped to the wrong place. Everything here works
    // on WCS XY: points are projected straight down (X,Y kept, Z dropped).
    public sealed class FlattenPlan
    {
        public readonly List<ObjectId> Lines=new List<ObjectId>();          // LINE with any Z ≠ 0
        public readonly List<ObjectId> FlatLines=new List<ObjectId>();      // LINE already on Z=0
        public readonly List<ObjectId> Polylines=new List<ObjectId>();      // LWPOLYLINE with elevation ≠ 0 or flipped normal
        public readonly List<ObjectId> Polylines3d=new List<ObjectId>();    // 3D polyline → LWPOLYLINE
        public readonly List<ObjectId> Polylines2d=new List<ObjectId>();    // old-style 2D polyline off Z=0
        public readonly List<string> Skipped=new List<string>();
        public int AlreadyFlat;
        public int Changes(bool convert){return Lines.Count+Polylines.Count+Polylines3d.Count+Polylines2d.Count+(convert?FlatLines.Count:0);}
        public string Summary(bool convert)
        {
            var s="Đưa về Z=0: LINE lệch Z "+Lines.Count+"; polyline có cao độ/lật pháp tuyến "+Polylines.Count+"; polyline 3D "+Polylines3d.Count+"; polyline 2D kiểu cũ "+Polylines2d.Count+".";
            if(convert)s+="\r\nĐổi LINE → polyline: "+(Lines.Count+FlatLines.Count)+" LINE (giữ handle, Layer, màu, nét, scale, lineweight, XData).";
            s+="\r\nĐã phẳng, giữ nguyên: "+AlreadyFlat+"; bỏ qua: "+Skipped.Count+".";
            s+="\r\nChiếu thẳng xuống mặt phẳng XY của WCS: giữ X,Y, bỏ Z. Polyline 3D thành LWPOLYLINE thẳng. Không nối các đường (dùng MRTJOIN sau).";
            var reasons=Skipped.Select(x=>{int i=x.IndexOf(": ");return i<0?x:x.Substring(i+2);}).GroupBy(x=>x).OrderByDescending(g=>g.Count()).Take(8).Select(g=>"• "+g.Count()+" × "+g.Key).ToList();
            if(reasons.Count>0)s+="\r\n"+String.Join("\r\n",reasons);
            return s;
        }
    }
    public static class FlattenService
    {
        const double Tolerance=1e-9;
        static bool Flat(double z){return Math.Abs(z)<=Tolerance;}
        static bool UpNormal(Vector3d n){return Math.Abs(n.X)<=1e-12&&Math.Abs(n.Y)<=1e-12&&n.Z>0;}
        static bool Linked(Entity e){return !e.ExtensionDictionary.IsNull||e.GetPersistentReactorIds().Count>0;}
        public static FlattenPlan Inspect(Database db,IEnumerable<ObjectId> ids)
        {
            var plan=new FlattenPlan();
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            foreach(var id in ids.Where(x=>!x.IsNull&&x.IsValid&&!x.IsErased).Distinct())
            {
                var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)continue;
                if(!(e is Line||e is Polyline||e is Polyline3d||e is Polyline2d))continue;
                var owner=tr.GetObject(e.OwnerId,OpenMode.ForRead) as BlockTableRecord;var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);
                string tag=id.Handle+": ";
                if(owner==null||!owner.IsLayout){plan.Skipped.Add(tag+"nằm trong block");continue;}
                if(layer.IsLocked||layer.IsDependent){plan.Skipped.Add(tag+"Layer khóa/Xref");continue;}
                var line=e as Line;
                if(line!=null)
                {
                    var a=line.StartPoint;var b=line.EndPoint;
                    if(new Point2d(a.X,a.Y).GetDistanceTo(new Point2d(b.X,b.Y))<=Tolerance){plan.Skipped.Add(tag+"LINE đứng theo Z (chiếu xuống thành một điểm)");continue;}
                    if(Flat(a.Z)&&Flat(b.Z)&&UpNormal(line.Normal))plan.FlatLines.Add(id);else plan.Lines.Add(id);continue;
                }
                var pl=e as Polyline;
                if(pl!=null){if(Flat(pl.Elevation)&&UpNormal(pl.Normal)){plan.AlreadyFlat++;continue;}if(pl.NumberOfVertices<2){plan.Skipped.Add(tag+"polyline thiếu đỉnh");continue;}if(Linked(pl)&&!UpNormal(pl.Normal)){plan.Skipped.Add(tag+"polyline lật pháp tuyến có dữ liệu liên kết");continue;}plan.Polylines.Add(id);continue;}
                var p3=e as Polyline3d;
                if(p3!=null){if(p3.PolyType!=Poly3dType.SimplePoly){plan.Skipped.Add(tag+"polyline 3D dạng spline");continue;}if(Linked(p3)){plan.Skipped.Add(tag+"polyline 3D có dữ liệu liên kết");continue;}plan.Polylines3d.Add(id);continue;}
                var p2=(Polyline2d)e;
                if(Flat(p2.Elevation)&&UpNormal(p2.Normal)){plan.AlreadyFlat++;continue;}
                if(p2.PolyType!=Poly2dType.SimplePoly){plan.Skipped.Add(tag+"polyline 2D dạng fit/spline");continue;}if(Linked(p2)){plan.Skipped.Add(tag+"polyline 2D có dữ liệu liên kết");continue;}
                plan.Polylines2d.Add(id);
            }
            return plan;
        }
        static void CopyLook(Entity from,Entity to)
        {
            to.SetPropertiesFrom(from);
            using(var data=from.XData){if(data!=null)using(var copy=new ResultBuffer(data.AsArray()))to.XData=copy;}
        }
        // HandOverTo keeps the original ObjectId/handle, so legend tables, isolate sessions
        // and anything else that remembers the pipe by handle keep pointing at it.
        static void Replace(Transaction tr,Entity old,Polyline fresh)
        {
            fresh.SetPropertiesFrom(old);old.UpgradeOpen();old.HandOverTo(fresh,true,true);tr.AddNewlyCreatedDBObject(fresh,true);old.Dispose();
        }
        // Old-style 2D/3D polylines own vertex sub-objects, so they are not handed over:
        // the new LWPOLYLINE is appended to the same space and the original is erased.
        static void ReplaceComplex(Transaction tr,Entity old,Polyline fresh)
        {
            CopyLook(old,fresh);var owner=(BlockTableRecord)tr.GetObject(old.OwnerId,OpenMode.ForWrite);owner.AppendEntity(fresh);tr.AddNewlyCreatedDBObject(fresh,true);old.UpgradeOpen();old.Erase();
        }
        static Polyline FromPoints(IList<Point3d> points,IList<double> bulges,bool closed)
        {
            var p=new Polyline();p.Normal=Vector3d.ZAxis;p.Elevation=0;int n=0;
            for(int i=0;i<points.Count;i++){var q=new Point2d(points[i].X,points[i].Y);if(n>0&&q.GetDistanceTo(p.GetPoint2dAt(n-1))<=Tolerance)continue;p.AddVertexAt(n,q,bulges==null?0:bulges[i],0,0);n++;}
            if(n<2)throw new InvalidOperationException("sau khi chiếu xuống chỉ còn một điểm");
            p.Closed=closed;return p;
        }
        public static int Apply(Database db,FlattenPlan plan,bool convertLines)
        {
            int count=0;using(var tr=db.TransactionManager.StartTransaction())
            {
                foreach(var id in plan.Lines.Concat(convertLines?plan.FlatLines:Enumerable.Empty<ObjectId>()))
                {
                    var line=(Line)tr.GetObject(id,OpenMode.ForRead);
                    if(convertLines)Replace(tr,line,FromPoints(new[]{line.StartPoint,line.EndPoint},null,false));
                    else{line.UpgradeOpen();var a=line.StartPoint;var b=line.EndPoint;line.Normal=Vector3d.ZAxis;line.StartPoint=new Point3d(a.X,a.Y,0);line.EndPoint=new Point3d(b.X,b.Y,0);line.Thickness=0;}
                    count++;
                }
                foreach(var id in plan.Polylines)
                {
                    var pl=(Polyline)tr.GetObject(id,OpenMode.ForWrite);
                    if(UpNormal(pl.Normal)){pl.Elevation=0;count++;continue;}
                    // Flipped (mirrored) or tilted polylines: rebuild from WCS vertices. A flipped
                    // normal reverses arc orientation, so bulges change sign; tilted arcs cannot be
                    // projected exactly and become straight chords.
                    bool flipped=Math.Abs(pl.Normal.X)<=1e-12&&Math.Abs(pl.Normal.Y)<=1e-12&&pl.Normal.Z<0;
                    var points=new List<Point3d>();var bulges=new List<double>();var widths=new List<Tuple<double,double>>();
                    for(int i=0;i<pl.NumberOfVertices;i++){points.Add(pl.GetPoint3dAt(i));bulges.Add(flipped?-pl.GetBulgeAt(i):0);widths.Add(Tuple.Create(pl.GetStartWidthAt(i),pl.GetEndWidthAt(i)));}
                    var fresh=FromPoints(points,bulges,pl.Closed);if(fresh.NumberOfVertices==points.Count)for(int i=0;i<points.Count;i++){fresh.SetStartWidthAt(i,widths[i].Item1);fresh.SetEndWidthAt(i,widths[i].Item2);}
                    fresh.Plinegen=pl.Plinegen;Replace(tr,pl,fresh);count++;
                }
                foreach(var id in plan.Polylines3d)
                {
                    var p3=(Polyline3d)tr.GetObject(id,OpenMode.ForRead);var points=new List<Point3d>();
                    foreach(ObjectId v in p3){var vertex=(PolylineVertex3d)tr.GetObject(v,OpenMode.ForRead);points.Add(vertex.Position);}
                    ReplaceComplex(tr,p3,FromPoints(points,null,p3.Closed));count++;
                }
                foreach(var id in plan.Polylines2d)
                {
                    var p2=(Polyline2d)tr.GetObject(id,OpenMode.ForRead);bool flipped=p2.Normal.Z<0;var points=new List<Point3d>();var bulges=new List<double>();
                    foreach(ObjectId v in p2){var vertex=(Vertex2d)tr.GetObject(v,OpenMode.ForRead);points.Add(p2.VertexPosition(vertex));bulges.Add(UpNormal(p2.Normal)?vertex.Bulge:flipped?-vertex.Bulge:0);}
                    var fresh=FromPoints(points,bulges,p2.Closed);fresh.Plinegen=p2.LinetypeGenerationOn;ReplaceComplex(tr,p2,fresh);count++;
                }
                tr.Commit();
            }
            return count;
        }
    }
}
