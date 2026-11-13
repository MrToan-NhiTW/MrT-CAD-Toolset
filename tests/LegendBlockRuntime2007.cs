using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;

[assembly:CommandClass(typeof(LegendBlockRuntime2007))]
public static class LegendBlockRuntime2007
{
    static void Check(bool condition,string message){if(!condition)throw new System.Exception(message);}
    static Table Inside(Transaction tr,ObjectId id){var br=(BlockReference)tr.GetObject(id,OpenMode.ForRead);var def=(BlockTableRecord)tr.GetObject(br.BlockTableRecord,OpenMode.ForRead);return def.Cast<ObjectId>().Select(x=>tr.GetObject(x,OpenMode.ForRead)).OfType<Table>().Single();}
    [CommandMethod("MRTLEGEND2007TEST",CommandFlags.Modal)]
    public static void Run()
    {
        var messages=new List<string>();var previous=HostApplicationServices.WorkingDatabase;
        try{using(var db=new Database(true,true)){
            HostApplicationServices.WorkingDatabase=db;ObjectId line;
            using(var tr=db.TransactionManager.StartTransaction()){
                var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);var e=new Line(new Point3d(-50,-50,0),new Point3d(500,200,0));space.AppendEntity(e);tr.AddNewlyCreatedDBObject(e,true);line=e.ObjectId;tr.Commit();}
            var spec=new LegendSpec{DatabaseKey=LegendService.DatabaseKey(db),DrawingFactor=1,Width=180,SymbolWidth=30};var row=LegendService.Capture(db,new[]{line},true);row.Description="Ống thử";spec.Rows.Add(row);
            bool rollback=false;try{LegendService.Write(db,spec,Point3d.Origin,Vector3d.ZAxis,Vector3d.XAxis,ObjectId.Null,true);}catch(InvalidOperationException){rollback=true;}Check(rollback,"Failure injection must roll back.");
            var id=LegendService.Write(db,spec,new Point3d(100,200,0),Vector3d.ZAxis,Vector3d.XAxis,ObjectId.Null);
            using(var tr=db.TransactionManager.StartTransaction()){
                var br=(BlockReference)tr.GetObject(id,OpenMode.ForRead);var def=(BlockTableRecord)tr.GetObject(br.BlockTableRecord,OpenMode.ForRead);var table=Inside(tr,id);var mask=def.Cast<ObjectId>().Select(x=>tr.GetObject(x,OpenMode.ForRead)).OfType<Wipeout>().Single();
                Check(def.Name=="Mr.T Bảng ký hiệu","First legend uses the requested default name.");Check(br.Layer=="Mr.T Bảng ký hiệu"&&table.Layer==br.Layer&&mask.Layer==br.Layer,"Wrapper, table and wipeout use the common layer.");Check(br.Color.ColorIndex==256&&table.Color.ColorIndex==0,"Block uses ByLayer and table uses ByBlock.");var sty=(TableStyle)tr.GetObject(table.TableStyle,OpenMode.ForRead);Check(sty.Color(RowType.DataRow).ColorIndex==0&&sty.GridColor(GridLineType.HorizontalTop,RowType.DataRow).ColorIndex==0,"Table text and grid use ByBlock.");var sample=(BlockTableRecord)tr.GetObject(table.Cells[2,0].BlockTableRecordId,OpenMode.ForRead);Check(sample.Name.StartsWith("Mr.T_LG_")&&sample.Name.Length==16,"Sample uses an eight-character identifier.");Check(sample.Cast<ObjectId>().Select(x=>(Entity)tr.GetObject(x,OpenMode.ForRead)).All(x=>x.Layer==br.Layer),"Sample geometry uses the same layer.");
                var order=(DrawOrderTable)tr.GetObject(def.DrawOrderTableId,OpenMode.ForRead);var ids=order.GetFullDrawOrder(0).Cast<ObjectId>().ToList();Check(ids.IndexOf(mask.ObjectId)<ids.IndexOf(table.ObjectId),"Mask must precede TABLE inside block.");
                var outer=(BlockTableRecord)tr.GetObject(br.OwnerId,OpenMode.ForRead);var outside=(DrawOrderTable)tr.GetObject(outer.DrawOrderTableId,OpenMode.ForRead);var all=outside.GetFullDrawOrder(0).Cast<ObjectId>().ToList();Check(all.IndexOf(line)<all.IndexOf(id),"Legend block must be above existing geometry.");Check(Math.Abs(mask.GeometricExtents.MaxPoint.X-table.Width)<1e-5,"Mask width must match table.");tr.Commit();}
            messages.Add("PASS: one block holds TABLE and WIPEOUT in correct draw order.");
            Check(LegendService.IsLegend(db,id),"New reference must be recognized.");var loaded=LegendService.Read(db,id);Check(loaded.Rows.Count==1&&loaded.Rows[0].Description=="Ống thử","Read nested table.");
            ObjectId copy;using(var tr=db.TransactionManager.StartTransaction()){
                var original=(BlockReference)tr.GetObject(id,OpenMode.ForRead);var space=(BlockTableRecord)tr.GetObject(original.OwnerId,OpenMode.ForWrite);var duplicate=(BlockReference)original.Clone();space.AppendEntity(duplicate);tr.AddNewlyCreatedDBObject(duplicate,true);duplicate.ScaleFactors=new Scale3d(2);duplicate.Position=new Point3d(700,200,0);duplicate.Color=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci,2);copy=duplicate.ObjectId;tr.Commit();}
            var scaled=LegendService.Read(db,copy);Check(Math.Abs(scaled.DrawingFactor-2)<1e-5,"Uniform scale must update drawing factor once.");scaled.Rows[0].Description="Bản đã sửa";var updated=LegendService.Write(db,scaled,Point3d.Origin,Vector3d.ZAxis,Vector3d.XAxis,copy);
            Check(updated==copy&&LegendService.Read(db,updated).Rows[0].Description=="Bản đã sửa","Updated block content keeps reference identity.");Check(LegendService.Read(db,id).Rows[0].Description=="Bản đã sửa","Editing a shared definition updates the other reference after confirmation.");
            using(var tr=db.TransactionManager.StartTransaction()){var b=(BlockReference)tr.GetObject(updated,OpenMode.ForRead);Check(Math.Abs(b.ScaleFactors.X-2)<1e-5&&Math.Abs(b.Position.X-700)<1e-5,"Edit preserves scale and insertion.");Check(b.Color.ColorIndex==2,"Explicit wrapper color survives edit.");tr.Commit();}
            messages.Add("PASS: scale, editing copied legend, and independent originals.");
            db.SaveAs(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"MrT_Legend_2007_Test.dwg"),DwgVersion.AC1032);
        }messages.Add("RESULT: PASS");}catch(System.Exception ex){messages.Add("RESULT: FAIL " + ex);}finally{HostApplicationServices.WorkingDatabase=previous;var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",messages)+"\n");}
    }
}
