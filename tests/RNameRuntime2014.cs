using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Engine;
using MedGas2024.Core;
[assembly:CommandClass(typeof(RNameRuntime2014))]
public static class RNameRuntime2014
{
    static void Check(bool ok,string name){if(!ok)throw new System.Exception(name);}
    [CommandMethod("MRTRNAME2014TEST",CommandFlags.Modal)]public static void Run()
    {
        var previous=HostApplicationServices.WorkingDatabase;string report="RESULT: FAIL — fixture did not complete.";
        try{using(var db=new Database(false,true)){
            string input=Environment.GetEnvironmentVariable("MRT_RNAME_TEST_DWG");if(String.IsNullOrWhiteSpace(input))throw new System.Exception("MRT_RNAME_TEST_DWG is missing.");db.ReadDwgFile(input,FileOpenMode.OpenForReadAndAllShare,true,null);db.CloseInput(true);HostApplicationServices.WorkingDatabase=db;
            var plan=RNameService.Inspect(db);var row=plan.Rows.Single(x=>x.Key=="Mr.T_OXY_D12_RAW");Check(row.Ready&&row.Target=="Mr.T_OXY_D12_RAW"&&row.Variants>1,"Actual DWG must contain the Oxy family and conflicting variants.");
            var before=LabelService.Read(db).Members.Where(x=>RNameRules.LabelKey(x.Text)=="D12.OXY").ToDictionary(x=>x.Handle,x=>x.Scale,StringComparer.OrdinalIgnoreCase);Check(before.Count>0,"Fixture must have Oxy D12 lines.");
            var source=plan.Actions.Single(x=>x.Row.Key==row.Key);double expected;using(var tr=db.TransactionManager.StartOpenCloseTransaction()){expected=((LinetypeTableRecord)tr.GetObject(source.Standard,OpenMode.ForRead)).PatternLength;}
            bool rolled=false;try{RNameService.Apply(db,plan,new[]{row.Key},true);}catch(InvalidOperationException){rolled=true;}Check(rolled&&RNameService.Inspect(db).Fingerprint==plan.Fingerprint,"Injected failure must roll back every write.");
            var result=RNameService.Apply(db,plan,new[]{row.Key});Check(result.Families==1,"One family applied.");
            using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);var reference=(LayerTableRecord)tr.GetObject(layers[row.StandardLayer],OpenMode.ForRead);var t=(LinetypeTableRecord)tr.GetObject(reference.LinetypeObjectId,OpenMode.ForRead);Check(t.Name==row.Target&&Math.Abs(t.PatternLength-expected)<1e-8,"Clean name uses active standard layer definition.");foreach(string legacy in new[]{"Mr.T Oxy D12","Mr.T Oxy D12 Bổ sung","Mr.T Oxy D12 TK mới"})if(layers.Has(legacy))Check(((LayerTableRecord)tr.GetObject(layers[legacy],OpenMode.ForRead)).LinetypeObjectId==t.ObjectId,"Legacy Oxy layer must use the standard type: "+legacy);foreach(var x in before){var id=db.GetObjectId(false,new Handle(Convert.ToInt64(x.Key,16)),0);if(id.IsErased)continue;var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e!=null)Check(Math.Abs(e.LinetypeScale-x.Value)<1e-10,"Entity linetype scale must remain unchanged.");}}
            report="RESULT: PASS — chosen canonical layer, legacy layers, scale preservation and rollback. Source DWG was not saved.";
        }}catch(System.Exception ex){report="RESULT: FAIL "+ex;}finally{HostApplicationServices.WorkingDatabase=previous;var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+report+"\n");}
    }
}
