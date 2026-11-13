using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;
[assembly:CommandClass(typeof(DensityRuntime2009))]
public static class DensityRuntime2009
{
    static void Check(bool ok,string text){if(!ok)throw new System.Exception(text);}
    static ObjectId CreateType(Database db,Transaction tr,ObjectId style,string name,string text){var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForWrite);var lt=new LinetypeTableRecord{Name=name,NumDashes=3,PatternLength=40};types.Add(lt);tr.AddNewlyCreatedDBObject(lt,true);lt.SetDashLengthAt(0,20);lt.SetDashLengthAt(1,-10);lt.SetDashLengthAt(2,-10);lt.SetShapeStyleAt(1,style);lt.SetTextAt(1,text);lt.SetShapeScaleAt(1,2.5);lt.SetShapeOffsetAt(1,new Vector2d(-4,-1));return lt.ObjectId;}
    [CommandMethod("MRTDENSITY2009TEST",CommandFlags.Modal)]public static void Run()
    {
        var old=HostApplicationServices.WorkingDatabase;var log=new List<string>();try{using(var db=new Database(true,true)){
            HostApplicationServices.WorkingDatabase=db;ObjectId a,b,untouched;
            using(var tr=db.TransactionManager.StartTransaction()){
                var styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForWrite);var st=new TextStyleTableRecord{Name="DENSITY_TEST",FileName="arial.ttf",TextSize=0,XScale=1};styles.Add(st);tr.AddNewlyCreatedDBObject(st,true);
                var ta=CreateType(db,tr,st.ObjectId,"Mr.T_A_D15_RAW","D15.A");var tb=CreateType(db,tr,st.ObjectId,"Mr.T_B_D15_RAW","D15.B");var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForWrite);var la=new LayerTableRecord{Name="Mr.T_A_D15_RAW",LinetypeObjectId=ta};layers.Add(la);tr.AddNewlyCreatedDBObject(la,true);var lb=new LayerTableRecord{Name="FOREIGN"};layers.Add(lb);tr.AddNewlyCreatedDBObject(lb,true);
                var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);var e1=new Line(Point3d.Origin,new Point3d(100,0,0)){LayerId=la.ObjectId,LinetypeScale=.5};space.AppendEntity(e1);tr.AddNewlyCreatedDBObject(e1,true);a=e1.ObjectId;
                var e2=new Line(new Point3d(0,10,0),new Point3d(100,10,0)){LayerId=lb.ObjectId,LinetypeId=tb,LinetypeScale=.25};space.AppendEntity(e2);tr.AddNewlyCreatedDBObject(e2,true);b=e2.ObjectId;
                var e3=new Line(new Point3d(0,20,0),new Point3d(100,20,0)){LayerId=lb.ObjectId};space.AppendEntity(e3);tr.AddNewlyCreatedDBObject(e3,true);untouched=e3.ObjectId;tr.Commit();}
            var scanned=DensityService.Scan(db);Check(scanned.Any(x=>!x.IsOverride&&x.Layer=="Mr.T_A_D15_RAW"&&x.Direct==1),"ByLayer scan");Check(scanned.Any(x=>x.IsOverride&&x.Layer=="FOREIGN"&&x.Type=="Mr.T_B_D15_RAW"&&x.Direct==1),"Explicit Mr.T type on foreign layer scan");
            var names=scanned.Where(x=>x.Selected).Select(x=>x.Key).ToList();var opt=new DensityOptions{Multiplier=2};var plan=DensityService.Preview(db,names,opt);Check(plan.Ready==2,"Both groups preview");Check(DensityService.Apply(db,plan,opt,names)==2,"Both groups apply atomically");
            using(var tr=db.TransactionManager.StartTransaction()){var e1=(Line)tr.GetObject(a,OpenMode.ForRead);var l=(LayerTableRecord)tr.GetObject(e1.LayerId,OpenMode.ForRead);var t=(LinetypeTableRecord)tr.GetObject(l.LinetypeObjectId,OpenMode.ForRead);Check(Math.Abs(t.PatternLength-80)<1e-8&&t.TextAt(1)=="D15.A"&&e1.LinetypeScale==.5,"ByLayer period and scale");var e2=(Line)tr.GetObject(b,OpenMode.ForRead);var t2=(LinetypeTableRecord)tr.GetObject(e2.LinetypeId,OpenMode.ForRead);Check(Math.Abs(t2.PatternLength-80)<1e-8&&t2.TextAt(1)=="D15.B"&&e2.LinetypeScale==.25,"Override period and scale");Check(((Line)tr.GetObject(untouched,OpenMode.ForRead)).LinetypeId==db.ByLayerLinetype,"Foreign type unchanged");tr.Commit();}
            var next=DensityService.Scan(db);names=next.Where(x=>x.Selected).Select(x=>x.Key).ToList();plan=DensityService.Preview(db,names,opt);using(var tr=db.TransactionManager.StartTransaction()){((Line)tr.GetObject(a,OpenMode.ForWrite)).LinetypeScale=.75;tr.Commit();}bool rejected=false;try{DensityService.Apply(db,plan,opt,names);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Scale change after preview blocks apply");
            log.Add("RESULT: PASS - ByLayer, override, foreign preservation, stale preview.");
        }}catch(System.Exception e){log.Add("RESULT: FAIL "+e);}finally{HostApplicationServices.WorkingDatabase=old;var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",log)+"\n");}
    }
}
