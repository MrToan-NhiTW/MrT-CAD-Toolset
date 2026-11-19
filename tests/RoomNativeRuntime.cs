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
    static bool Near(Point3d a,Point3d b){return a.DistanceTo(b)<1e-6;}
    static Point3d Center(Entity e){var x=e.GeometricExtents;return x.MinPoint+(x.MaxPoint-x.MinPoint)*0.5;}
    // Issue #6 acceptance: 510 in a nested block (outer insert rotated 90°, scale 2),
    // 520 in a block inserted twice (0° and 45°), Excel 510→610 and 520→620.
    static void BlockLabels(Database db,List<string> output)
    {
        const string source="ROOM_OLD_LABEL";
        ObjectId t510,t520,outer,nested,twiceA,twiceB;
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForWrite);
            var layer=new LayerTableRecord{Name=source};layers.Add(layer);tr.AddNewlyCreatedDBObject(layer,true);
            var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);
            var inner=new BlockTableRecord{Name="LBL_INNER"};table.Add(inner);tr.AddNewlyCreatedDBObject(inner,true);
            var a=new DBText{TextString="510",Position=new Point3d(2,3,0),Height=2,LayerId=layer.ObjectId};inner.AppendEntity(a);tr.AddNewlyCreatedDBObject(a,true);t510=a.ObjectId;
            var outerDef=new BlockTableRecord{Name="LBL_OUTER"};table.Add(outerDef);tr.AddNewlyCreatedDBObject(outerDef,true);
            var n=new BlockReference(new Point3d(5,0,0),inner.ObjectId);outerDef.AppendEntity(n);tr.AddNewlyCreatedDBObject(n,true);nested=n.ObjectId;
            var twice=new BlockTableRecord{Name="LBL_TWICE"};table.Add(twice);tr.AddNewlyCreatedDBObject(twice,true);
            var b=new DBText{TextString="520",Position=new Point3d(1,1,0),Height=3,Rotation=0.25,LayerId=layer.ObjectId};twice.AppendEntity(b);tr.AddNewlyCreatedDBObject(b,true);t520=b.ObjectId;
            var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);
            var o=new BlockReference(new Point3d(1000,500,0),outerDef.ObjectId){Rotation=Math.PI/2,ScaleFactors=new Scale3d(2)};space.AppendEntity(o);tr.AddNewlyCreatedDBObject(o,true);outer=o.ObjectId;
            var i1=new BlockReference(new Point3d(2000,0,0),twice.ObjectId);space.AppendEntity(i1);tr.AddNewlyCreatedDBObject(i1,true);twiceA=i1.ObjectId;
            var i2=new BlockReference(new Point3d(3000,0,0),twice.ObjectId){Rotation=Math.PI/4};space.AppendEntity(i2);tr.AddNewlyCreatedDBObject(i2,true);twiceB=i2.ObjectId;
            tr.Commit();
        }
        Point3d expect510,expectA,expectB;Matrix3d m510,mA,mB;
        using(var tr=db.TransactionManager.StartOpenCloseTransaction())
        {
            Func<ObjectId,BlockReference> br=id=>(BlockReference)tr.GetObject(id,OpenMode.ForRead);
            m510=br(outer).BlockTransform*br(nested).BlockTransform;mA=br(twiceA).BlockTransform;mB=br(twiceB).BlockTransform;
            expect510=Center((Entity)tr.GetObject(t510,OpenMode.ForRead)).TransformBy(m510);
            var c520=Center((Entity)tr.GetObject(t520,OpenMode.ForRead));expectA=c520.TransformBy(mA);expectB=c520.TransformBy(mB);
        }
        var catalog=RoomCatalog.Build(new[]{new RoomEntry{Id="510",OldName="510",NewName="610"},new RoomEntry{Id="520",OldName="520",NewName="620"}});
        Func<RoomMatchOptions,List<RoomHit>> match=o=>RoomNameMatcher.Match(RoomService.ScanNames(db,db.CurrentSpaceId,source),catalog,true,o);
        var labels=new RoomMatchOptions{CreateLabels=true};var all=new RoomMatchOptions{CreateLabels=true,AllowRepeats=true};
        var first=match(labels);
        var h510=first.Single(h=>h.Text=="510");
        Check(h510.Ready&&h510.NeedsLabel&&h510.Action==RoomNameMatcher.ActionLabel&&h510.Entry.NewName=="610","Nested 510 is ready to get its own label (preview 510 → 610)");
        Check(first.Where(h=>h.Text=="520").All(h=>!h.Ready&&h.Status.StartsWith("2 vị trí")),"520 in two inserts asks for a choice");
        var ready=match(all).Where(h=>h.Ready).ToList();
        Check(ready.Count==3,"Apply-all makes 3 labels ready");
        var done=RoomService.ApplyMatches(db,db.CurrentSpaceId,ready,true,RoomCatalog.DefaultLayer,RoomCatalog.DefaultStyle,true,null);
        Check(done.Labels==3&&done.Adopted==0,"Three labels created in one transaction");
        using(var tr=db.TransactionManager.StartOpenCloseTransaction())
        {
            var l510=(MText)tr.GetObject(ready.Single(h=>h.Text=="510").Label,OpenMode.ForRead);
            Check(Near(l510.Location,expect510)&&l510.Text=="610"&&Math.Abs(l510.TextHeight-4)<1e-9&&l510.Direction.IsEqualTo(Vector3d.YAxis,new Tolerance(1e-9,1e-9))
                &&l510.Layer==RoomCatalog.DefaultLayer&&l510.BackgroundFill,"510 label sits at the nested WCS centre, scaled ×2 and turned 90°");
            var placed=ready.Where(h=>h.Text=="520").Select(h=>(MText)tr.GetObject(h.Label,OpenMode.ForRead)).ToList();
            Check(placed.Any(m=>Near(m.Location,expectA))&&placed.Any(m=>Near(m.Location,expectB)),"Each insert of 520 keeps its own position");
            var turned=placed.Single(m=>Near(m.Location,expectB));
            Check(Math.Abs(turned.Rotation-(0.25+Math.PI/4))<1e-9&&Math.Abs(turned.TextHeight-3)<1e-9,"Label follows the insert rotation plus the text rotation");
            var tag=RoomService.ReadTag(db,l510.ObjectId);
            Check(tag.Id=="510"&&tag.NewName=="610"&&tag.Source==ready.Single(h=>h.Text=="510").Address,"Label carries MRT_ROOM_V1 with the ID and the block text address");
        }
        Check(!match(all).Any(h=>h.Ready)&&!match(labels).Any(h=>h.Ready),"Running again creates no duplicate labels");
        var file=Path.Combine(Path.GetTempPath(),"MrT-room-labels-"+Guid.NewGuid().ToString("N")+".dwg");
        var ids=ready.Select(h=>h.Label.Handle).ToList();
        try
        {
            db.SaveAs(file,DwgVersion.Current);
            using(var reopened=new Database(false,true))
            {
                reopened.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,true,null);reopened.CloseInput(true);
                Check(!RoomNameMatcher.Match(RoomService.ScanNames(reopened,reopened.CurrentSpaceId,source),catalog,true,all).Any(h=>h.Ready),"After reopening, a rerun still creates no duplicates");
                var revised=RoomCatalog.Build(new[]{new RoomEntry{Id="510",OldName="510",NewName="611"},new RoomEntry{Id="520",OldName="520",NewName="620"}});
                var plan=RoomService.Inspect(reopened,revised,true);
                var mine=plan.Rows.Where(r=>ids.Contains(r.Id.Handle)).ToList();
                Check(mine.Count==3&&mine.All(r=>r.Ready),"Update finds all three labels by ID after reopening");
                RoomService.Apply(reopened,plan);
                var id510=reopened.GetObjectId(false,ready.Single(h=>h.Text=="510").Label.Handle,0);
                using(var tr=reopened.TransactionManager.StartOpenCloseTransaction())
                    Check(((MText)tr.GetObject(id510,OpenMode.ForRead)).Text=="611","Revised Excel row updates the reopened label");
                var back=RoomService.Inspect(reopened,revised,false);RoomService.Apply(reopened,back);
                using(var tr=reopened.TransactionManager.StartOpenCloseTransaction())
                    Check(((MText)tr.GetObject(id510,OpenMode.ForRead)).Text=="510","Label switches back to the old name");
            }
        }
        finally{if(File.Exists(file))File.Delete(file);}
        // Turning the source layer off is part of the same apply.
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);
            var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
            var o=new BlockReference(new Point3d(5000,0,0),table["LBL_OUTER"]);space.AppendEntity(o);tr.AddNewlyCreatedDBObject(o,true);tr.Commit();
        }
        var extra=RoomNameMatcher.Match(RoomService.ScanNames(db,db.CurrentSpaceId,source),catalog,true,all).Where(h=>h.Ready).ToList();
        Check(extra.Count==1&&extra[0].Text=="510","A new insert of the same block gets only its own label");
        var hid=RoomService.ApplyMatches(db,db.CurrentSpaceId,extra,true,RoomCatalog.NewLayer,RoomCatalog.DefaultStyle,false,source);
        using(var tr=db.TransactionManager.StartOpenCloseTransaction())
        {
            var table=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
            Check(hid.HiddenLayer==source&&((LayerTableRecord)tr.GetObject(table[source],OpenMode.ForRead)).IsOff,"Old text layer is turned off only when asked");
            Check(((Entity)tr.GetObject(extra[0].Label,OpenMode.ForRead)).Layer==RoomCatalog.NewLayer&&!((LayerTableRecord)tr.GetObject(table[RoomCatalog.NewLayer],OpenMode.ForRead)).IsOff,"The separate new-name layer is created and stays visible");
        }
        output.Add("PASS: block labels — nested/rotated/scaled positions, repeats, no duplicates, reopen, update, switch, hide layer.");
    }
    // UNDO check on the active drawing: FIX builds a block with 515, APPLY labels it in its
    // own command, the script runs UNDO 1, CHECK expects the block kept and the label gone.
    static void Say(string text)
    {
        File.AppendAllLines(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"RoomNativeUndo.txt"),new[]{text});
        var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+text+"\n");
    }
    static int Labels(Database db)
    {
        using(var tr=db.TransactionManager.StartOpenCloseTransaction())
        {
            var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForRead);
            return space.Cast<ObjectId>().Count(id=>{var tag=RoomService.ReadTag(db,id);return tag!=null&&tag.Id=="515";});
        }
    }
    [CommandMethod("MRTROOMSUNDOFIX",CommandFlags.Modal)]public static void UndoFixture()
    {
        var db=HostApplicationServices.WorkingDatabase;
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);
            var def=new BlockTableRecord{Name="UNDO_ROOM"};table.Add(def);tr.AddNewlyCreatedDBObject(def,true);
            var t=new DBText{TextString="515",Position=Point3d.Origin,Height=2};def.AppendEntity(t);tr.AddNewlyCreatedDBObject(t,true);
            var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);
            var br=new BlockReference(new Point3d(50,50,0),def.ObjectId);space.AppendEntity(br);tr.AddNewlyCreatedDBObject(br,true);tr.Commit();
        }
        Say("UNDO fixture ready");
    }
    [CommandMethod("MRTROOMSUNDOAPPLY",CommandFlags.Modal)]public static void UndoApply()
    {
        var db=HostApplicationServices.WorkingDatabase;
        var catalog=RoomCatalog.Build(new[]{new RoomEntry{Id="515",OldName="515",NewName="615"}});
        var ready=RoomNameMatcher.Match(RoomService.ScanNames(db,db.CurrentSpaceId,null),catalog,true,new RoomMatchOptions{CreateLabels=true}).Where(h=>h.Ready).ToList();
        var done=RoomService.ApplyMatches(db,db.CurrentSpaceId,ready,true,RoomCatalog.DefaultLayer,RoomCatalog.DefaultStyle,false,null);
        Say("UNDO apply labels="+done.Labels+" count="+Labels(db));
    }
    [CommandMethod("MRTROOMSUNDOCHECK",CommandFlags.Modal)]public static void UndoCheck()
    {
        var db=HostApplicationServices.WorkingDatabase;bool block;
        using(var tr=db.TransactionManager.StartOpenCloseTransaction())block=((BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead)).Has("UNDO_ROOM");
        int n=Labels(db);
        Say((block&&n==0?"RESULT: PASS":"RESULT: FAIL")+" UNDO removes the label (labels="+n+", block kept="+block+")");
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
            BlockLabels(db,output);
            output.Add("RESULT: PASS — create, ID, Excel refresh, switch, geometry, manual edit skip.");
        }}catch(System.Exception ex){output.Add("RESULT: FAIL "+ex);}finally{HostApplicationServices.WorkingDatabase=old;File.WriteAllLines(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"RoomNativeRuntime.txt"),output);var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",output)+"\n");}
    }
}
