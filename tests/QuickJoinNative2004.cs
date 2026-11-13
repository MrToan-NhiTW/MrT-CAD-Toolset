using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Engine;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;

public class QuickJoinNative2004
{
    [CommandMethod("MRTTESTJOIN2004")]
    public void Test()
    {
        var doc=AcApp.DocumentManager.MdiActiveDocument;
        var report=new List<string>();
        try{
            var ids=new List<ObjectId>();
            using(var tr=doc.Database.TransactionManager.StartTransaction()){
                var bt=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);
                var space=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);
                foreach(ObjectId id in space)if(tr.GetObject(id,OpenMode.ForRead) is Polyline)ids.Add(id);
            }
            if(ids.Count!=5)throw new System.Exception("Expected 5 polylines, found "+ids.Count);
            var p=QuickJoinService.Inspect(doc.Database,ids.ToArray(),.5,false);
            if(p.Links!=4||p.Chains.Count!=1||p.Chains[0].Count!=5)throw new System.Exception("Inspection failed: links="+p.Links+" chains="+p.Chains.Count+" "+p.Summary());
            var changed=QuickJoinService.Apply(doc.Database,p,false);
            if(changed.Count!=5)throw new System.Exception("Replacements="+changed.Count);
            using(var tr=doc.Database.TransactionManager.StartTransaction()){
                var e=(Entity)tr.GetObject(changed[ids[0]][0],OpenMode.ForRead);
                using(var x=e.XData){if(x==null||x.AsArray().Length!=2||(string)x.AsArray()[0].Value!="AcDbBlockRepETag")throw new System.Exception("ETag was not retained");}
            }
            report.Add("PASS: 5 original DXF polylines formed one output; 4 joins; empty ETag retained.");
        }catch(System.Exception ex){report.Add("FAIL: "+ex);}
        string target=Path.Combine(Path.GetDirectoryName(typeof(QuickJoinNative2004).Assembly.Location),"join-native-2004-result.txt");
        File.WriteAllLines(target,report);doc.Editor.WriteMessage("\n"+String.Join("\n",report.ToArray())+"\n");
    }
}
