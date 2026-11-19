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
    static void BlockRoundTrip(Database db,ObjectId managedText)
    {
        ObjectId firstAttr=ObjectId.Null;
        const string source="ROOM_SOURCE_TEST";
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForWrite);
            var layer=new LayerTableRecord{Name=source};layers.Add(layer);tr.AddNewlyCreatedDBObject(layer,true);
            var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);
            var inner=new BlockTableRecord{Name="ROOM_INNER_TEST"};table.Add(inner);tr.AddNewlyCreatedDBObject(inner,true);
            var text=new DBText{TextString="Phòng chung",Position=new Point3d(2,3,0),Height=2};
            inner.AppendEntity(text);tr.AddNewlyCreatedDBObject(text,true);
            var outer=new BlockTableRecord{Name="ROOM_OUTER_TEST"};table.Add(outer);tr.AddNewlyCreatedDBObject(outer,true);
            var nested=new BlockReference(new Point3d(5,0,0),inner.ObjectId);outer.AppendEntity(nested);tr.AddNewlyCreatedDBObject(nested,true);
            var def=new AttributeDefinition{Tag="ROOM",TextString="Room",Position=Point3d.Origin,Height=2};
            outer.AppendEntity(def);tr.AddNewlyCreatedDBObject(def,true);
            var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);
            for(int i=0;i<2;i++){
                var br=new BlockReference(new Point3d(100*i,10,0),outer.ObjectId){LayerId=layer.ObjectId};
                space.AppendEntity(br);tr.AddNewlyCreatedDBObject(br,true);
                var attr=new AttributeReference();attr.SetAttributeFromBlock(def,br.BlockTransform);attr.TextString=i==0?"Trực A":"Trực B";
                br.AttributeCollection.AppendAttribute(attr);tr.AddNewlyCreatedDBObject(attr,true);
                if(i==0)firstAttr=attr.ObjectId;
            }
            tr.Commit();
        }
        var catalog=RoomCatalog.Build(new[]{new RoomEntry{Id="001",OldName="Trực A",NewName="Phòng bác sĩ"}});
        var hits=RoomNameMatcher.Match(RoomService.ScanNames(db,db.CurrentSpaceId,source),catalog,true);
        var shared=hits.Where(h=>h.Text=="Phòng chung").ToList();
        Check(shared.Count==2&&shared.Select(h=>h.Address).Distinct().Count()==2&&shared.All(h=>h.Shared&&!h.Ready),"Two nested instances have distinct placement addresses");
        Check(shared.Select(h=>new Point3d(2,3,0).TransformBy(h.ToWorld).X).OrderBy(x=>x).SequenceEqual(new[]{7d,107d}),"Nested positions include both INSERT transforms");
        var choice=hits.Single(h=>h.Ready);
        Check(choice.Id==firstAttr&&choice.Layer==source,"Layer 0 attribute inherits the insertion layer");
        RoomService.Adopt(db,db.CurrentSpaceId,new[]{choice},true,RoomCatalog.DefaultLayer,RoomCatalog.DefaultStyle);
        var tracked=RoomService.Place(db,new RoomEntry{Id="RT-007",OldName="Phòng gốc",NewName="Tên đang hiển thị"},
            new Point3d(4,6,0),Vector3d.ZAxis,Vector3d.XAxis,25,true);
        var file=Path.Combine(Path.GetTempPath(),"MrT-room-"+Guid.NewGuid().ToString("N")+".dwg");
        try{
            db.SaveAs(file,DwgVersion.Current);
            using(var reopened=new Database(false,true)){
                reopened.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,true,null);reopened.CloseInput(true);
                var attr=reopened.GetObjectId(false,firstAttr.Handle,0);
                var id=reopened.GetObjectId(false,managedText.Handle,0);
                var room=RoomService.ReadTag(reopened,id);
                Check(room.Id=="PHÒNG KHÁM"&&room.Original=="PHÒNG KHÁM","MTEXT original ID survives save and reopen");
                var tag=RoomService.ReadTag(reopened,attr);
                Check(tag.Id=="001"&&tag.Original=="Trực A"&&tag.NewName=="Phòng bác sĩ","Instance attribute retains original ID and names after reopen");
                var updated=RoomCatalog.Build(new[]{new RoomEntry{Id="RT-007",OldName="Tên cũ đã sửa",NewName="Tên mới từ Excel"}});
                var mt=reopened.GetObjectId(false,tracked.Handle,0);
                var pending=RoomService.Inspect(reopened,updated,true);
                Check(pending.Rows.Single(h=>h.Id==mt).Ready,"Find reopened MTEXT by ID independently of its current display");
                RoomService.Apply(reopened,pending);
                using(var tr=reopened.TransactionManager.StartOpenCloseTransaction())
                    Check(((MText)tr.GetObject(mt,OpenMode.ForRead)).Text=="Tên mới từ Excel","Reopened MTEXT follows a revised Excel row");
                Check(RoomService.ReadTag(reopened,mt).Original=="Phòng gốc","Original name survives subsequent Excel updates");
                var plan=RoomService.Inspect(reopened,catalog,false);
                Check(plan.Rows.Single(h=>h.Id==attr).Ready,"Reopened instance is found by its stored ID");
                RoomService.Apply(reopened,plan);
                using(var tr=reopened.TransactionManager.StartOpenCloseTransaction())
                    Check(((AttributeReference)tr.GetObject(attr,OpenMode.ForRead)).TextString=="Trực A","Switch updates the reopened instance");
            }
        }finally{if(File.Exists(file))File.Delete(file);}
    }
    [CommandMethod("MRTROOMS2016TEST",CommandFlags.Modal)]public static void Run()
    {
        var old=HostApplicationServices.WorkingDatabase;var output=new List<string>();try{using(var db=new Database(true,true)){
            HostApplicationServices.WorkingDatabase=db;var entry=new RoomEntry{Id="PHÒNG KHÁM",OldName="PHÒNG KHÁM",NewName="PHÒNG ĐIỀU TRỊ"};var id=RoomService.Place(db,entry,new Point3d(50,100,0),Vector3d.ZAxis,Vector3d.XAxis,25,false);
            var plan=RoomService.Inspect(db,null,true);Check(plan.Rows.Count==1&&plan.Rows[0].Ready,"Inserted managed room must be found.");Check(RoomService.Apply(db,plan)==1,"Switch to new name");
            using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var m=(MText)tr.GetObject(id,OpenMode.ForRead);Check(m.Text==entry.NewName&&m.Location==new Point3d(50,100,0)&&m.TextHeight==25,"Text changes but location/height stay unchanged");}
            var catalog=RoomCatalog.Build(new[]{new RoomEntry{Id="PHÒNG KHÁM",OldName="PHÒNG CŨ ĐỔI",NewName="PHÒNG MỚI ĐỔI"}});plan=RoomService.Inspect(db,catalog,true);Check(plan.Rows[0].RoomId==entry.Id&&plan.Rows[0].After=="PHÒNG MỚI ĐỔI","Excel refresh uses immutable original ID");RoomService.Apply(db,plan);
            plan=RoomService.Inspect(db,null,false);Check(plan.Rows[0].After=="PHÒNG CŨ ĐỔI","Stored updated old name can be restored without Excel");RoomService.Apply(db,plan);
            using(var tr=db.TransactionManager.StartTransaction()){var m=(MText)tr.GetObject(id,OpenMode.ForWrite);m.Contents="SỬA THỦ CÔNG";tr.Commit();}plan=RoomService.Inspect(db,catalog,true);Check(plan.Rows.Count==1&&!plan.Rows[0].Ready&&plan.Rows[0].Reason.Contains("thủ công"),"Manual edit must be skipped");
            ObjectId legacy;
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var owner=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);
                var txt=new DBText{TextString="Trực",Position=new Point3d(10,25,0),Rotation=.3,Height=12};
                legacy=owner.AppendEntity(txt);tr.AddNewlyCreatedDBObject(txt,true);tr.Commit();
            }
            var list=RoomCatalog.Build(new[]{new RoomEntry{Id="T2",OldName="TRỰC",NewName="Trực ngày"}});
            var hits=RoomNameMatcher.Match(RoomService.ScanNames(db,db.CurrentSpaceId,"0"),list,true);
            var one=hits.Single(h=>h.Ready);
            Check(one.Id==legacy,"Find unique legacy TEXT on specified layer");
            RoomService.Adopt(db,db.CurrentSpaceId,new[]{one},true,RoomCatalog.DefaultLayer,RoomCatalog.DefaultStyle);
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var t=(DBText)tr.GetObject(legacy,OpenMode.ForRead);
                Check(t.TextString=="Trực ngày"&&t.Layer==RoomCatalog.DefaultLayer&&t.Position==new Point3d(10,25,0)
                    &&Math.Abs(t.Rotation-.3)<1e-8&&t.Height==12,"Adoption preserves TEXT handle and geometry");
            }
            Check(!RoomNameMatcher.Match(RoomService.ScanNames(db,db.CurrentSpaceId,"0"),list,true).Any(h=>h.Ready),"Second scan is idempotent");
            RoomService.Apply(db,RoomService.Inspect(db,list,false));
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
                Check(((DBText)tr.GetObject(legacy,OpenMode.ForRead)).TextString=="TRỰC","Adopted TEXT participates in old/new switching");
            BlockRoundTrip(db,id);
            output.Add("RESULT: PASS — create, ID, Excel refresh, switch, geometry, manual edit skip.");
        }}catch(System.Exception ex){output.Add("RESULT: FAIL "+ex);}finally{HostApplicationServices.WorkingDatabase=old;File.WriteAllLines(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"RoomNativeRuntime.txt"),output);var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",output)+"\n");}
    }
}
