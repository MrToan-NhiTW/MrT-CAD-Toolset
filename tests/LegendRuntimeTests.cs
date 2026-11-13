using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;
[assembly:CommandClass(typeof(LegendRuntimeTests))]
public static class LegendRuntimeTests
{
    static List<string> log=new List<string>();
    static void Check(bool ok,string text){if(!ok)throw new System.Exception(text);log.Add("PASS: "+text);}
    static int BlockCount(Database db){using(var tr=db.TransactionManager.StartOpenCloseTransaction())return ((BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead)).Cast<ObjectId>().Count();}
    static void HatchSamples(Database db)
    {
        ObjectId source, border, block, nestedSource;Extents3d originalBounds;
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);var model=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);
            var loop=new Polyline();loop.AddVertexAt(0,new Point2d(0,0),0,0,0);loop.AddVertexAt(1,new Point2d(10,0),0,0,0);loop.AddVertexAt(2,new Point2d(10,10),0,0,0);loop.AddVertexAt(3,new Point2d(0,10),0,0,0);loop.Closed=true;model.AppendEntity(loop);tr.AddNewlyCreatedDBObject(loop,true);border=loop.ObjectId;
            var h=new Hatch();h.SetDatabaseDefaults(db);model.AppendEntity(h);tr.AddNewlyCreatedDBObject(h,true);h.SetHatchPattern(HatchPatternType.PreDefined,"ANSI31");h.PatternScale=1;h.Associative=true;h.AppendLoop(HatchLoopTypes.Outermost,new ObjectIdCollection(new[]{border}));h.EvaluateHatch(true);source=h.ObjectId;originalBounds=h.GeometricExtents;
            var def=new BlockTableRecord{Name="HATCH_SYMBOL"};bt.Add(def);tr.AddNewlyCreatedDBObject(def,true);
            var inner=new Polyline();inner.AddVertexAt(0,new Point2d(0,0),0,0,0);inner.AddVertexAt(1,new Point2d(5,0),0,0,0);inner.AddVertexAt(2,new Point2d(5,5),0,0,0);inner.AddVertexAt(3,new Point2d(0,5),0,0,0);inner.Closed=true;def.AppendEntity(inner);tr.AddNewlyCreatedDBObject(inner,true);
            var bh=new Hatch();bh.SetDatabaseDefaults(db);def.AppendEntity(bh);tr.AddNewlyCreatedDBObject(bh,true);bh.SetHatchPattern(HatchPatternType.PreDefined,"SOLID");bh.Associative=true;bh.AppendLoop(HatchLoopTypes.Outermost,new ObjectIdCollection(new[]{inner.ObjectId}));bh.EvaluateHatch(true);nestedSource=bh.ObjectId;
            var br=new BlockReference(new Point3d(40,20,0),def.ObjectId);model.AppendEntity(br);tr.AddNewlyCreatedDBObject(br,true);block=br.ObjectId;tr.Commit();
        }
        var direct=LegendService.Capture(db,new[]{source},false);var nested=LegendService.Capture(db,new[]{block},false);Check(direct!=null&&nested!=null,"Capture associative hatch alone and inside a block without eNotInDatabase");
        using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var h=(Hatch)tr.GetObject(source,OpenMode.ForRead);var bh=(Hatch)tr.GetObject(nestedSource,OpenMode.ForRead);Check(h.Associative&&bh.Associative&&h.NumberOfLoops==1&&h.GeometricExtents.MinPoint==originalBounds.MinPoint,"Capture retains original HATCH associations, geometry, and loop");}
        var s=new LegendSpec{DatabaseKey=LegendService.DatabaseKey(db),Groups=1};s.Rows.Add(direct);s.Rows.Add(nested);ObjectId table=LegendService.Write(db,s,new Point3d(500,400,0),Vector3d.ZAxis,Vector3d.XAxis,ObjectId.Null);
        using(var tr=db.TransactionManager.StartOpenCloseTransaction())
        {
            var t=(Table)tr.GetObject(table,OpenMode.ForRead);for(int i=0;i<2;i++){var sample=(BlockTableRecord)tr.GetObject(t.Cells[i+2,0].BlockTableRecordId,OpenMode.ForRead);var found=sample.Cast<ObjectId>().Select(id=>tr.GetObject(id,OpenMode.ForRead)).OfType<Hatch>().ToArray();Check(found.Length==1&&!found[0].Associative&&found[0].NumberOfLoops==1,"TABLE stores a non-associative native HATCH snapshot with its loop");}
            Check(((Hatch)tr.GetObject(source,OpenMode.ForRead)).Associative&&((Hatch)tr.GetObject(nestedSource,OpenMode.ForRead)).Associative&&!border.IsErased,"Creating the TABLE leaves the source associations and border intact");
        }
        var reopened=LegendService.Read(db,table);Check(reopened.Rows.Count==2,"The HATCH sample rows reopen after insertion");
    }
    static void MaskSamples(Database db)
    {
        ObjectId rear,mask,front,blockId,dimensionId,leaderId,unsupported;ObjectId[] sourceOrder;
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);var model=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);
            var back=new Line(new Point3d(-10,0,0),new Point3d(10,0,0));model.AppendEntity(back);tr.AddNewlyCreatedDBObject(back,true);rear=back.ObjectId;
            var wipe=new Wipeout();wipe.SetDatabaseDefaults(db);wipe.SetFrom(new Point2dCollection(new[]{new Point2d(-2,-2),new Point2d(2,-2),new Point2d(2,2),new Point2d(-2,2),new Point2d(-2,-2)}),Vector3d.ZAxis);model.AppendEntity(wipe);tr.AddNewlyCreatedDBObject(wipe,true);mask=wipe.ObjectId;
            var fore=new Line(new Point3d(0,-3,0),new Point3d(0,3,0));model.AppendEntity(fore);tr.AddNewlyCreatedDBObject(fore,true);front=fore.ObjectId;
            // Deliberately make display order differ from handle/insertion order.
            var order=(DrawOrderTable)tr.GetObject(model.DrawOrderTableId,OpenMode.ForWrite);order.MoveToTop(new ObjectIdCollection(new[]{rear}));order.MoveToTop(new ObjectIdCollection(new[]{front}));
            sourceOrder=order.GetFullDrawOrder(0).Cast<ObjectId>().ToArray();
            var definition=new BlockTableRecord{Name="MASKED_SYMBOL"};bt.Add(definition);tr.AddNewlyCreatedDBObject(definition,true);
            var backCopy=(Line)back.Clone();definition.AppendEntity(backCopy);tr.AddNewlyCreatedDBObject(backCopy,true);var wipeCopy=(Wipeout)wipe.Clone();definition.AppendEntity(wipeCopy);tr.AddNewlyCreatedDBObject(wipeCopy,true);var frontCopy=(Line)fore.Clone();definition.AppendEntity(frontCopy);tr.AddNewlyCreatedDBObject(frontCopy,true);
            var definitionOrder=(DrawOrderTable)tr.GetObject(definition.DrawOrderTableId,OpenMode.ForWrite);definitionOrder.MoveToTop(new ObjectIdCollection(new[]{backCopy.ObjectId}));definitionOrder.MoveToTop(new ObjectIdCollection(new[]{frontCopy.ObjectId}));
            var inner=new BlockReference(new Point3d(40,10,0),definition.ObjectId);model.AppendEntity(inner);tr.AddNewlyCreatedDBObject(inner,true);blockId=inner.ObjectId;
            var dimension=new RotatedDimension(0,new Point3d(0,20,0),new Point3d(10,20,0),new Point3d(0,25,0),"",db.Dimstyle);model.AppendEntity(dimension);tr.AddNewlyCreatedDBObject(dimension,true);dimension.RecomputeDimensionBlock(true);dimensionId=dimension.ObjectId;
            var leader=new MLeader();leader.SetDatabaseDefaults(db);leader.ContentType=ContentType.MTextContent;using(var text=new MText()){text.Contents="Chú thích";text.TextHeight=2;leader.MText=text;}leader.TextLocation=new Point3d(20,30,0);int l=leader.AddLeader();int ll=leader.AddLeaderLine(l);leader.AddFirstVertex(ll,new Point3d(10,28,0));model.AppendEntity(leader);tr.AddNewlyCreatedDBObject(leader,true);leaderId=leader.ObjectId;
            var face=new Face(new Point3d(0,0,0),new Point3d(3,0,0),new Point3d(3,3,1),new Point3d(0,3,1),true,true,true,true);model.AppendEntity(face);tr.AddNewlyCreatedDBObject(face,true);unsupported=face.ObjectId;tr.Commit();
        }
        var alone=LegendService.Capture(db,new[]{mask},false);Check(alone.Kind=="WIPEOUT"&&alone.Strokes.Single().Mask,"Standalone WIPEOUT is accepted as a native mask, not replaced by a white SOLID");
        var group=LegendService.Capture(db,new[]{front,rear,mask},false);Check(group.Strokes.Count==3&&group.Strokes[0].Mask&&!group.Strokes[1].Mask&&!group.Strokes[2].Mask,"Grouped sample obeys DRAWORDER rather than pick order or insertion order");
        var nested=LegendService.Capture(db,new[]{blockId},false);Check(nested.Strokes.Count==3&&nested.Strokes[0].Mask,"Mask inside translated block retains its relative draw order");
        Check(LegendService.Capture(db,new[]{dimensionId},false).Strokes.Count>0,"DIMENSION captures its displayed 2D geometry");Check(LegendService.Capture(db,new[]{leaderId},false).Strokes.Count>0,"MLEADER captures its displayed 2D geometry");
        bool rejected=false;try{LegendService.Capture(db,new[]{unsupported},false);}catch(InvalidOperationException ex){rejected=ex.Message.Contains("Face");}Check(rejected,"Unsupported 3D face is explicitly rejected by type instead of silently disappearing");
        var spec=new LegendSpec{DatabaseKey=LegendService.DatabaseKey(db)};spec.Rows.Add(group);spec.Rows.Add(nested);var tableId=LegendService.Write(db,spec,new Point3d(500,0,0),Vector3d.ZAxis,Vector3d.XAxis,ObjectId.Null);
        using(var tr=db.TransactionManager.StartOpenCloseTransaction())
        {
            var table=(Table)tr.GetObject(tableId,OpenMode.ForRead);var sample=(BlockTableRecord)tr.GetObject(table.Cells[2,0].BlockTableRecordId,OpenMode.ForRead);var parts=sample.Cast<ObjectId>().Select(id=>(Entity)tr.GetObject(id,OpenMode.ForRead)).ToArray();Check(parts.Length==3&&parts[0] is Wipeout&&parts[1] is Line&&parts[2] is Line,"Native TABLE sample contains WIPEOUT and foreground lines in correct order");
            var original=(Wipeout)tr.GetObject(mask,OpenMode.ForRead);Check(!original.IsErased&&Math.Abs(original.GeometricExtents.MinPoint.X+2)<1e-5,"Original WIPEOUT bounds and identity are unchanged");
            var model=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForRead);var order=(DrawOrderTable)tr.GetObject(model.DrawOrderTableId,OpenMode.ForRead);var wanted=new HashSet<ObjectId>(sourceOrder);Check(order.GetFullDrawOrder(0).Cast<ObjectId>().Where(id=>wanted.Contains(id)).SequenceEqual(sourceOrder),"Original DRAWORDER is unchanged by capture and insertion");
        }
    }
    [CommandMethod("MRTLEGENDTEST",CommandFlags.Modal)]public static void Run()
    {
        string root=Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName;var before=HostApplicationServices.WorkingDatabase;try
        {
            using(var db=new Database(true,true))
            {
                HostApplicationServices.WorkingDatabase=db;ObjectId pipe,valve;using(var tr=db.TransactionManager.StartTransaction())
                {
                    var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);var model=(BlockTableRecord)tr.GetObject(blocks[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var line=new Polyline();line.AddVertexAt(0,new Point2d(0,0),0,0,0);line.AddVertexAt(1,new Point2d(200,0),0,0,0);model.AppendEntity(line);tr.AddNewlyCreatedDBObject(line,true);line.Color=Color.FromColorIndex(ColorMethod.ByAci,3);line.LinetypeScale=2;line.ConstantWidth=4;pipe=line.ObjectId;
                    var definition=new BlockTableRecord{Name="TEST_VALVE"};blocks.Add(definition);tr.AddNewlyCreatedDBObject(definition,true);var circle=new Circle(Point3d.Origin,Vector3d.ZAxis,5);definition.AppendEntity(circle);tr.AddNewlyCreatedDBObject(circle,true);var block=new BlockReference(new Point3d(15,20,0),definition.ObjectId);model.AppendEntity(block);tr.AddNewlyCreatedDBObject(block,true);block.Color=Color.FromColorIndex(ColorMethod.ByAci,1);valve=block.ObjectId;tr.Commit();
                }
                var s=new LegendSpec{DatabaseKey=LegendService.DatabaseKey(db),Notes="Ghi chú tiếng Việt\nDòng thứ hai.",PipeLinetypeMultiplier=3,PipeWidthMultiplier=2};var a=LegendService.Capture(db,new[]{pipe},true);a.Description="Ống cấp nước";var b=LegendService.Capture(db,new[]{valve},false);b.Description="Van khóa";s.Rows.Add(a);s.Rows.Add(b);Check(b.Strokes.Count>0,"Block sample captures native curves for layout preview");int count=BlockCount(db);bool failed=false;try{LegendService.Write(db,s,Point3d.Origin,Vector3d.ZAxis,Vector3d.XAxis,ObjectId.Null,true);}catch(InvalidOperationException){failed=true;}Check(failed&&BlockCount(db)==count&&String.IsNullOrEmpty(s.Rows[0].SampleBlock),"Failed insertion rolls back all private samples and leaves draft unchanged");
                var id=LegendService.Write(db,s,Point3d.Origin,Vector3d.ZAxis,Vector3d.XAxis,ObjectId.Null);Check(!id.IsNull&&!String.IsNullOrEmpty(s.Rows[0].SampleBlock),"Inserts a native TABLE and stores its private sample identities after commit");
                ObjectId oldMask=ObjectId.Null,oldGroup=ObjectId.Null;using(var maskTr=db.TransactionManager.StartOpenCloseTransaction()){
                    var table=(Table)maskTr.GetObject(id,OpenMode.ForRead);var dict=(DBDictionary)maskTr.GetObject(table.ExtensionDictionary,OpenMode.ForRead);Check(dict.Contains("MRT_LEGEND_MASK_V1"),"Legend persists the background wipeout relationship");var record=(Xrecord)maskTr.GetObject(dict.GetAt("MRT_LEGEND_MASK_V1"),OpenMode.ForRead);TypedValue[] vals;using(var data=record.Data)vals=data.AsArray();oldMask=(ObjectId)vals[0].Value;oldGroup=(ObjectId)vals[1].Value;var wipe=(Wipeout)maskTr.GetObject(oldMask,OpenMode.ForRead);var group=(Group)maskTr.GetObject(oldGroup,OpenMode.ForRead);Check(group.GetAllEntityIds().Contains(id)&&group.GetAllEntityIds().Contains(oldMask),"Table and wipeout belong to the same selectable group");var bounds=wipe.GeometricExtents;Check(Math.Abs((bounds.MaxPoint.X-bounds.MinPoint.X)-table.Width)<.01&&Math.Abs((bounds.MaxPoint.Y-bounds.MinPoint.Y)-table.Height)<.01,"Wipeout spans the full generated table size");var space=(BlockTableRecord)maskTr.GetObject(table.OwnerId,OpenMode.ForRead);var draw=(DrawOrderTable)maskTr.GetObject(space.DrawOrderTableId,OpenMode.ForRead);var all=draw.GetFullDrawOrder(0).Cast<ObjectId>().ToList();Check(all.IndexOf(oldMask)<all.IndexOf(id)&&all.Last()==id,"Wipeout is behind the table, with both above existing drawing objects");}

                using(var tr=db.TransactionManager.StartTransaction()){var table=(Table)tr.GetObject(id,OpenMode.ForWrite);Check(table.Columns.Count==4&&table.Rows.Count==4,"Two-group legend has four columns, title, header, data and notes");Check(table.Cells[2,0].BlockTableRecordId!=ObjectId.Null&&table.Cells[2,2].BlockTableRecordId!=ObjectId.Null,"Both symbol cells contain native block content");var record=(BlockTableRecord)tr.GetObject(table.Cells[2,0].BlockTableRecordId,OpenMode.ForRead);var sample=(Polyline)tr.GetObject(record.Cast<ObjectId>().Single(),OpenMode.ForRead);Check(Math.Abs(sample.LinetypeScale-6)<1e-8&&Math.Abs(sample.ConstantWidth-8)<1e-8,"Pipe sample independently multiplies source linetype and constant width");table.Cells[2,1].TextString="Tên phòng {mới} — sửa trực tiếp";tr.Commit();}
                using(var scaleTr=db.TransactionManager.StartTransaction()){var scaled=(Table)scaleTr.GetObject(id,OpenMode.ForWrite);scaled.TransformBy(Matrix3d.Scaling(100,Point3d.Origin));scaleTr.Commit();}
                var loaded=LegendService.Read(db,id);Check(Math.Abs(loaded.Width-180)<.01&&Math.Abs(loaded.DrawingFactor-s.DrawingFactor*100)<.01,"Native SCALE x100 reopens with correct paper dimensions and a scaled conversion factor");Check(loaded.Rows[0].Description.Contains("sửa trực tiếp")&&loaded.Title==s.Title,"Reopening reads edited description from native TABLE instead of stale metadata");loaded.Groups=1;var id2=LegendService.Write(db,loaded,new Point3d(999,999,0),Vector3d.ZAxis,Vector3d.XAxis,id);Check(id.IsErased&&!id2.IsNull,"Updating replaces only the old legend after the new table succeeds");Check(oldMask.IsErased&&oldGroup.IsErased,"Updating removes the previous linked wipeout and group");
                using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var table=(Table)tr.GetObject(id2,OpenMode.ForRead);var original=(Polyline)tr.GetObject(pipe,OpenMode.ForRead);Check(table.Columns.Count==2&&table.Position==Point3d.Origin,"One-group update keeps insertion point and changes to two columns");Check(original.Length==200&&original.Color.ColorIndex==3&&!valve.IsErased,"Original pipe and block are unchanged by capture/insert/update");}
                ObjectId pointId;using(var ptr=db.TransactionManager.StartTransaction()){db.Pdmode=34;db.Pdsize=2;var ms=(BlockTableRecord)ptr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);var point=new DBPoint(new Point3d(10,10,0));ms.AppendEntity(point);ptr.AddNewlyCreatedDBObject(point,true);pointId=point.ObjectId;ptr.Commit();}var pointRow=LegendService.Capture(db,new[]{pointId},false);Check(pointRow.Kind=="POINT"&&pointRow.Strokes.Count==3&&db.Pdmode==34&&db.Pdsize==2,"POINT capture makes a stable plus/circle legend glyph without changing PDMODE/PDSIZE");
                // A later foreground entity can cover a previously-created mask.
                // Refresh must keep the table identity, replace only its owned mask,
                // and put both above that later object.
                ObjectId later;using(var tr=db.TransactionManager.StartTransaction()){var owner=(BlockTableRecord)tr.GetObject(((Table)tr.GetObject(id2,OpenMode.ForRead)).OwnerId,OpenMode.ForWrite);var overlay=new Line(new Point3d(0,0,0),new Point3d(500,0,0));owner.AppendEntity(overlay);tr.AddNewlyCreatedDBObject(overlay,true);later=overlay.ObjectId;tr.Commit();}
                Check(LegendService.RefreshMask(db,id2),"Refresh recognizes and replaces the mask owned by an existing legend");
                using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var t=(Table)tr.GetObject(id2,OpenMode.ForRead);var d=(DBDictionary)tr.GetObject(t.ExtensionDictionary,OpenMode.ForRead);var x=(Xrecord)tr.GetObject(d.GetAt("MRT_LEGEND_MASK_V1"),OpenMode.ForRead);TypedValue[] data;using(var values=x.Data)data=values.AsArray();var mask=(ObjectId)data[0].Value;var group=(Group)tr.GetObject((ObjectId)data[1].Value,OpenMode.ForRead);var owner=(BlockTableRecord)tr.GetObject(t.OwnerId,OpenMode.ForRead);var draw=(DrawOrderTable)tr.GetObject(owner.DrawOrderTableId,OpenMode.ForRead);var order=draw.GetFullDrawOrder(0).Cast<ObjectId>().ToList();Check(order.IndexOf(later)<order.IndexOf(mask)&&order.IndexOf(mask)<order.IndexOf(id2),"Refreshed TABLE and WIPEOUT are above later geometry in correct order");Check(group.GetAllEntityIds().Length==2&&!later.IsErased,"Refresh keeps other drawing geometry untouched");}
                ObjectId generic;using(var tr=db.TransactionManager.StartTransaction()){var owner=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);var t=new Table();t.SetDatabaseDefaults(db);t.SetSize(2,2);t.SetRowHeight(20);t.SetColumnWidth(30);owner.AppendEntity(t);tr.AddNewlyCreatedDBObject(t,true);t.GenerateLayout();generic=t.ObjectId;tr.Commit();}
                Check(!LegendService.RefreshMask(db,generic)&&LegendService.RefreshMask(db,generic),"Native non-Mr.T TABLE can create and refresh its own mask without replacing the table");
                MaskSamples(db);HatchSamples(db);db.SaveAs(Path.Combine(root,"evidence","legend-native-regression.dwg"),DwgVersion.AC1032);
            }
            log.Add("RESULT: PASS; native scratch Database only. Ribbon clicks, interactive picking, font appearance and plotting still need live GUI verification.");
        }catch(System.Exception ex){log.Add("RESULT: FAIL");log.Add(ex.ToString());}finally{HostApplicationServices.WorkingDatabase=before;File.WriteAllLines(Path.Combine(root,"evidence","legend-runtime-tests.txt"),log);var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",log)+"\n");}
    }
}
