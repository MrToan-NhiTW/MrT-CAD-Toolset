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
[assembly:CommandClass(typeof(DrawingRuntimeTests))]
public static class DrawingRuntimeTests
{
    static readonly List<string> log=new List<string>();
    static void Check(bool ok,string message){if(!ok)throw new System.Exception(message);log.Add("PASS: "+message);}
    static bool Reject(Action action){try{action();return false;}catch(InvalidOperationException){return true;}}
    static ObjectId Layer(Database db,string name,ObjectId type,bool locked=false)
    {
        using(var tr=db.TransactionManager.StartTransaction()){var lt=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForWrite);var row=new LayerTableRecord{Name=name,LinetypeObjectId=type,IsLocked=locked,Color=Color.FromColorIndex(ColorMethod.ByAci,2)};lt.Add(row);tr.AddNewlyCreatedDBObject(row,true);tr.Commit();return row.ObjectId;}
    }
    static ObjectId Add(Database db,Entity e,ObjectId layer,ObjectId type,double scale,short color=4,ObjectId owner=default(ObjectId))
    {
        using(e)using(var tr=db.TransactionManager.StartTransaction()){var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var space=(BlockTableRecord)tr.GetObject(owner.IsNull?bt[BlockTableRecord.ModelSpace]:owner,OpenMode.ForWrite);e.SetDatabaseDefaults(db);space.AppendEntity(e);tr.AddNewlyCreatedDBObject(e,true);e.LayerId=layer;e.LinetypeId=type;e.LinetypeScale=scale;e.Color=Color.FromColorIndex(color==256?ColorMethod.ByLayer:ColorMethod.ByAci,color);tr.Commit();return e.ObjectId;}
    }
    static string EntityState(Database db,ObjectId id)
    {
        using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var e=(Entity)tr.GetObject(id,OpenMode.ForRead);var l=e as Line;return e.Layer+"|"+e.LinetypeId.Handle+"|"+e.Color.ColorIndex+"|"+e.LinetypeScale+"|"+(l==null?e.GetType().Name:l.StartPoint+"|"+l.EndPoint);}
    }
    static int Flags(Database db,string name,string path)
    {
        db.DxfOut(path,16,DwgVersion.AC1032);var lines=File.ReadAllLines(path);bool lt=false;string current="";
        for(int i=0;i+1<lines.Length;i+=2){string c=lines[i].Trim(),v=lines[i+1].Trim();if(c=="0"){lt=v=="LTYPE";current="";}if(!lt)continue;if(c=="2")current=v;if(c=="74"&&current==name&&v!="0")return Int32.Parse(v);}throw new System.Exception("Text flags not found: "+name);
    }
    [CommandMethod("MRTDWGTEST",CommandFlags.Modal)]public static void Run()
    {
        string root=Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName;string evidence=Path.Combine(root,"evidence");var previous=HostApplicationServices.WorkingDatabase;
        log.Clear();log.Add("Native scratch-database acceptance: "+DateTimeOffset.Now.ToString("o"));
        try
        {
            using(var db=new Database(true,true))
            {
                HostApplicationServices.WorkingDatabase=db;var p=Profile.Load(Path.Combine(root,"tests","legacy-medgas-fixture.json"));p.Rows=p.Rows.Where(r=>r.SystemCode=="OXY"&&r.Diameter==15).ToList();p.FontFile="arial.ttf";p.FontBold=false;p.RefreshNames();
                Check(SafeCreation.Apply(db,p)==1,"Create initial actual pipe definition once");var actual=DrawingService.Read(db).Single(r=>r.LayerName==p.LayerNameFor(p.Rows[0]));
                ObjectId pipeLayer,type;using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);pipeLayer=layers[actual.LayerName];type=((LayerTableRecord)tr.GetObject(pipeLayer,OpenMode.ForRead)).LinetypeObjectId;}
                var oldLayer=Layer(db,"OLD_PIPE",db.ContinuousLinetype);var locked=Layer(db,"LOCKED_PIPE",db.ContinuousLinetype,true);Layer(db,"SHARED_PIPE",type);
                ObjectId target=Add(db,new Line(new Point3d(1,2,3),new Point3d(100,2,3)),oldLayer,db.ContinuousLinetype,.25);
                ObjectId sample=Add(db,new Line(Point3d.Origin,new Point3d(100,0,0)),pipeLayer,db.ContinuousLinetype,2.5,6);
                ObjectId label=Add(db,new DBText{TextString="do not touch",Height=1},oldLayer,db.ByLayerLinetype,1);
                ObjectId lockedTarget=Add(db,new Line(Point3d.Origin,new Point3d(10,0,0)),locked,db.ByLayerLinetype,1);
                ObjectId block;using(var tr=db.TransactionManager.StartTransaction()){var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);var record=new BlockTableRecord{Name="TestPipeBlock"};bt.Add(record);tr.AddNewlyCreatedDBObject(record,true);block=record.ObjectId;tr.Commit();}
                ObjectId nested=Add(db,new Line(Point3d.Origin,new Point3d(10,0,0)),oldLayer,db.ByLayerLinetype,1,256,block);
                string sourceState=EntityState(db,sample),targetState=EntityState(db,target),labelState=EntityState(db,label),lockedState=EntityState(db,lockedTarget),nestedState=EntityState(db,nested);
                var snapshot=DrawingService.Read(db);actual=snapshot.Single(r=>r.LayerName==actual.LayerName);string fingerprint=actual.Fingerprint;
                Check(actual.UsedCount==1&&actual.OverrideCount==1&&actual.SharedLayers==2,"Actual drawing counts own overrides and shared type references");
                Check(actual.Editable&&snapshot.Single(r=>r.LayerName=="0").Editable==false,"Supported text pattern editable, Continuous readonly");
                Check(DrawingService.Read(db).Single(r=>r.LayerName==actual.LayerName).Fingerprint==fingerprint&&EntityState(db,target)==targetState,"Repeated read is stable and does not touch entities");
                var result=DrawingService.Assign(db,new[]{target,label,lockedTarget,nested,target},actual.LayerName,true,true,1,expected:actual);
                Check(result.Changed==1&&result.Skipped==3,"Catalog assignment deduplicates targets and skips text, locked, nested geometry");
                using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var line=(Line)tr.GetObject(target,OpenMode.ForRead);Check(line.LayerId==pipeLayer&&line.LinetypeId==db.ByLayerLinetype&&line.Color.ColorMethod==ColorMethod.ByLayer&&line.LinetypeScale==.25&&line.StartPoint==new Point3d(1,2,3)&&line.EndPoint==new Point3d(100,2,3),"Catalog changes only Layer/ByLayer settings, preserves scale and geometry");}
                Check(EntityState(db,label)==labelState&&EntityState(db,lockedTarget)==lockedState&&EntityState(db,nested)==nestedState&&EntityState(db,sample)==sourceState,"Skipped objects and source remain untouched");
                Check(Reject(()=>DrawingService.Assign(db,new[]{target},actual.LayerName,true,true,1,expected:actual)),"Stale catalog snapshot is rejected");
                actual=DrawingService.Read(db).Single(r=>r.LayerName==actual.LayerName);Check(DrawingService.Assign(db,new[]{target},actual.LayerName,true,true,1,expected:actual).Unchanged==1,"Repeated catalog assignment is a no-op");
                var picked=DrawingService.Pick(db,sample);DrawingService.Assign(db,new[]{target,sample},actual.LayerName,false,true,1,picked);
                using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var line=(Line)tr.GetObject(target,OpenMode.ForRead);Check(line.LinetypeId==db.ContinuousLinetype&&line.Color.ColorIndex==6&&line.LinetypeScale==2.5,"Quick pick copies actual entity overrides and scale exactly");}
                Check(EntityState(db,sample)==sourceState,"Picking source among targets preserves source properties");
                string rollback=EntityState(db,target);Check(Reject(()=>DrawingService.Assign(db,new[]{target},"OLD_PIPE",true,false,3,injectFailure:true))&&EntityState(db,target)==rollback,"Injected assignment failure rolls back every entity change");
                DrawingService.Assign(db,new[]{target},"OLD_PIPE",false,false,3);using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var line=(Line)tr.GetObject(target,OpenMode.ForRead);Check(line.Color.ColorIndex==6&&line.LinetypeScale==3,"Optional color preservation and explicit scale are respected");}
                var edit=new DefinitionEdit{Text="D15.Oxy_EDIT",Description="Actual edited definition",Dash=7,Before=2,After=2,Height=.6,X=-1,Y=-.2,ChangeColor=true,Aci=3};
                actual=DrawingService.Read(db).Single(r=>r.LayerName==actual.LayerName);int originalFlags=Flags(db,actual.LinetypeName,Path.Combine(evidence,"dwg-test-before.dxf"));DrawingService.Update(db,actual,edit);
                var changed=DrawingService.Read(db).Single(r=>r.LayerName==actual.LayerName);Check(changed.Text==edit.Text&&changed.Description==edit.Description&&changed.Dash==7&&changed.Aci==3,"Explicit reviewed edit updates actual existing values");
                Check(Flags(db,actual.LinetypeName,Path.Combine(evidence,"dwg-test-after.dxf"))==originalFlags,"Editing geometry preserves native U/R flags");
                Check(DrawingService.Read(db).Single(r=>r.LayerName=="SHARED_PIPE").Text==edit.Text,"Shared linetype keeps its identity and shared references");
                Check(Reject(()=>DrawingService.Update(db,actual,edit)),"Stale edit snapshot cannot overwrite newer definition");
                string changedFingerprint=changed.Fingerprint,changedFont=changed.Font;p.FontBold=true;p.Rows[0].Text="D15.WRONG_TEMPLATE";p.Rows[0].ColorIndex=1;
                Check(SafeCreation.Apply(db,p)==0,"Template creation skips existing name instead of restoring defaults");
                Check(DrawingService.Read(db).Single(r=>r.LayerName==actual.LayerName).Fingerprint==changedFingerprint,"Skipped creation preserves actual type, style, color and objects");
                string prefix=p.LayerPrefix;p.LayerPrefix="MRG";Check(SafeCreation.Apply(db,p)==1,"Fixture includes an owned legacy MRG definition");p.LayerPrefix=prefix;
                string legacyName=p.LegacyNameFor(p.Rows[0]),legacyFingerprint=DrawingService.Read(db).Single(r=>r.LayerName==legacyName).Fingerprint;
                Check(SafeCreation.Preview(db,p,new Point3d(1000,1000,0))==2,"Preview creates both directions in private namespace");
                Check(DrawingService.Read(db).Single(r=>r.LayerName==legacyName).Fingerprint==legacyFingerprint,"Preview cannot rename or overwrite legacy MRG definitions");
                changed=DrawingService.Read(db).Single(r=>r.LayerName==actual.LayerName);Check(changed.Fingerprint==changedFingerprint&&changed.Font==changedFont,"Private preview does not overwrite current definition or its style");
                Check(!DrawingService.Read(db).Any(r=>DrawingSearch.Preview(r.LayerName)),"Private previews cannot enter the current pipe catalog");
                string beforeFit=changed.Fingerprint;edit.AutoFit=true;DrawingService.PrepareEdit(db,changed,edit);Check(!edit.AutoFit&&edit.Before>0&&edit.After>0&&DrawingService.Read(db).Single(r=>r.LayerName==changed.LayerName).Fingerprint==beforeFit,"Fit preparation measures actual style in scratch DB without changing current definitions");
                using(var tr=db.TransactionManager.StartTransaction()){var e=(Entity)tr.GetObject(sample,OpenMode.ForWrite);e.LinetypeScale=4;tr.Commit();}Check(Reject(()=>DrawingService.Assign(db,new[]{target},actual.LayerName,false,true,1,picked)),"Changed picked source is rejected before copy");
                Check(HostApplicationServices.WorkingDatabase==db,"WorkingDatabase restored after measuring and previews");
            }
            log.Add("SUCCESS: "+log.Count+" recorded native checks.");
        }
        catch(System.Exception ex){log.Add("FAIL: "+ex);}
        finally{HostApplicationServices.WorkingDatabase=previous;File.WriteAllLines(Path.Combine(evidence,"drawing-runtime-tests.txt"),log);Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n"+String.Join("\n",log)+"\n");}
    }
}
