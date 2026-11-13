using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Engine;
[assembly:CommandClass(typeof(NativeRProbeRuntime))]
public static class NativeRProbeRuntime
{
    [CommandMethod("MRT_RPROBE_TEST",CommandFlags.Modal)] public static void Run()
    {
        string root=Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName;var log=new List<string>();string input=Path.Combine(root,"evidence","rprobe-input.lin");var old=HostApplicationServices.WorkingDatabase;
        try{File.WriteAllText(input,"*TEST_R,relative\r\nA,4,-3,[\"D15.VAC\",STANDARD,S=1,R=0],-3\r\n*TEST_U,upright\r\nA,4,-3,[\"D15.VAC\",STANDARD,S=1,U=0],-3\r\n*TEST_A,absolute\r\nA,4,-3,[\"D15.VAC\",STANDARD,S=1,A=0],-3\r\n",Encoding.ASCII);
            using(var db=new Database(true,true)){HostApplicationServices.WorkingDatabase=db;foreach(string name in new[]{"TEST_R","TEST_U","TEST_A"})db.LoadLineTypeFile(name,input);ObjectId[] ids;using(var t=db.TransactionManager.StartOpenCloseTransaction()){var l=(LinetypeTable)t.GetObject(db.LinetypeTableId,OpenMode.ForRead);ids=new[]{l["TEST_R"],l["TEST_U"],l["TEST_A"]};}var flags=RelativeTypeProbe.Read(db,ids);if(!flags.ContainsKey(ids[0])||!flags[ids[0]]||!flags.ContainsKey(ids[1])||flags[ids[1]]||!flags.ContainsKey(ids[2])||flags[ids[2]])throw new System.Exception("Native R/U/A flags not distinguished.");log.Add("PASS: R true; U and A false");
                ObjectId route;using(var tr=db.TransactionManager.StartTransaction()){var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var p=new Polyline();p.AddVertexAt(0,new Autodesk.AutoCAD.Geometry.Point2d(0,0),0,0,0);p.AddVertexAt(1,new Autodesk.AutoCAD.Geometry.Point2d(-100,0),0,0,0);p.AddVertexAt(2,new Autodesk.AutoCAD.Geometry.Point2d(-100,-100),0,0,0);p.AddVertexAt(3,new Autodesk.AutoCAD.Geometry.Point2d(50,-100),0,0,0);model.AppendEntity(p);tr.AddNewlyCreatedDBObject(p,true);p.LinetypeId=ids[0];route=p.ObjectId;tr.Commit();}
                var plan=NativeDirectionService.Inspect(db,new[]{route});if(plan.Items.Count!=1||plan.Items[0].Sign!=0)throw new System.Exception("Mixed route was not recognized");if(NativeDirectionService.Apply(db,plan,false)!=0||route.IsErased)throw new System.Exception("No-split mode altered route");if(NativeDirectionService.Apply(db,plan,true)!=1||!route.IsErased)throw new System.Exception("Explicit split failed");
                ObjectId[] newIds;using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);newIds=model.Cast<ObjectId>().ToArray();}var after=NativeDirectionService.Inspect(db,newIds);if(newIds.Length!=2||after.Items.Count!=2||after.Items.Any(i=>i.Sign!=1)||after.Skipped.Count!=0)throw new System.Exception("Split segments not all in required direction");log.Add("PASS: unmarked native R detected; optional split preserves direction groups");}
            log.Add("RESULT: PASS");}catch(System.Exception e){log.Add("RESULT: FAIL");log.Add(e.ToString());}finally{HostApplicationServices.WorkingDatabase=old;File.WriteAllLines(Path.Combine(root,"evidence","rprobe-runtime.txt"),log);var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",log)+"\n");}
    }
}
