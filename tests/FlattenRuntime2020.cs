using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Engine;

[assembly: CommandClass(typeof(FlattenRuntime2020))]
public static class FlattenRuntime2020
{
    static void Check(bool condition, string message)
    {
        if(!condition) throw new InvalidOperationException(message);
    }

    [CommandMethod("MRTZ0PL2020TEST")]
    public static void Test()
    {
        using(var db=new Database(true,true))
        {
            ObjectId arc,legacy,vertical;
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);
                var tilted=new Polyline();
                tilted.AddVertexAt(0,new Point2d(0,0),0.5,2,3);
                tilted.AddVertexAt(1,new Point2d(20,0),0,0,0);
                tilted.Normal=new Vector3d(0,1,1).GetNormal();
                arc=space.AppendEntity(tilted);
                tr.AddNewlyCreatedDBObject(tilted,true);

                var old=new Polyline2d();
                legacy=space.AppendEntity(old);
                tr.AddNewlyCreatedDBObject(old,true);
                old.Elevation=5;
                foreach(int i in new[]{0,1,2})
                {
                    var vertex=new Vertex2d(new Point3d(10*i,0,0),i==0?0.25:0,2+i,3+i,0);
                    old.AppendVertex(vertex);
                    tr.AddNewlyCreatedDBObject(vertex,true);
                }
                var points=new Point3dCollection(new[]{Point3d.Origin,new Point3d(0,0,10)});
                var collapsed=new Polyline3d(Poly3dType.SimplePoly,points,false);
                vertical=space.AppendEntity(collapsed);
                tr.AddNewlyCreatedDBObject(collapsed,true);
                tr.Commit();
            }
            var plan=FlattenService.Inspect(db,new[]{arc,legacy,vertical});
            Check(plan.Changes(false)==1&&plan.Skipped.Count==2,"Tilted arc and collapsed route must be skipped.");
            Dictionary<ObjectId,ObjectId[]> mapping;
            Check(FlattenService.Apply(db,plan,false,out mapping)==1,"Exactly one polyline should change.");
            Check(mapping.ContainsKey(legacy)&&legacy.IsErased&&!arc.IsErased&&!vertical.IsErased,"Replacement identity mismatch.");
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var pl=(Polyline)tr.GetObject(mapping[legacy].Single(),OpenMode.ForRead);
                Check(pl.Elevation==0&&pl.GetBulgeAt(0)==0.25,"Elevation or bulge mismatch.");
                for(int i=0;i<3;i++)
                    Check(pl.GetStartWidthAt(i)==2+i&&pl.GetEndWidthAt(i)==3+i,"Variable width mismatch.");
            }
        }
        Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\\nMRTZ0PL2020TEST PASS\\n");
    }
}
