using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Engine;
[assembly:CommandClass(typeof(NativeJoinDirection2005))]
public static class NativeJoinDirection2005
{
    [CommandMethod("MRT_JOIN_R_2005_TEST",CommandFlags.Modal)]
    public static void Run()
    {
        string root=Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName;
        var log=new List<string>();var old=HostApplicationServices.WorkingDatabase;
        try{
            string lin=Path.Combine(root,"evidence","join-r-2005.lin");
            File.WriteAllText(lin,"*TEST_JOIN_R,relative\r\nA,4,-3,[\"D15.VAC\",STANDARD,S=1,R=0],-3\r\n",Encoding.ASCII);
            using(var db=new Database(true,true)){
                HostApplicationServices.WorkingDatabase=db;db.LoadLineTypeFile("TEST_JOIN_R",lin);
                ObjectId[] source=new ObjectId[3];
                using(var tr=db.TransactionManager.StartTransaction()){
                    var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
                    var space=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
                    var lt=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);
                    var apps=(RegAppTable)tr.GetObject(db.RegAppTableId,OpenMode.ForRead);
                    if(!apps.Has("AcDbBlockRepETag")){apps.UpgradeOpen();var reg=new RegAppTableRecord{Name="AcDbBlockRepETag"};apps.Add(reg);tr.AddNewlyCreatedDBObject(reg,true);}
                    var points=new[]{new Point2d(0,0),new Point2d(100,0),new Point2d(100,100),new Point2d(200,100)};
                    for(int i=0;i<3;i++){
                        var p=new Polyline();p.AddVertexAt(0,points[i],0,2+i*2,4+i*2);p.AddVertexAt(1,points[i+1],0,0,0);
                        space.AppendEntity(p);tr.AddNewlyCreatedDBObject(p,true);p.LinetypeId=lt["TEST_JOIN_R"];
                        using(var x=new ResultBuffer(new TypedValue(1001,"AcDbBlockRepETag"),new TypedValue(1000,"")))p.XData=x;
                        source[i]=p.ObjectId;
                    }tr.Commit();
                }
                var jp=QuickJoinService.Inspect(db,source,.5,false);
                if(jp.Links!=2||jp.Chains.Count!=1||jp.Chains[0].Count!=3)throw new System.Exception("JOIN inspect: "+jp.Summary());
                var replacements=QuickJoinService.Apply(db,jp,false);if(replacements.Count!=3)throw new System.Exception("JOIN replacements: "+replacements.Count);
                var joined=replacements[source[0]][0];var rp=NativeDirectionService.Inspect(db,new[]{joined});
                if(rp.Items.Count!=1||rp.Items[0].Sign!=0)throw new System.Exception("R must offer split: "+rp.Summary);
                var parts=new Dictionary<ObjectId,ObjectId[]>();if(NativeDirectionService.Apply(db,rp,true,parts)!=1||!parts.ContainsKey(joined))throw new System.Exception("R split failed");
                var result=parts[joined];var after=NativeDirectionService.Inspect(db,result);
                if(after.Items.Count!=result.Length||after.Items.Any(i=>i.Sign!=1)||after.Skipped.Count!=0)throw new System.Exception("R direction after split: "+after.Summary);
                var expected=new Dictionary<string,double>{{"0,0",2},{"100,0",4},{"100,100",6},{"200,100",8}};
                using(var tr=db.TransactionManager.StartOpenCloseTransaction())foreach(var id in result){
                    var p=(Polyline)tr.GetObject(id,OpenMode.ForRead);using(var x=p.XData){if(x==null||x.AsArray().Length!=2||(string)x.AsArray()[0].Value!="AcDbBlockRepETag")throw new System.Exception("ETag not retained");}
                    for(int i=0;i<p.NumberOfVertices-1;i++){var a=p.GetPoint2dAt(i);var b=p.GetPoint2dAt(i+1);string ka=a.X+","+a.Y,kb=b.X+","+b.Y;
                        if(Math.Abs(p.GetStartWidthAt(i)-expected[ka])>1e-8||Math.Abs(p.GetEndWidthAt(i)-expected[kb])>1e-8)throw new System.Exception("Width changed at "+ka+" → "+kb);}
                }
                log.Add("PASS: join 3 tagged tapered parts, split/reverse R, retain geometry/widths and XData.");
                ObjectId unknown;
                using(var tr=db.TransactionManager.StartTransaction()){
                    var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var space=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
                    var lt=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);var apps=(RegAppTable)tr.GetObject(db.RegAppTableId,OpenMode.ForWrite);
                    var reg=new RegAppTableRecord{Name="MRT_TEST_UNKNOWN"};apps.Add(reg);tr.AddNewlyCreatedDBObject(reg,true);
                    var p=new Polyline();p.AddVertexAt(0,new Point2d(0,0),0,0,0);p.AddVertexAt(1,new Point2d(100,0),0,0,0);p.AddVertexAt(2,new Point2d(100,100),0,0,0);
                    space.AppendEntity(p);tr.AddNewlyCreatedDBObject(p,true);p.LinetypeId=lt["TEST_JOIN_R"];using(var x=new ResultBuffer(new TypedValue(1001,"MRT_TEST_UNKNOWN"),new TypedValue(1000,"payload")))p.XData=x;unknown=p.ObjectId;tr.Commit();
                }
                var rejected=NativeDirectionService.Inspect(db,new[]{unknown});
                if(rejected.Items.Count!=0||rejected.Skipped.Count!=1)throw new System.Exception("Unknown metadata was not rejected");
                log.Add("PASS: unknown application XData is not split.");
            }
            log.Add("RESULT: PASS");
        }catch(System.Exception ex){log.Add("RESULT: FAIL");log.Add(ex.ToString());}
        finally{HostApplicationServices.WorkingDatabase=old;File.WriteAllLines(Path.Combine(root,"evidence","join-r-2005-native.txt"),log);var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",log.ToArray())+"\n");}
    }
}
