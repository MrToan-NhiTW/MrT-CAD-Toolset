using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly:CommandClass(typeof(EngineTests))]

public static class EngineTests
{
    private static readonly List<string> log=new List<string>();
    private static void Check(bool ok,string name) { if(!ok) throw new System.Exception("FAIL: "+name); log.Add("PASS: "+name); AcApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nPASS: "+name); }
    private static ObjectId TypeId(Database db,string name)
    {
        using(var tr=db.TransactionManager.StartTransaction()) return ((LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead))[name];
    }
    private static string StandardFingerprint(Database db)
    {
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var table=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForRead);
            var s=(TextStyleTableRecord)tr.GetObject(table["Standard"],OpenMode.ForRead);
            return s.FileName+"|"+s.TextSize+"|"+s.XScale+"|"+s.ObliquingAngle+"|"+s.ObjectId.Handle;
        }
    }
    private static Dictionary<string,int> DxfTextFlags(string path)
    {
        var lines=File.ReadAllLines(path); var result=new Dictionary<string,int>();
        bool ltype=false; string name="";
        for(int i=0;i+1<lines.Length;i+=2)
        {
            string code=lines[i].Trim(), value=lines[i+1].Trim();
            if(code=="0") { ltype=value=="LTYPE"; name=""; }
            if(!ltype) continue;
            if(code=="2") name=value;
            if(code=="74" && (name.StartsWith("MRG_") || name.StartsWith("Mr.T_",StringComparison.OrdinalIgnoreCase)) && value!="0") result[name]=Int32.Parse(value);
        }
        return result;
    }
    [CommandMethod("MRGTEST",CommandFlags.Modal)]
    public static void Run()
    {
        string bin=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string root=Directory.GetParent(bin).FullName; string evidence=Path.Combine(root,"evidence");
        try
        {
            log.Clear(); log.Add("AutoCAD 2024 Core Console integration test; "+DateTimeOffset.Now.ToString("o"));
            var p=Profile.Load(Path.Combine(root,"tests","legacy-profiles","reference-raw.json"));
            using(var db=new Database(true,true))
            {
                var prev=HostApplicationServices.WorkingDatabase;
                try
                {
                    HostApplicationServices.WorkingDatabase=db;
                    string standard=StandardFingerprint(db);
                    Check(SyncService.Inspect(db,p).All(x=>x.Layer=="Tạo mới" && x.Linetype=="Tạo mới"),"Initial dry run reports 10 new pairs");
                    Check(SyncService.Apply(db,p)==10,"Create 10 layer/linetype pairs");
                    Check(SyncService.Inspect(db,p).All(x=>x.Layer=="Giữ nguyên" && x.Linetype=="Giữ nguyên"),"Second dry run detects unchanged definitions");
                    Check(SyncService.Apply(db,p)==0,"Repeated synchronization is idempotent");
                    Check(StandardFingerprint(db)==standard,"STANDARD text style untouched");
                    var row=p.Rows.First(x=>x.SystemCode=="OXY" && x.Diameter==12); string name=p.NameFor(row); ObjectId id=TypeId(db,name);
                    row.GapAfter+=.2; row.ColorIndex=3;
                    Check(SyncService.Inspect(db,p).Single(x=>x.Name==name).Linetype=="Cập nhật","Pattern change detected");
                    Check(SyncService.Apply(db,p)==1,"Update one pair only");
                    Check(TypeId(db,name)==id,"Linetype update preserves ObjectId and existing references");
                    using(var tr=db.TransactionManager.StartTransaction())
                    {
                        var lt=(LinetypeTableRecord)tr.GetObject(id,OpenMode.ForRead);
                        Check(Math.Abs(lt.PatternLength-row.Period)<1e-10 && lt.TextAt(1)=="D12.Oxy" && lt.ShapeScaleAt(1)==.35,"Read back AutoCAD numeric geometry and text");
                    }
                    using(var tr=db.TransactionManager.StartTransaction())
                    {
                        var lt=(LinetypeTableRecord)tr.GetObject(id,OpenMode.ForWrite); lt.SetDashLengthAt(0,9); tr.Commit();
                    }
                    Check(SyncService.Inspect(db,p).Single(x=>x.Name==name).Linetype=="Cập nhật","Manual dash edits detected despite ownership marker");
                    SyncService.Apply(db,p);
                    string dxf=Path.Combine(evidence,"native-flags.dxf"); db.DxfOut(dxf,16,DwgVersion.AC1032);
                    var flags=DxfTextFlags(dxf); string agss=p.NameFor(p.Rows.Single(x=>x.SystemCode=="AGSS"));
                    Check(flags.ContainsKey(agss) && (flags[agss]&16)!=0,"Native LIN loader preserves U upright flag (DXF 74 bit 16)");
                    Check(flags.ContainsKey(name) && (flags[name]&16)==0,"R orientation remains relative, without upright bit");
                    row.RotationMode="A"; row.RotationDegrees=25; SyncService.Apply(db,p);
                    Check(SyncService.Inspect(db,p).All(x=>x.Linetype=="Giữ nguyên"),"Absolute rotation matches after synchronization");
                    row.RotationMode="R"; row.RotationDegrees=0; SyncService.Apply(db,p);
                    p.UnitTag="M"; p.GeometryFactor=.001; SyncService.Apply(db,p);
                    using(var tr=db.TransactionManager.StartTransaction())
                    {
                        var lt=(LinetypeTableRecord)tr.GetObject(TypeId(db,p.NameFor(row)),OpenMode.ForRead);
                        Check(Math.Abs(lt.ShapeScaleAt(1)-.00035)<1e-12 && Math.Abs(lt.DashLengthAt(0)-.0015)<1e-12,"Meter profile read back from native AutoCAD database");
                    }
                    p.UnitTag="RAW"; p.GeometryFactor=1;
                    SyncService.MakeSamples(db,p,Point3d.Origin);
                    Check(StandardFingerprint(db)==standard,"STANDARD unchanged after updates, rotation tests and samples");
                    var fitting=Profile.Load(Path.Combine(bin,"default-profile.json"));
                    SyncService.FitTextSpacing(db,fitting);
                    Check(fitting.Rows.All(x=>Math.Abs(x.GapBefore-x.GapAfter)<1e-10 && x.GapAfter>x.TextScale*fitting.PaddingRatio),"Font-measured spacing uses centered symmetric gaps");
                    Check(StandardFingerprint(db)==standard,"Text measurement leaves drawing styles untouched");
                    string sample=Path.Combine(root,"MrT-LayerLinetype-Sample.dwg"); db.SaveAs(sample,DwgVersion.AC1032);
                    Check(File.Exists(sample) && new FileInfo(sample).Length>1000,"Saved standalone editable sample DWG");
                }
                finally { HostApplicationServices.WorkingDatabase=prev; }
            }
            using(var conflictDb=new Database(true,true))
            {
                using(var tr=conflictDb.TransactionManager.StartTransaction())
                {
                    var l=(LayerTable)tr.GetObject(conflictDb.LayerTableId,OpenMode.ForWrite);
                    var foreign=new LayerTableRecord {Name=p.LayerNameFor(p.Rows[0])}; l.Add(foreign); tr.AddNewlyCreatedDBObject(foreign,true); tr.Commit();
                }
                Check(SyncService.Inspect(conflictDb,p).Any(x=>x.Conflict),"Unowned name collision is reported");
                bool rejected=false; try {SyncService.Apply(conflictDb,p);} catch(InvalidOperationException) {rejected=true;}
                Check(rejected,"Unowned collision blocks mutation");
            }
            using(var abortDb=new Database(true,true))
            {
                try {SyncService.Apply(abortDb,p,true);} catch(InvalidOperationException) { }
                Check(SyncService.Inspect(abortDb,p).All(x=>x.Layer=="Tạo mới" && x.Linetype=="Tạo mới"),"Injected failure after clone rolls back new definitions");
                SyncService.Apply(abortDb,p);
                var row=p.Rows[0]; double before=row.Dash; row.Dash+=1;
                try {SyncService.Apply(abortDb,p,true);} catch(InvalidOperationException) { }
                row.Dash=before;
                Check(SyncService.Inspect(abortDb,p).All(x=>x.Linetype=="Giữ nguyên"),"Injected failure rolls back replacement of existing definitions");
            }
            using(var migrationDb=new Database(true,true))
            {
                var legacyProfile=Profile.Load(Path.Combine(root,"tests","legacy-profiles","reference-raw.json")); legacyProfile.LayerPrefix="MRG";
                foreach(var r in legacyProfile.Rows) r.RotationMode="R";
                SyncService.Apply(migrationDb,legacyProfile);
                ObjectId originalLayer;
                using(var tr=migrationDb.TransactionManager.StartTransaction()) originalLayer=((LayerTable)tr.GetObject(migrationDb.LayerTableId,OpenMode.ForRead))[legacyProfile.LayerNameFor(legacyProfile.Rows[0])];
                legacyProfile.LayerPrefix="Mr.T";
                Check(SyncService.Inspect(migrationDb,legacyProfile).All(x=>x.Layer=="Đổi tên"),"Legacy MRG layers are planned for rename to Mr.T");
                SyncService.Apply(migrationDb,legacyProfile);
                using(var tr=migrationDb.TransactionManager.StartTransaction())
                {
                    var layers=(LayerTable)tr.GetObject(migrationDb.LayerTableId,OpenMode.ForRead);
                    Check(layers[legacyProfile.LayerNameFor(legacyProfile.Rows[0])]==originalLayer,"Layer rename preserves ObjectId and entity references");
                }
                var working=Profile.Load(Path.Combine(bin,"default-profile.json"));
                // Preserve this legacy command as an R-path regression; MRTTEST exercises the U default.
                foreach(var r in working.Rows) { r.RotationMode="R"; r.RotationDegrees=0; }
                SyncService.Apply(migrationDb,working);
                var co2=working.Rows.First(x=>x.SystemCode=="CO2");
                using(var tr=migrationDb.TransactionManager.StartTransaction())
                {
                    var types=(LinetypeTable)tr.GetObject(migrationDb.LinetypeTableId,OpenMode.ForRead);
                    var type=(LinetypeTableRecord)tr.GetObject(types[working.NameFor(co2)],OpenMode.ForRead);
                    var style=(TextStyleTableRecord)tr.GetObject(type.ShapeStyleAt(1),OpenMode.ForRead);
                    Check(style.Font.TypeFace.Equals("Bahnschrift",StringComparison.OrdinalIgnoreCase),"CO2 linetype uses actual Bahnschrift font descriptor");
                    Check(working.Rows.Where(x=>x.SystemCode=="CO2" || x.SystemCode=="AGSS").All(x=>x.RotationMode=="R"),"Working CO2 and AGSS retain R=0");
                }
                migrationDb.DxfOut(Path.Combine(evidence,"v02-font-relative.dxf"),16,DwgVersion.AC1032);
                var v02flags=DxfTextFlags(Path.Combine(evidence,"v02-font-relative.dxf"));
                Check(working.Rows.All(x=>(v02flags[working.NameFor(x)]&16)==0),"Working linetypes have no U upright flag after font fitting");
            }
            log.Add("RESULT: PASS ("+log.Count(x=>x.StartsWith("PASS:"))+" assertions)");
        }
        catch(System.Exception ex) { log.Add("RESULT: FAIL\r\n"+ex); AcApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nFAIL: "+ex); }
        File.WriteAllLines(Path.Combine(evidence,"autocad-integration-tests.txt"),log,Encoding.UTF8);
    }
}
