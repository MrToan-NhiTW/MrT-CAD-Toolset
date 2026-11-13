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

[assembly:CommandClass(typeof(ToolsetRuntimeTests))]
public static class ToolsetRuntimeTests
{
    private static readonly List<string> log=new List<string>();
    private static void Check(bool ok,string name) { if(!ok) throw new System.Exception(name); log.Add("PASS: "+name); }
    [CommandMethod("MRTTEST",CommandFlags.Modal)]
    public static void Run()
    {
        string root=Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName;
        string evidence=Path.Combine(root,"evidence"); var previous=HostApplicationServices.WorkingDatabase;
        log.Clear(); log.Add("Toolset 1.9.3 U=0 native regression: "+DateTimeOffset.Now.ToString("o"));
        try
        {
            using(var unrelated=new Database(true,true))
            using(var target=new Database(true,true))
            {
                HostApplicationServices.WorkingDatabase=unrelated;
                var p=Profile.Load(Path.Combine(root,"tests","legacy-medgas-fixture.json"));
                Check(SyncService.CreateSamples(target,p,new Point3d(10,20,3))==26,"Create 26 sample lines with another WorkingDatabase active");
                Check(HostApplicationServices.WorkingDatabase.UnmanagedObject==unrelated.UnmanagedObject,"WorkingDatabase restored after sample operation");
                using(var tr=target.TransactionManager.StartTransaction())
                {
                    var bt=(BlockTable)tr.GetObject(target.BlockTableId,OpenMode.ForRead);
                    var model=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);
                    int lines=0;
                    foreach(ObjectId id in model)
                    {
                        var line=tr.GetObject(id,OpenMode.ForRead) as Line; if(line==null) continue; lines++;
                        Check(line.Database.UnmanagedObject==target.UnmanagedObject && line.LayerId.Database.UnmanagedObject==target.UnmanagedObject && line.LinetypeId.Database.UnmanagedObject==target.UnmanagedObject,"Sample references remain in target DWG, line "+lines);
                        Check(line.StartPoint.Z==3 && line.EndPoint.Z==3,"Sample origin elevation is retained");
                    }
                    Check(lines==26,"Model contains exactly 26 sample lines");
                    var styles=(TextStyleTable)tr.GetObject(target.TextStyleTableId,OpenMode.ForRead);
                    var style=(TextStyleTableRecord)tr.GetObject(styles[p.StyleName],OpenMode.ForRead);
                    Check(style.Font.TypeFace.Equals("Bahnschrift",StringComparison.OrdinalIgnoreCase) && style.Font.Bold,"Actual target text style is Bahnschrift Bold");
                    var types=(LinetypeTable)tr.GetObject(target.LinetypeTableId,OpenMode.ForRead);
                    foreach(var r in p.Rows)
                    {
                        var type=(LinetypeTableRecord)tr.GetObject(types[p.NameFor(r)],OpenMode.ForRead);
                        Check(type.ShapeStyleAt(1)==style.ObjectId,"Linetype uses target style, "+r.Text);
                        Check(type.TextAt(1)==r.DefaultText,"Native linetype uses diameter-first automatic label, "+r.Text);
                        Check(type.Comments==r.DescriptionText && p.NameFor(r).StartsWith("Mr.T_"),"Description and prefix correct, "+r.Text);
                    }
                }
                Check(SyncService.Apply(target,p)==0,"Repeating sync makes no changes");
                var firstRow=p.Rows.First(); ObjectId beforeId=TypeId(target,p.NameFor(firstRow));
                firstRow.RotationMode="R"; Check(SyncService.Apply(target,p)==1,"Mixed U/R profile updates one row through native loader");
                Check(TypeId(target,p.NameFor(firstRow))==beforeId,"Native replacement retains existing Linetype ObjectId");
                firstRow.RotationMode="U"; SyncService.Apply(target,p);
                Check(TypeId(target,p.NameFor(firstRow))==beforeId,"Switching back to U retains references");
                var first=p.Rows.First(); first.DescriptionText="Changed description";
                Check(SyncService.Apply(target,p)==1,"Editing description updates one definition");
                // Compare exported native flags against an actual U/R pair loaded by AutoCAD;
                // do not assume an undocumented numeric upright flag value.
                var fixture=p.Rows.First().Copy(); fixture.SystemCode="REFU"; fixture.RotationMode="U";
                var relative=fixture.Copy(); relative.SystemCode="REFR"; relative.RotationMode="R";
                var reference=new Profile {Rows=new List<GasRow>{fixture,relative}};
                SyncService.Apply(target,reference);
                var flags=ReadFlags(target,Path.Combine(evidence,"toolset-v1-native-u-flags.dxf"));
                int upright=flags[reference.NameFor(fixture)], rotating=flags[reference.NameFor(relative)];
                Check(upright!=rotating,"Native U and R definitions export distinct orientation flags");
                foreach(var r in p.Rows) Check(flags[p.NameFor(r)]==upright,"U flag survives cloning and updates: "+r.Text);
                foreach(var r in p.Rows) {r.RotationMode="R";r.RotationDegrees=0;}
                SyncService.Apply(target,p);
                flags=ReadFlags(target,Path.Combine(evidence,"toolset-v1-native-r-flags.dxf"));
                foreach(var r in p.Rows) Check(flags[p.NameFor(r)]==rotating,"R direct path clears the previous U flag: "+r.Text);
                foreach(var r in p.Rows) r.RotationMode="U"; SyncService.Apply(target,p);
                AddAngleCoupons(target,p,root);
                target.SaveAs(Path.Combine(root,"MrT-toolset-v1-runtime-sample.dwg"),DwgVersion.AC1032);
            }
            using(var abort=new Database(true,true))
            {
                var p=Profile.Load(Path.Combine(root,"tests","legacy-medgas-fixture.json"));
                bool failed=false; try { SyncService.Apply(abort,p,true); } catch(InvalidOperationException) {failed=true;}
                Check(failed && SyncService.Inspect(abort,p).All(x=>x.Layer=="Tạo mới" && x.Linetype=="Tạo mới"),"Native U definition creation is rolled back on failure");
            }
            using(var legacyDb=new Database(true,true))
            {
                var p=Profile.Load(Path.Combine(root,"tests","legacy-medgas-fixture.json"));
                foreach(var r in p.Rows) r.RotationMode="R";
                SyncService.Apply(legacyDb,p); var row=p.Rows.First(); ObjectId oldId=TypeId(legacyDb,p.NameFor(row));
                using(var tr=legacyDb.TransactionManager.StartTransaction())
                {
                    ((LinetypeTableRecord)tr.GetObject(oldId,OpenMode.ForWrite)).Name=p.LegacyNameFor(row);
                    var layers=(LayerTable)tr.GetObject(legacyDb.LayerTableId,OpenMode.ForRead);
                    ((LayerTableRecord)tr.GetObject(layers[p.LayerNameFor(row)],OpenMode.ForWrite)).Name=p.LegacyNameFor(row); tr.Commit();
                }
                foreach(var r in p.Rows) r.RotationMode="U";
                Check(SyncService.Apply(legacyDb,p)==13 && TypeId(legacyDb,p.NameFor(row))==oldId,"Owned legacy MRG name migrates to Mr.T while switching to U");
                double before=p.Rows[0].Dash; p.Rows[0].Dash+=2; bool failed=false;
                try {SyncService.Apply(legacyDb,p,true);} catch(InvalidOperationException){failed=true;}
                p.Rows[0].Dash=before;
                Check(failed && SyncService.Inspect(legacyDb,p).All(x=>x.Linetype=="Giữ nguyên"),"Failed U update restores existing definitions and ownership stamps");
            }
            log.Add("RESULT: PASS ("+log.Count(x=>x.StartsWith("PASS:"))+" checks)");
        }
        catch(System.Exception ex) { log.Add("RESULT: FAIL\r\nStage: "+ex.Data["MrT.Stage"]+"\r\n"+ex); }
        finally { HostApplicationServices.WorkingDatabase=previous; }
        Directory.CreateDirectory(evidence); File.WriteAllLines(Path.Combine(evidence,"toolset-v1-autocad-tests.txt"),log,Encoding.UTF8);
        Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n"+log.Last()+"\n"+Path.Combine(evidence,"toolset-v1-autocad-tests.txt")+"\n");
    }
    private static ObjectId TypeId(Database db,string name)
    { using(var tr=db.TransactionManager.StartTransaction()) return ((LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead))[name]; }
    private static Dictionary<string,int> ReadFlags(Database db,string path)
    {
        db.DxfOut(path,16,DwgVersion.AC1032); var lines=File.ReadAllLines(path);var flags=new Dictionary<string,int>(); bool type=false;string name="";
        for(int i=0;i+1<lines.Length;i+=2)
        {
            string code=lines[i].Trim(), value=lines[i+1].Trim();
            if(code=="0") {type=value=="LTYPE";name="";}
            if(!type)continue; if(code=="2")name=value;
            if(code=="74" && value!="0" && name.StartsWith("Mr.T_"))flags[name]=Int32.Parse(value);
        }
        return flags;
    }
    private static void AddAngleCoupons(Database db,Profile p,string root)
    {
        var cases=new[]{p.Rows.First(r=>r.SystemCode=="OXY"),p.Rows.First(r=>r.SystemCode=="VAC"),p.Rows.First(r=>r.SystemCode=="CO2"),p.Rows.First(r=>r.SystemCode=="AGSS")}.Select(r=>r.Copy()).ToList();
        var hdpe=cases[0].Copy();hdpe.SystemCode="HDPE";hdpe.LabelPrefix="HDPE";hdpe.Diameter=110;hdpe.CustomText=null;hdpe.Description=null;cases.Add(hdpe);
        var c=new Profile {Rows=cases,AutoFitOnSync=true}; SyncService.Apply(db,c);
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
            for(int j=0;j<cases.Count;j++) for(int a=0;a<4;a++) foreach(int sign in new[]{1,-1})
            {
                var origin=new Point3d(100+a*65,30-j*65-(sign<0?3:0),0);double angle=a*Math.PI/4;
                var end=origin+new Vector3d(Math.Cos(angle)*40,Math.Sin(angle)*40,0);
                var line=new Line(sign>0?origin:end,sign>0?end:origin);line.SetDatabaseDefaults(db);ms.AppendEntity(line);tr.AddNewlyCreatedDBObject(line,true);
                line.LayerId=layers[c.LayerNameFor(cases[j])];line.LinetypeId=db.ByLayerLinetype;
                line.Color=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer,256);
            }
            tr.Commit();
        }
        log.Add("VISUAL QA REQUIRED: inspect separate saved DWG; U=0 at 0/180, 45/225, 90/270 and 135/315 degrees, including D110.HDPE. Numeric flag PASS does not prove glyph clearance.");
    }
}
