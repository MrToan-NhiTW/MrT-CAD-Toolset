using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;
[assembly:CommandClass(typeof(LegendPipeSyncRuntime))]
public static class LegendPipeSyncRuntime
{
    static void Check(bool ok,string message){if(!ok)throw new System.Exception(message);}
    [CommandMethod("MRTLEGSYNC2015TEST",CommandFlags.Modal)]public static void Run()
    {
        var previous=HostApplicationServices.WorkingDatabase;var log=new List<string>();
        try{using(var db=new Database(true,true)){
            HostApplicationServices.WorkingDatabase=db;ObjectId a,b,other;
            using(var tr=db.TransactionManager.StartTransaction()){
                var styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForWrite);var style=new TextStyleTableRecord{Name="SYNC_TEST",FileName="arial.ttf",TextSize=0,XScale=1};styles.Add(style);tr.AddNewlyCreatedDBObject(style,true);
                var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForWrite);var lt=new LinetypeTableRecord{Name="Mr.T_VAC_D28_RAW",NumDashes=3,PatternLength=40};types.Add(lt);tr.AddNewlyCreatedDBObject(lt,true);lt.SetDashLengthAt(0,20);lt.SetDashLengthAt(1,-10);lt.SetDashLengthAt(2,-10);lt.SetShapeStyleAt(1,style.ObjectId);lt.SetTextAt(1,"D28.VAC");lt.SetShapeScaleAt(1,2.5);lt.SetShapeOffsetAt(1,new Vector2d(-4,-1));
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForWrite);var layer=new LayerTableRecord{Name="Mr.T_VAC_D28_RAW",LinetypeObjectId=lt.ObjectId};layers.Add(layer);tr.AddNewlyCreatedDBObject(layer,true);
                var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);
                var first=new Polyline();first.AddVertexAt(0,new Point2d(0,0),0,0,0);first.AddVertexAt(1,new Point2d(100,0),0,0,0);first.LayerId=layer.ObjectId;first.LinetypeScale=.5;first.ConstantWidth=2;space.AppendEntity(first);tr.AddNewlyCreatedDBObject(first,true);a=first.ObjectId;
                var second=new Line(new Point3d(0,10,0),new Point3d(100,10,0)){LayerId=layer.ObjectId,LinetypeScale=.25};space.AppendEntity(second);tr.AddNewlyCreatedDBObject(second,true);b=second.ObjectId;
                var symbol=new Circle(new Point3d(10,10,0),Vector3d.ZAxis,4);space.AppendEntity(symbol);tr.AddNewlyCreatedDBObject(symbol,true);other=symbol.ObjectId;tr.Commit();}
            var s=new LegendSpec{DatabaseKey=LegendService.DatabaseKey(db),Groups=1,PipeLinetypeMultiplier=2,PipeWidthMultiplier=3};var ra=LegendService.Capture(db,new[]{a},true);ra.SampleScale=.8;var rb=LegendService.Capture(db,new[]{b},true);var legacy=LegendService.Capture(db,new[]{a},true);legacy.PipeSourceDatabaseKey=null;legacy.PipeSourceHandle=null;legacy.PipeSourceLayer=null;s.Rows.Add(ra);s.Rows.Add(rb);s.Rows.Add(legacy);s.Rows.Add(LegendService.Capture(db,new[]{other},false));
            var tableId=LegendService.Write(db,s,Point3d.Origin,Vector3d.ZAxis,Vector3d.XAxis,ObjectId.Null);ObjectId oldSymbol,oldMask;double width,height,scale,constant;
            using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var t=(Table)tr.GetObject(tableId,OpenMode.ForRead);width=t.Width;height=t.Height;oldSymbol=t.Cells[5,0].BlockTableRecordId;var rec=(BlockTableRecord)tr.GetObject(t.Cells[2,0].BlockTableRecordId,OpenMode.ForRead);var pl=(Polyline)tr.GetObject(rec.Cast<ObjectId>().Single(),OpenMode.ForRead);scale=pl.LinetypeScale;constant=pl.ConstantWidth;var dict=(DBDictionary)tr.GetObject(t.ExtensionDictionary,OpenMode.ForRead);var x=(Xrecord)tr.GetObject(dict.GetAt("MRT_LEGEND_MASK_V1"),OpenMode.ForRead);using(var vals=x.Data)oldMask=(ObjectId)vals.AsArray()[0].Value;}
            var before=DensityService.Scan(db);var keys=before.Where(x=>x.Selected).Select(x=>x.Key).ToList();var opt=new DensityOptions{PaperMode=true,PaperMm=25,Denominator=200};var plan=DensityService.Preview(db,keys,opt);Check(DensityService.Apply(db,plan,opt,keys)==1,"Density must change source layer.");
            var sync=LegendPipeSync.Inspect(db,before);Check(sync.Changes.Count==3&&sync.Skipped.Count==0,"Both source handles and legacy row must resolve mixed source scales.");Check(LegendPipeSync.Apply(db,sync)==3,"Three cells must update.");
            using(var tr=db.TransactionManager.StartOpenCloseTransaction()){
                var t=(Table)tr.GetObject(tableId,OpenMode.ForRead);Check(Math.Abs(t.Width-width)<1e-8&&Math.Abs(t.Height-height)<1e-8&&t.Cells[5,0].BlockTableRecordId==oldSymbol&&!oldMask.IsErased,"Table geometry, symbol and wipeout must be preserved.");
                var pa=(Polyline)tr.GetObject(((BlockTableRecord)tr.GetObject(t.Cells[2,0].BlockTableRecordId,OpenMode.ForRead)).Cast<ObjectId>().Single(),OpenMode.ForRead);var pb=(Polyline)tr.GetObject(((BlockTableRecord)tr.GetObject(t.Cells[3,0].BlockTableRecordId,OpenMode.ForRead)).Cast<ObjectId>().Single(),OpenMode.ForRead);
                var ea=(Entity)tr.GetObject(a,OpenMode.ForRead);var la=(LayerTableRecord)tr.GetObject(ea.LayerId,OpenMode.ForRead);var ta=ea.LinetypeId==db.ByLayerLinetype?la.LinetypeObjectId:ea.LinetypeId;var eb=(Entity)tr.GetObject(b,OpenMode.ForRead);var tb=eb.LinetypeId==db.ByLayerLinetype?la.LinetypeObjectId:eb.LinetypeId;
                Check(pa.LinetypeId==ta&&pb.LinetypeId==tb&&ta!=tb,"Each sample must use its source scale's density definition.");Check(Math.Abs(pa.LinetypeScale-scale)<1e-8&&Math.Abs(pa.ConstantWidth-constant)<1e-8,"Linetype and width multipliers must stay intact.");}
            var again=LegendPipeSync.Inspect(db,DensityService.Scan(db));Check(again.Changes.Count==0,"Sync must be idempotent.");log.Add("RESULT: PASS — mixed scales, legacy rows, linked source, sample size, width, table geometry, symbols, mask and idempotence.");
        }}catch(System.Exception ex){log.Add("RESULT: FAIL "+ex);}finally{HostApplicationServices.WorkingDatabase=previous;var path=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"LegendPipeSyncRuntime.txt");File.WriteAllLines(path,log);var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",log)+"\n");}
    }
}
