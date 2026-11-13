using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;
[assembly:CommandClass(typeof(ScaleRuntimeTests))]
public static class ScaleRuntimeTests
{
    static readonly List<string> log=new List<string>();static void Check(bool b,string s){if(!b)throw new System.Exception(s);log.Add("PASS: "+s);}
    static ObjectId Layer(Database db,string name,bool locked=false){using(var tr=db.TransactionManager.StartTransaction()){var table=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForWrite);var l=new LayerTableRecord{Name=name,Color=Color.FromColorIndex(ColorMethod.ByAci,3),IsLocked=locked};table.Add(l);tr.AddNewlyCreatedDBObject(l,true);tr.Commit();return l.ObjectId;}}
    static ObjectId Add(Database db,Entity e,ObjectId layer,ObjectId owner=default(ObjectId)){using(e)using(var tr=db.TransactionManager.StartTransaction()){var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var space=(BlockTableRecord)tr.GetObject(owner.IsNull?bt[BlockTableRecord.ModelSpace]:owner,OpenMode.ForWrite);space.AppendEntity(e);tr.AddNewlyCreatedDBObject(e,true);e.LayerId=layer;e.LinetypeId=db.ContinuousLinetype;e.Color=Color.FromRgb(10,20,30);e.LinetypeScale=.1;tr.Commit();return e.ObjectId;}}
    static string State(Database db,ObjectId id){using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var e=(Entity)tr.GetObject(id,OpenMode.ForRead);return e.LayerId.Handle+"|"+e.LinetypeId.Handle+"|"+e.Color.ColorMethod+"|"+e.Color.Red+"|"+e.Color.Green+"|"+e.Color.Blue+"|"+e.LinetypeScale+"|"+ScaleService.WidthState(e,tr);}}
    static bool Reject(Action a){try{a();return false;}catch(InvalidOperationException){return true;}}
    [CommandMethod("MRTSCALETEST",CommandFlags.Modal)]public static void Run()
    {
        string root=Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName;var previous=HostApplicationServices.WorkingDatabase;log.Clear();
        try
        {
            using(var db=new Database(true,true))
            {
                HostApplicationServices.WorkingDatabase=db;ObjectId cn=Layer(db,"Mr.T_CN_D110_BASE"),tn=Layer(db,"Mr.T_TN_D160_BASE"),other=Layer(db,"OUTSIDE"),locked=Layer(db,"LOCKED",true);
                var p=new Polyline();p.AddVertexAt(0,new Point2d(10,20),.2,10,15);p.AddVertexAt(1,new Point2d(110,20),0,15,20);ObjectId pipe=Add(db,p,cn),line=Add(db,new Line(Point3d.Origin,new Point3d(100,0,0)),tn),untouched=Add(db,new Line(Point3d.Origin,new Point3d(100,0,0)),other),text=Add(db,new DBText{TextString="label",Height=2.5},cn),lockedLine=Add(db,new Line(Point3d.Origin,new Point3d(100,0,0)),locked);
                ObjectId block;using(var tr=db.TransactionManager.StartTransaction()){var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);var b=new BlockTableRecord{Name="SCALE_TEST_BLOCK"};bt.Add(b);tr.AddNewlyCreatedDBObject(b,true);block=b.ObjectId;tr.Commit();}ObjectId nested=Add(db,new Line(Point3d.Origin,new Point3d(10,0,0)),cn,block);
                string before=State(db,pipe),outsideBefore=State(db,untouched),textBefore=State(db,text),nestedBefore=State(db,nested),lockedBefore=State(db,lockedLine);double global=db.Ltscale;
                var options=new ScaleOptions{ApplyScale=true,EntityScale=.7,ApplyWidth=true,GlobalWidth=25};var ids=ScaleService.OnLayers(db,new[]{"Mr.T_CN_D110_BASE","Mr.T_TN_D160_BASE"});Check(ids.Contains(pipe)&&ids.Contains(line)&&ids.Contains(text)&&!ids.Contains(nested)&&!ids.Contains(untouched),"Layer scope covers both chosen systems and excludes nested block geometry/outside layers");
                var plan=ScaleService.Inspect(db,ids.Concat(new[]{lockedLine,pipe}),options);Check(plan.States.Count==2&&plan.Skipped==2&&plan.UnsupportedWidth==1,"Inspection deduplicates and reports text/locked objects plus LINE width limitation");Check(State(db,pipe)==before,"Preview is read-only");Check(ScaleService.Apply(db,plan,options)==2,"Scale sync updates two eligible curves atomically");
                using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var curve=(Polyline)tr.GetObject(pipe,OpenMode.ForRead);Check(curve.LinetypeScale==.7&&curve.GetStartWidthAt(0)==25&&curve.GetEndWidthAt(0)==25,"Scale and width are normalized on the selected polyline");Check(curve.Color.Red==10&&curve.Color.Green==20&&curve.Color.Blue==30&&curve.LayerId==cn&&curve.LinetypeId==db.ContinuousLinetype,"Scale sync preserves actual color/Layer/Linetype overrides");Check(curve.GetPoint2dAt(0)==new Point2d(10,20)&&curve.GetPoint2dAt(1)==new Point2d(110,20)&&curve.GetBulgeAt(0)==.2,"Vertex coordinates and bulges remain intact");}
                Check(State(db,untouched)==outsideBefore&&State(db,text)==textBefore&&State(db,nested)==nestedBefore&&State(db,lockedLine)==lockedBefore,"Outside objects, labels, nested geometry and locked objects remain unchanged");Check(db.Ltscale==global,"Global LTSCALE is unchanged");
                var repeated=ScaleService.Inspect(db,new[]{pipe,line},options);Check(repeated.Unchanged==2&&ScaleService.Apply(db,repeated,options)==0,"Repeating exact settings is a no-op");
                var changed=new ScaleOptions{ApplyScale=true,EntityScale=2,ApplyWidth=true,GlobalWidth=5};var rollbackPlan=ScaleService.Inspect(db,new[]{pipe,line},changed);string stable=State(db,pipe);Check(Reject(()=>ScaleService.Apply(db,rollbackPlan,changed,true))&&State(db,pipe)==stable,"Injected failure rolls back scale and width together");
                using(var tr=db.TransactionManager.StartTransaction()){((Entity)tr.GetObject(pipe,OpenMode.ForWrite)).LinetypeScale=3;tr.Commit();}Check(Reject(()=>ScaleService.Apply(db,rollbackPlan,changed)),"Stale state cannot overwrite newer entity values");
                var widthOnly=new ScaleOptions{ApplyWidth=true,GlobalWidth=0};var widthPlan=ScaleService.Inspect(db,new[]{pipe,line},widthOnly);Check(widthPlan.States.Count==1&&widthPlan.Skipped==1,"Width-only skips LINE and acts only on a 2D polyline");ScaleService.Apply(db,widthPlan,widthOnly);using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var curve=(Polyline)tr.GetObject(pipe,OpenMode.ForRead);Check(curve.LinetypeScale==3&&ScaleService.WidthMatches(curve,tr,0),"Width-only leaves linetype scale unchanged");var layer=(LayerTableRecord)tr.GetObject(cn,OpenMode.ForRead);Check(layer.Color.ColorIndex==3&&layer.LinetypeObjectId==db.ContinuousLinetype,"Layer definition remains unchanged");}
            }log.Add("RESULT: PASS");
        }
        catch(System.Exception ex){log.Add("RESULT: FAIL");log.Add(ex.ToString());}
        finally{HostApplicationServices.WorkingDatabase=previous;File.WriteAllLines(Path.Combine(root,"evidence","scale-runtime-tests.txt"),log);var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",log)+"\n");}
    }
}
