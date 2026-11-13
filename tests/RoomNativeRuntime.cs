using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;
[assembly:CommandClass(typeof(RoomNativeRuntime))]
public static class RoomNativeRuntime
{
    static void Check(bool ok,string text){if(!ok)throw new System.Exception(text);}
    [CommandMethod("MRTROOMS2016TEST",CommandFlags.Modal)]public static void Run()
    {
        var old=HostApplicationServices.WorkingDatabase;var output=new List<string>();try{using(var db=new Database(true,true)){
            HostApplicationServices.WorkingDatabase=db;var entry=new RoomEntry{Id="PHÒNG KHÁM",OldName="PHÒNG KHÁM",NewName="PHÒNG ĐIỀU TRỊ"};var id=RoomService.Place(db,entry,new Point3d(50,100,0),Vector3d.ZAxis,Vector3d.XAxis,25,false);
            var plan=RoomService.Inspect(db,null,true);Check(plan.Rows.Count==1&&plan.Rows[0].Ready,"Inserted managed room must be found.");Check(RoomService.Apply(db,plan)==1,"Switch to new name");
            using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var m=(MText)tr.GetObject(id,OpenMode.ForRead);Check(m.Text==entry.NewName&&m.Location==new Point3d(50,100,0)&&m.TextHeight==25,"Text changes but location/height stay unchanged");}
            var catalog=RoomCatalog.Build(new[]{new RoomEntry{Id="PHÒNG KHÁM",OldName="PHÒNG CŨ ĐỔI",NewName="PHÒNG MỚI ĐỔI"}});plan=RoomService.Inspect(db,catalog,true);Check(plan.Rows[0].RoomId==entry.Id&&plan.Rows[0].After=="PHÒNG MỚI ĐỔI","Excel refresh uses immutable original ID");RoomService.Apply(db,plan);
            plan=RoomService.Inspect(db,null,false);Check(plan.Rows[0].After=="PHÒNG CŨ ĐỔI","Stored updated old name can be restored without Excel");RoomService.Apply(db,plan);
            using(var tr=db.TransactionManager.StartTransaction()){var m=(MText)tr.GetObject(id,OpenMode.ForWrite);m.Contents="SỬA THỦ CÔNG";tr.Commit();}plan=RoomService.Inspect(db,catalog,true);Check(plan.Rows.Count==1&&!plan.Rows[0].Ready&&plan.Rows[0].Reason.Contains("thủ công"),"Manual edit must be skipped");
            output.Add("RESULT: PASS — create, ID, Excel refresh, switch, geometry, manual edit skip.");
        }}catch(System.Exception ex){output.Add("RESULT: FAIL "+ex);}finally{HostApplicationServices.WorkingDatabase=old;File.WriteAllLines(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"RoomNativeRuntime.txt"),output);var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",output)+"\n");}
    }
}
