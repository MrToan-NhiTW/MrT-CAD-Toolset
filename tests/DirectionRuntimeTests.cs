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
[assembly:CommandClass(typeof(DirectionRuntimeTests))]
public static class DirectionRuntimeTests
{
    static List<string> log=new List<string>();static void Check(bool value,string text){if(!value)throw new System.Exception(text);log.Add("PASS: "+text);}
    static bool Reject(Action a){try{a();return false;}catch(InvalidOperationException){return true;}}
    static ObjectId Add(Database db,Entity e,ObjectId layer){using(e)using(var tr=db.TransactionManager.StartTransaction()){var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);ms.AppendEntity(e);tr.AddNewlyCreatedDBObject(e,true);e.LayerId=layer;e.LinetypeId=db.ByLayerLinetype;e.Color=Color.FromRgb(14,80,150);e.LinetypeScale=1;tr.Commit();return e.ObjectId;}}
    static Polyline Route(bool reverse=false){var p=new Polyline();var pts=new[]{new Point2d(0,0),new Point2d(200,0),new Point2d(200,200)};if(reverse)Array.Reverse(pts);for(int i=0;i<pts.Length;i++)p.AddVertexAt(i,pts[i],0,2,2);p.Plinegen=true;return p;}
    static ObjectId[] Labels(Database db){using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);return ((BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead)).Cast<ObjectId>().Where(id=>tr.GetObject(id,OpenMode.ForRead) is MText).ToArray();}}
    static string Geometry(Database db,ObjectId id){using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var p=(Polyline)tr.GetObject(id,OpenMode.ForRead);return p.LayerId.Handle+"|"+p.Color.Red+","+p.Color.Green+","+p.Color.Blue+"|"+p.LinetypeScale+"|"+p.Closed+"|"+p.Normal+"|"+ScaleService.WidthState(p,tr)+"|"+String.Join(";",Enumerable.Range(0,p.NumberOfVertices).Select(i=>p.GetPoint3dAt(i)+":"+p.GetBulgeAt(i)));}}
    [CommandMethod("MRTDIRTEST",CommandFlags.Modal)]public static void Run()
    {
        var previous=HostApplicationServices.WorkingDatabase;log.Clear();string root=Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName;
        try
        {
            using(var db=new Database(true,true))
            {
                HostApplicationServices.WorkingDatabase=db;ObjectId layer,style,type;
                using(var tr=db.TransactionManager.StartTransaction())
                {
                    var styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForWrite);var st=new TextStyleTableRecord{Name="DIRECTION_TEST_STYLE",FileName="arial.ttf",TextSize=0,XScale=1};styles.Add(st);tr.AddNewlyCreatedDBObject(st,true);style=st.ObjectId;
                    var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForWrite);var lt=new LinetypeTableRecord{Name="DIRECTION_TEST_TYPE",AsciiDescription="D110.HDPE",NumDashes=3,PatternLength=40};types.Add(lt);tr.AddNewlyCreatedDBObject(lt,true);type=lt.ObjectId;lt.SetDashLengthAt(0,20);lt.SetDashLengthAt(1,-10);lt.SetDashLengthAt(2,-10);lt.SetShapeStyleAt(1,style);lt.SetTextAt(1,"D110.HDPE");lt.SetShapeScaleAt(1,2.5);lt.SetShapeIsUcsOrientedAt(1,true);
                    var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForWrite);var lr=new LayerTableRecord{Name="Mr.T_HDPE_D110_BASE",LinetypeObjectId=type,Color=Color.FromColorIndex(ColorMethod.ByAci,3)};layers.Add(lr);tr.AddNewlyCreatedDBObject(lr,true);layer=lr.ObjectId;tr.Commit();
                }
                ObjectId route=Add(db,Route(),layer),reverse=Add(db,Route(true),layer);string before=Geometry(db,route);var options=new DirectionOptions();db.Plinegen=true;
                var plan=DirectionService.Inspect(db,new[]{route,reverse},options);Check(plan.Items.Count==2&&plan.Items.All(x=>x.Labels.Count>0),"Both L-shaped route directions receive labels");Check(Labels(db).Length==0&&Geometry(db,route)==before,"Inspection leaves all source geometry and Model objects unchanged");
                DirectionService.Apply(db,plan,options);var first=Labels(db);Check(first.Length==plan.Items.Sum(x=>x.Labels.Count),"Native MText count matches the reviewed plan");Check(Geometry(db,route)==before,"Original polyline coordinates, widths, bulges, layer, RGB and scale are preserved");Check(!db.Plinegen,"Successful operation sets the new-polyline default PLINEGEN=0");
                using(var tr=db.TransactionManager.StartOpenCloseTransaction())
                {
                    var p=(Polyline)tr.GetObject(route,OpenMode.ForRead);Check(!p.Plinegen&&p.LinetypeId==db.ContinuousLinetype,"Generation is Disabled and native U text is suppressed at entity level");var texts=first.Select(id=>(MText)tr.GetObject(id,OpenMode.ForRead)).ToList();Check(texts.All(t=>t.TextStyleId==style&&t.BackgroundFill&&t.UseBackgroundColor&&t.LayerId==layer&&t.Color.Red==14),"Masks, real style and actual entity color are inherited by labels");Check(texts.Any(t=>Math.Abs(t.Rotation)<1e-8)&&texts.Any(t=>Math.Abs(t.Rotation-Math.PI*1.5)<1e-8),"Native MText rotations are 0 and 270 degrees");Check(((LayerTableRecord)tr.GetObject(layer,OpenMode.ForRead)).LinetypeObjectId==type&&((LinetypeTableRecord)tr.GetObject(type,OpenMode.ForRead)).TextAt(1)=="D110.HDPE","Shared layer and linetype definitions are untouched");
                }
                var update=DirectionService.Inspect(db,new[]{route,reverse},options);DirectionService.Apply(db,update,options);Check(Labels(db).Length==first.Length&&first.All(id=>id.IsErased),"Rerunning replaces owned labels without accumulating duplicates");
                string stable=Geometry(db,route);var ids=Labels(db);var fail=DirectionService.Inspect(db,new[]{route},options);Check(Reject(()=>DirectionService.Apply(db,fail,options,true))&&Geometry(db,route)==stable&&ids.All(id=>!id.IsErased),"Native transaction failure rolls back source and old/new labels together");
                var stale=DirectionService.Inspect(db,new[]{route},options);using(var tr=db.TransactionManager.StartTransaction()){((Polyline)tr.GetObject(route,OpenMode.ForWrite)).SetPointAt(1,new Point2d(220,0));tr.Commit();}Check(Reject(()=>DirectionService.Apply(db,stale,options)),"Geometry edited after review cannot receive a stale plan");
                ObjectId copied;using(var tr=db.TransactionManager.StartTransaction()){var source=(Entity)tr.GetObject(reverse,OpenMode.ForRead);using(var map=new IdMapping()){db.DeepCloneObjects(new ObjectIdCollection(new[]{reverse}),source.OwnerId,map,false);copied=map[reverse].Value;}tr.Commit();}
                var sourceLabels=Labels(db);DirectionService.Apply(db,DirectionService.Inspect(db,new[]{copied},options),options);Check(sourceLabels.All(id=>!id.IsErased)&&Labels(db).Length>sourceLabels.Length,"Updating a copied route cannot erase labels owned by the original route");
                var restore=new DirectionOptions{Restore=true};DirectionService.Apply(db,DirectionService.Inspect(db,new[]{copied},restore),restore);Check(sourceLabels.All(id=>!id.IsErased)&&Labels(db).Length==sourceLabels.Length,"Restoring the copy leaves original labels intact");
                DirectionService.Apply(db,DirectionService.Inspect(db,new[]{route,reverse},restore),restore);Check(Labels(db).Length==0,"Restore deletes exactly linked labels");using(var tr=db.TransactionManager.StartOpenCloseTransaction()){Check(((Entity)tr.GetObject(route,OpenMode.ForRead)).LinetypeId==db.ByLayerLinetype,"Restore returns original ByLayer override");}
                ObjectId shortRoute=Add(db,new Line(Point3d.Origin,new Point3d(1,0,0)),layer);Check(DirectionService.Inspect(db,new[]{shortRoute},options).ShortSegments==1,"Very short branch receives no elbow-overlapping label");
                var curve=Route();curve.SetBulgeAt(0,.5);ObjectId mixed=Add(db,curve,layer);var mixedPlan=DirectionService.Inspect(db,new[]{mixed},options);Check(mixedPlan.ArcSegments==1&&mixedPlan.Items[0].Labels.All(x=>Math.Abs(x.Rotation+Math.PI/2)<1e-8),"Curved segments are reported and only the straight branch is labeled");
                var closed=Route();closed.Closed=true;ObjectId closedId=Add(db,closed,layer);Check(DirectionService.Inspect(db,new[]{closedId},options).Items.Count==1,"Closed planar polyline is supported without opening it");
                ObjectId tilt=Add(db,new Line(Point3d.Origin,new Point3d(100,0,20)),layer);Check(DirectionService.Inspect(db,new[]{tilt},options).Items.Count==0,"3D tilted route is explicitly skipped");
                using(var tr=db.TransactionManager.StartTransaction()){((LayerTableRecord)tr.GetObject(layer,OpenMode.ForWrite)).IsLocked=true;tr.Commit();}Check(DirectionService.Inspect(db,new[]{route},options).Items.Count==0,"Locked layers are skipped before writes");
                // Persist the scratch-only regression drawing for visual inspection.
                db.SaveAs(Path.Combine(root,"evidence","direction-regression.dwg"),DwgVersion.AC1032);
            }
            log.Add("RESULT: PASS (scratch Database only; active user drawing was not edited).");
        }
        catch(System.Exception ex){log.Add("RESULT: FAIL");log.Add(ex.ToString());}
        finally{HostApplicationServices.WorkingDatabase=previous;Directory.CreateDirectory(Path.Combine(root,"evidence"));File.WriteAllLines(Path.Combine(root,"evidence","direction-runtime-tests.txt"),log);var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",log)+"\n");}
    }
}
