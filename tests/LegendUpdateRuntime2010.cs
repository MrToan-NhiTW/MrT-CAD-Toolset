using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;
[assembly:CommandClass(typeof(LegendUpdateRuntime2010))]
public static class LegendUpdateRuntime2010
{
    static void Check(bool ok,string name){if(!ok)throw new System.Exception(name);}
    static Table Inside(Transaction tr,ObjectId id){var br=(BlockReference)tr.GetObject(id,OpenMode.ForRead);var def=(BlockTableRecord)tr.GetObject(br.BlockTableRecord,OpenMode.ForRead);return def.Cast<ObjectId>().Select(x=>tr.GetObject(x,OpenMode.ForRead)).OfType<Table>().Single();}
    static int BlockCount(Database db){using(var tr=db.TransactionManager.StartOpenCloseTransaction()){return ((BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead)).Cast<ObjectId>().Count();}}
    static int StyleCount(Database db){using(var tr=db.TransactionManager.StartOpenCloseTransaction()){return ((DBDictionary)tr.GetObject(db.TableStyleDictionaryId,OpenMode.ForRead)).Count;}}
    [CommandMethod("MRTLEGEND2010TEST",CommandFlags.Modal)]public static void Run()
    {
        var prev=HostApplicationServices.WorkingDatabase;var log=new List<string>();try{using(var db=new Database(true,true)){
            HostApplicationServices.WorkingDatabase=db;ObjectId line;using(var tr=db.TransactionManager.StartTransaction()){var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);var l=new Line(Point3d.Origin,new Point3d(100,0,0));space.AppendEntity(l);tr.AddNewlyCreatedDBObject(l,true);line=l.ObjectId;tr.Commit();}
            var s=new LegendSpec{DatabaseKey=LegendService.DatabaseKey(db),DrawingFactor=1,Groups=1};var row=LegendService.Capture(db,new[]{line},true);row.Description="Ống ban đầu";s.Rows.Add(row);
            var id=LegendService.Write(db,s,new Point3d(100,200,0),Vector3d.ZAxis,Vector3d.XAxis,ObjectId.Null);ObjectId defid,sampleid,styleid;using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var br=(BlockReference)tr.GetObject(id,OpenMode.ForRead);defid=br.BlockTableRecord;var t=Inside(tr,id);sampleid=t.Cells[2,0].BlockTableRecordId;styleid=t.TableStyle;}
            int blocks=BlockCount(db),styles=StyleCount(db);
            for(int i=0;i<5;i++){var edit=LegendService.Read(db,id);edit.Rows[0].Description="Lần sửa "+i;var updated=LegendService.Write(db,edit,Point3d.Origin,Vector3d.ZAxis,Vector3d.XAxis,id);Check(updated==id&&!id.IsErased,"Reference identity must stay fixed");using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var br=(BlockReference)tr.GetObject(id,OpenMode.ForRead);Check(br.BlockTableRecord==defid&&((BlockTableRecord)tr.GetObject(defid,OpenMode.ForRead)).Name=="Mr.T Bảng ký hiệu","Definition and name stay fixed");var t=Inside(tr,id);Check(t.Cells[2,0].BlockTableRecordId==sampleid&&t.TableStyle==styleid,"Unchanged sample and style are reused");}Check(BlockCount(db)==blocks&&StyleCount(db)==styles,"Ordinary repeated edits do not accumulate blocks or styles");}
            var before=LegendService.Read(db,id).Rows[0].Description;var fail=LegendService.Read(db,id);fail.Rows[0].Description="không ghi";bool rejected=false;try{LegendService.Write(db,fail,Point3d.Origin,Vector3d.ZAxis,Vector3d.XAxis,id,true);}catch(InvalidOperationException){rejected=true;}Check(rejected&&LegendService.Read(db,id).Rows[0].Description==before&&BlockCount(db)==blocks,"Failure rolls back content and block count");
            ObjectId extraLine,extraWipe;using(var tr=db.TransactionManager.StartTransaction()){var def=(BlockTableRecord)tr.GetObject(defid,OpenMode.ForWrite);var l=new Line(new Point3d(0,5,0),new Point3d(20,5,0));def.AppendEntity(l);tr.AddNewlyCreatedDBObject(l,true);extraLine=l.ObjectId;var mask=new Wipeout();mask.SetDatabaseDefaults(db);mask.SetFrom(new Point2dCollection(new[]{new Point2d(300,300),new Point2d(310,300),new Point2d(310,290),new Point2d(300,290),new Point2d(300,300)}),Vector3d.ZAxis);def.AppendEntity(mask);tr.AddNewlyCreatedDBObject(mask,true);extraWipe=mask.ObjectId;tr.Commit();}
            var withExtras=LegendService.Read(db,id);withExtras.Rows[0].Description="Giữ đối tượng khác";Check(LegendService.Write(db,withExtras,Point3d.Origin,Vector3d.ZAxis,Vector3d.XAxis,id)==id&&!extraLine.IsErased&&!extraWipe.IsErased,"Update keeps additional unrelated geometry and wipeout");
            var shift=Matrix3d.Displacement(new Vector3d(23,17,0));using(var tr=db.TransactionManager.StartTransaction()){var t=Inside(tr,id);t.UpgradeOpen();t.TransformBy(shift);var def=(BlockTableRecord)tr.GetObject(defid,OpenMode.ForRead);var main=def.Cast<ObjectId>().Select(x=>tr.GetObject(x,OpenMode.ForRead)).OfType<Wipeout>().Single(x=>x.ObjectId!=extraWipe);main.UpgradeOpen();main.TransformBy(shift);tr.Commit();}
            var moved=LegendService.Read(db,id);moved.Rows[0].Description="Giữ vị trí trong block";LegendService.Write(db,moved,Point3d.Origin,Vector3d.ZAxis,Vector3d.XAxis,id);using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var t=Inside(tr,id);Check(Math.Abs(t.Position.X-23)<1e-8&&Math.Abs(t.Position.Y-17)<1e-8,"Update preserves table local position after block edits");}
            ObjectId copy;using(var tr=db.TransactionManager.StartTransaction()){var first=(BlockReference)tr.GetObject(id,OpenMode.ForRead);var space=(BlockTableRecord)tr.GetObject(first.OwnerId,OpenMode.ForWrite);var clone=(BlockReference)first.Clone();space.AppendEntity(clone);tr.AddNewlyCreatedDBObject(clone,true);clone.Position=new Point3d(500,200,0);clone.ScaleFactors=new Scale3d(2);copy=clone.ObjectId;tr.Commit();}
            Check(LegendService.ReferenceCount(db,id)==2,"Two references of same definition reported");var shared=LegendService.Read(db,copy);shared.Rows[0].Description="Cùng thay đổi";var result=LegendService.Write(db,shared,Point3d.Origin,Vector3d.ZAxis,Vector3d.XAxis,copy);Check(result==copy&&LegendService.Read(db,id).Rows[0].Description=="Cùng thay đổi"&&LegendService.Read(db,copy).Rows[0].Description=="Cùng thay đổi","Shared definition updates both references without replacement");using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var b=(BlockReference)tr.GetObject(copy,OpenMode.ForRead);Check(b.ScaleFactors.X==2&&b.Position.X==500,"Scaled copy keeps transform");}
            log.Add("RESULT: PASS — stable reference, definition, samples, styles, rollback and shared instances.");
        }}catch(System.Exception ex){log.Add("RESULT: FAIL "+ex);}finally{HostApplicationServices.WorkingDatabase=prev;var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",log)+"\n");}
    }
}
