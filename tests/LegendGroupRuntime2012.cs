using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;
[assembly:CommandClass(typeof(LegendGroupRuntime2012))]
public static class LegendGroupRuntime2012
{
    static void Check(bool ok,string message){if(!ok)throw new System.Exception(message);}
    static Tuple<ObjectId,ObjectId> MaskGroup(Database db,ObjectId table)
    {
        using(var tr=db.TransactionManager.StartOpenCloseTransaction()){
            var groups=(DBDictionary)tr.GetObject(db.GroupDictionaryId,OpenMode.ForRead);
            var result=new List<Tuple<ObjectId,ObjectId>>();
            foreach(DBDictionaryEntry e in groups){var g=tr.GetObject(e.Value,OpenMode.ForRead) as Group;if(g==null||g.Description!="Mr.T bảng ký hiệu và vùng che")continue;var ids=g.GetAllEntityIds();if(ids.Length!=2||!ids.Contains(table))continue;var other=ids.Single(x=>x!=table);if(tr.GetObject(other,OpenMode.ForRead) is Wipeout)result.Add(Tuple.Create(g.ObjectId,other));}
            Check(result.Count==1,"Expected exactly one owned group and wipeout");return result[0];
        }
    }
    [CommandMethod("MRTLEGEND2012TEST",CommandFlags.Modal)]public static void Run()
    {
        var previous=HostApplicationServices.WorkingDatabase;var log=new List<string>();
        try{using(var db=new Database(true,true)){
            HostApplicationServices.WorkingDatabase=db;ObjectId line;using(var tr=db.TransactionManager.StartTransaction()){var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);var l=new Line(Point3d.Origin,new Point3d(100,0,0));space.AppendEntity(l);tr.AddNewlyCreatedDBObject(l,true);line=l.ObjectId;tr.Commit();}
            var spec=new LegendSpec{DatabaseKey=LegendService.DatabaseKey(db),DrawingFactor=1,Groups=1};var row=LegendService.Capture(db,new[]{line},true);row.Description="Ban đầu";spec.Rows.Add(row);
            var id=LegendService.Write(db,spec,new Point3d(100,200,0),Vector3d.ZAxis,Vector3d.XAxis,ObjectId.Null);var first=MaskGroup(db,id);
            Check(LegendService.ResolveLegend(db,first.Item2)==id,"Selecting mask resolves its table");
            var other=LegendService.Write(db,spec,new Point3d(400,200,0),Vector3d.ZAxis,Vector3d.XAxis,ObjectId.Null);var second=MaskGroup(db,other);
            var edit=LegendService.Read(db,id);edit.Rows[0].Description="Đã sửa";edit.Width*=1.2;var result=LegendService.Write(db,edit,Point3d.Origin,Vector3d.ZAxis,Vector3d.XAxis,id);var updated=MaskGroup(db,id);
            Check(result==id&&first.Item1.IsErased&&first.Item2.IsErased,"Edit keeps table and replaces its group and mask");
            Check(updated.Item1!=first.Item1&&updated.Item2!=first.Item2&&!second.Item1.IsErased&&!second.Item2.IsErased,"Other table group remains intact");
            Check(LegendService.Read(db,id).Rows[0].Description=="Đã sửa"&&LegendService.Read(db,other).Rows[0].Description=="Ban đầu","Edit is isolated to selected table");
            var fail=LegendService.Read(db,id);fail.Rows[0].Description="Không lưu";bool rejected=false;try{LegendService.Write(db,fail,Point3d.Origin,Vector3d.ZAxis,Vector3d.XAxis,id,true);}catch(InvalidOperationException){rejected=true;}
            Check(rejected&&!updated.Item1.IsErased&&!updated.Item2.IsErased&&LegendService.Read(db,id).Rows[0].Description=="Đã sửa","Failure rolls back table, group and mask");
            log.Add("RESULT: PASS — direct table, owned group and mask replacement, independent table and transaction rollback.");
        }}catch(System.Exception ex){log.Add("RESULT: FAIL "+ex);}finally{HostApplicationServices.WorkingDatabase=previous;var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",log)+"\n");}
    }
}
