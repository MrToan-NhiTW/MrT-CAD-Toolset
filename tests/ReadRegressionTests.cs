using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Engine;
[assembly:CommandClass(typeof(ReadRegressionTests))]
public static class ReadRegressionTests
{
    static readonly List<string> log=new List<string>();
    static void Check(bool ok,string message){if(!ok)throw new System.Exception(message);log.Add("PASS: "+message);}
    static ObjectId Make(Database db,string name,int kind)
    {
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForWrite);var type=new LinetypeTableRecord{Name=name,NumDashes=3,PatternLength=4};types.Add(type);tr.AddNewlyCreatedDBObject(type,true);type.SetDashLengthAt(0,2);type.SetDashLengthAt(1,-1);type.SetDashLengthAt(2,-1);
            if(kind>0)
            {
                var styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForWrite);var style=new TextStyleTableRecord{Name=name+"_STYLE"};styles.Add(style);tr.AddNewlyCreatedDBObject(style,true);style.FileName=kind==2?"ltypeshp1.shx":"arial.ttf";style.IsShapeFile=kind==2;
                int slot=kind==3?0:1;type.SetShapeStyleAt(slot,style.ObjectId);type.SetShapeScaleAt(slot,.35);type.SetShapeOffsetAt(slot,new Vector2d(-1,-.16));
                if(kind==2)type.SetShapeNumberAt(slot,132);else type.SetTextAt(slot,"D15.Oxy");
            }
            var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForWrite);var layer=new LayerTableRecord{Name=name,LinetypeObjectId=type.ObjectId};layers.Add(layer);tr.AddNewlyCreatedDBObject(layer,true);tr.Commit();return layer.ObjectId;
        }
    }
    [CommandMethod("MRTREADTEST",CommandFlags.Modal)]public static void Run()
    {
        string root=Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName;string evidence=Path.Combine(root,"evidence");var previous=HostApplicationServices.WorkingDatabase;log.Clear();
        try
        {
            using(var db=new Database(true,true))
            {
                HostApplicationServices.WorkingDatabase=db;var plain=Make(db,"Mr.T_TEST_PLAIN",0);var text=Make(db,"Mr.T_TEST_TEXT",1);Make(db,"Mr.T_TEST_SHAPE",2);Make(db,"Mr.T_TEST_TEXT_AT_ZERO",3);
                bool reproduced=false;using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var layer=(LayerTableRecord)tr.GetObject(plain,OpenMode.ForRead);var type=(LinetypeTableRecord)tr.GetObject(layer.LinetypeObjectId,OpenMode.ForRead);try{type.TextAt(0);}catch(Autodesk.AutoCAD.Runtime.Exception ex){reproduced=ex.ErrorStatus==ErrorStatus.NotApplicable;}}
                Check(reproduced,"Reproduce the reported eNotApplicable by calling TextAt on a plain dash");
                var rows=DrawingService.Read(db);var current=rows.Single(r=>r.LayerName=="Mr.T_TEST_TEXT");
                Check(rows.Single(r=>r.LayerName=="Mr.T_TEST_PLAIN").Text=="","Plain dash/gap pattern loads without requesting text");
                Check(current.Text=="D15.Oxy"&&current.Editable,"Embedded text loads from the middle text slot only");
                Check(rows.Single(r=>r.LayerName=="Mr.T_TEST_SHAPE").Text==""&&!rows.Single(r=>r.LayerName=="Mr.T_TEST_SHAPE").Editable,"SHX shape is not read as text or offered for simple-text editing");
                Check(rows.Single(r=>r.LayerName=="Mr.T_TEST_TEXT_AT_ZERO").Text=="D15.Oxy"&&!rows.Single(r=>r.LayerName=="Mr.T_TEST_TEXT_AT_ZERO").Editable,"Text in a different slot cannot trigger an unguarded TextAt(1)");
                Check(rows.Single(r=>r.LayerName=="0").Text=="","Continuous is read safely alongside all complex patterns");
                ObjectId id;using(var tr=db.TransactionManager.StartTransaction()){var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var line=new Polyline();line.AddVertexAt(0,new Point2d(10,20),0,15,15);line.AddVertexAt(1,new Point2d(110,20),0,15,15);model.AppendEntity(line);tr.AddNewlyCreatedDBObject(line,true);line.LinetypeScale=.1;id=line.ObjectId;tr.Commit();}
                current=DrawingService.Read(db).Single(r=>r.LayerName=="Mr.T_TEST_TEXT");var result=DrawingService.Assign(db,new[]{id},current.LayerName,true,true,1,expected:current);
                Check(result.Changed==1,"Search assignment succeeds after loading mixed native linetypes");
                using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var line=(Polyline)tr.GetObject(id,OpenMode.ForRead);Check(line.LayerId==text&&line.LinetypeId==db.ByLayerLinetype&&line.LinetypeScale==.1,"Assignment uses chosen layer and ByLayer while preserving scale 0.1");Check(line.GetStartWidthAt(0)==15&&line.GetPoint2dAt(0)==new Point2d(10,20)&&line.GetPoint2dAt(1)==new Point2d(110,20),"Polyline width 15 and vertex geometry are preserved");}
                Check(DrawingService.Read(db).Single(r=>r.LayerName==current.LayerName).Text=="D15.Oxy","Assignment does not alter the linetype definition");
            }
            log.Add("RESULT: PASS");
        }
        catch(System.Exception ex){log.Add("RESULT: FAIL");log.Add(ex.ToString());}
        finally{HostApplicationServices.WorkingDatabase=previous;File.WriteAllLines(Path.Combine(evidence,"read-regression-native.txt"),log);var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",log)+"\n");}
    }
}
