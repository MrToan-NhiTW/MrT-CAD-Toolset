using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;
[assembly:CommandClass(typeof(VietnameseRuntimeTests))]
public static class VietnameseRuntimeTests
{
    [CommandMethod("MRTVIETTEST",CommandFlags.Modal)]
    public static void Run()
    {
        string root=Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName;var log=new List<string>();var previous=HostApplicationServices.WorkingDatabase;
        try
        {
            using(var db=new Database(true,true))
            {
                HostApplicationServices.WorkingDatabase=db;
                var rows=new List<GasRow>();foreach(string mode in new[]{"R","U"})rows.Add(new GasRow{Enabled=true,SystemCode="ỐNG KHÍ "+mode,LabelPrefix="Khí nén",Diameter=22,Dash=10,GapBefore=8,GapAfter=8,TextScale=2.5,RotationMode=mode,ColorIndex=7});
                var p=new Profile{Rows=rows,FontFile="arial.ttf",FontBold=false};p.RefreshNames();SyncService.Apply(db,p);
                using(var tr=db.TransactionManager.StartOpenCloseTransaction())
                {
                    var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                    foreach(var row in rows){var layer=(LayerTableRecord)tr.GetObject(layers[p.LayerNameFor(row)],OpenMode.ForRead);var type=(LinetypeTableRecord)tr.GetObject(types[p.NameFor(row)],OpenMode.ForRead);if(layer.LinetypeObjectId!=type.ObjectId||type.TextAt(1)!=row.Text)throw new System.Exception("Vietnamese label or association changed for "+row.RotationMode);log.Add("PASS: "+row.RotationMode+" Unicode layer, label and linetype association");}
                }
                db.SaveAs(Path.Combine(root,"evidence","vietnamese-runtime-test.dwg"),DwgVersion.AC1032);
            }
            log.Add("RESULT: PASS; inspect visible glyphs separately in AutoCAD.");
        }
        catch(System.Exception ex){log.Add("RESULT: FAIL");log.Add(ex.ToString());}
        finally{HostApplicationServices.WorkingDatabase=previous;File.WriteAllLines(Path.Combine(root,"evidence","vietnamese-runtime-test.txt"),log);var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",log)+"\n");}
    }
}
