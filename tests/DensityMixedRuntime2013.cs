using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;
[assembly:CommandClass(typeof(DensityMixedRuntime2013))]
public static class DensityMixedRuntime2013
{
    static void Check(bool x,string message){if(!x)throw new System.Exception(message);}
    [CommandMethod("MRTDENSITY2013TEST",CommandFlags.Modal)]public static void Run()
    {
        var log=new List<string>();var previous=HostApplicationServices.WorkingDatabase;
        try{using(var db=new Database(false,true)){
            var path=Environment.GetEnvironmentVariable("MRT_DENSITY_TEST_DWG");if(String.IsNullOrEmpty(path))throw new System.Exception("Set MRT_DENSITY_TEST_DWG to the copied fixture.");
            db.ReadDwgFile(path,FileOpenMode.OpenForReadAndAllShare,true,null);db.CloseInput(true);HostApplicationServices.WorkingDatabase=db;
            var layers=new[]{"Mr.T_CO2_D22_RAW","Mr.T_VAC_D28_RAW","Mr.T_OXY_D22_RAW"};var before=DensityService.Scan(db).Where(r=>!r.IsOverride&&layers.Contains(r.Layer)).ToList();Check(before.Count==3,"Expected three named pipe layers.");
            var originalScales=before.SelectMany(r=>r.Members).ToDictionary(m=>m.Handle,m=>m.Scale,StringComparer.OrdinalIgnoreCase);Check(before.Single(r=>r.Layer==layers[1]).Members.Select(m=>m.Scale).Distinct().Count()>1,"Fixture must contain mixed VAC scales.");
            var options=new DensityOptions{PaperMode=true,PaperMm=25,Denominator=200};var keys=before.Select(r=>r.Key).ToList();var plan=DensityService.Preview(db,keys,options);Check(plan.Rows.All(r=>r.After!="—"),"All fixture groups must be applicable.");Check(plan.Overrides.Any(x=>x.FromLayer),"Mixed scale must create explicit variants.");
            Check(DensityService.Apply(db,plan,options,keys)==3,"All three groups must apply.");
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())foreach(var member in originalScales){var id=db.GetObjectId(false,new Handle(Convert.ToInt64(member.Key,16)),0);var entity=(Entity)tr.GetObject(id,OpenMode.ForRead);Check(Math.Abs(entity.LinetypeScale-member.Value)<1e-10,"Linetype scale changed unexpectedly.");var layer=(LayerTableRecord)tr.GetObject(entity.LayerId,OpenMode.ForRead);var lt=(LinetypeTableRecord)tr.GetObject(entity.LinetypeId==db.ByLayerLinetype?layer.LinetypeObjectId:entity.LinetypeId,OpenMode.ForRead);var paper=lt.PatternLength*entity.LinetypeScale*db.Ltscale/200;Check(Math.Abs(paper-25)<1e-6,"Entity does not have target paper estimate: "+member.Key+" = "+paper);Check(!lt.Name.Contains("_D_"),"New active density name retains hash chain.");}
            log.Add("RESULT: PASS — actual drawing mixed scales, readable names, unchanged entity scales, 25 mm model estimate. No DWG saved.");
        }}catch(System.Exception ex){log.Add("RESULT: FAIL "+ex);}finally{HostApplicationServices.WorkingDatabase=previous;var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",log)+"\n");}
    }
}
