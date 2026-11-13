using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;
[assembly:CommandClass(typeof(SelectionRuntimeTests))]
public static class SelectionRuntimeTests
{
    static List<string> log=new List<string>();
    static void Check(bool ok,string message){if(!ok)throw new System.Exception(message);log.Add("PASS: "+message);}
    static ObjectId Layer(Database db,string name,ObjectId type){using(var tr=db.TransactionManager.StartTransaction()){var table=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForWrite);var l=new LayerTableRecord{Name=name};table.Add(l);tr.AddNewlyCreatedDBObject(l,true);l.LinetypeObjectId=type;tr.Commit();return l.ObjectId;}}
    static ObjectId Type(Database db,ObjectId style,string name,string text){using(var tr=db.TransactionManager.StartTransaction()){var table=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForWrite);var t=new LinetypeTableRecord{Name=name,NumDashes=3,PatternLength=70};table.Add(t);tr.AddNewlyCreatedDBObject(t,true);t.SetDashLengthAt(0,40);t.SetDashLengthAt(1,-15);t.SetDashLengthAt(2,-15);t.SetShapeStyleAt(1,style);t.SetShapeScaleAt(1,2.5);t.SetTextAt(1,text);tr.Commit();return t.ObjectId;}}
    static ObjectId Pipe(Database db,ObjectId layer,ObjectId type,ObjectId owner){using(var tr=db.TransactionManager.StartTransaction()){var space=(BlockTableRecord)tr.GetObject(owner,OpenMode.ForWrite);var e=new Line(Point3d.Origin,new Point3d(300,0,0));space.AppendEntity(e);tr.AddNewlyCreatedDBObject(e,true);e.LayerId=layer;e.LinetypeId=type;e.LinetypeScale=.25;tr.Commit();return e.ObjectId;}}
    static string State(Database db,IEnumerable<ObjectId> ids){using(var tr=db.TransactionManager.StartOpenCloseTransaction())return String.Join(";",ids.Select(id=>{var e=(Line)tr.GetObject(id,OpenMode.ForRead);return id.Handle+"|"+e.LayerId.Handle+"|"+e.LinetypeId.Handle+"|"+e.LinetypeScale+"|"+e.Color+"|"+e.StartPoint+"|"+e.EndPoint;}));}
    static int Symbols(Database db){using(var tr=db.TransactionManager.StartOpenCloseTransaction())return ((LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead)).Cast<ObjectId>().Count()+((LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead)).Cast<ObjectId>().Count();}
    [CommandMethod("MRTSELECTTEST",CommandFlags.Modal)]public static void Run(){log.Clear();var previous=HostApplicationServices.WorkingDatabase;string root=Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName;try{using(var db=new Database(true,true)){
        HostApplicationServices.WorkingDatabase=db;ObjectId style,model,paper,nested;using(var tr=db.TransactionManager.StartTransaction()){var styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForWrite);var st=new TextStyleTableRecord{Name="SELECT_STYLE",FileName="arial.ttf",TextSize=0};styles.Add(st);tr.AddNewlyCreatedDBObject(st,true);style=st.ObjectId;var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);model=bt[BlockTableRecord.ModelSpace];paper=bt[BlockTableRecord.PaperSpace];var b=new BlockTableRecord{Name="NESTED_SELECT"};bt.Add(b);tr.AddNewlyCreatedDBObject(b,true);nested=b.ObjectId;tr.Commit();}
        var oxy=Type(db,style,"T_A","D15.Oxy");var oxy22=Type(db,style,"T_B","D22.Oxy");var old=Type(db,style,"T_C","Oxy-%%C15");var co=Type(db,style,"T_D","D15.CO2");
        var a=Layer(db,"Mr.T_PIPE_RAW",oxy);var b1=Layer(db,"random foreign layer",oxy22);var b2=Layer(db,"also foreign",co);var plain=Layer(db,"UNLABELED",db.ContinuousLinetype);var other=Layer(db,"OTHER_PLAIN",db.ContinuousLinetype);
        var p1=Pipe(db,a,db.ByLayerLinetype,model);var p2=Pipe(db,b1,db.ByLayerLinetype,model);var p3=Pipe(db,b2,old,model);var different=Pipe(db,a,co,model);var paperPipe=Pipe(db,b1,oxy,paper);var locked=Pipe(db,b2,oxy,model);var plain1=Pipe(db,plain,db.ByLayerLinetype,model);var plain2=Pipe(db,plain,db.ContinuousLinetype,model);var labelOnPlain=Pipe(db,plain,co,model);var otherPlain=Pipe(db,other,db.ByLayerLinetype,model);var nestedPipe=Pipe(db,a,oxy,nested);
        using(var tr=db.TransactionManager.StartTransaction()){((LayerTableRecord)tr.GetObject(b2,OpenMode.ForWrite)).IsLocked=true;tr.Commit();}
        var ids=new[]{p1,p2,p3,different,paperPipe,locked,plain1,plain2,labelOnPlain,otherPlain,nestedPipe};string before=State(db,ids);int symbols=Symbols(db);
        var pick=PipeSelection.FromSource(db,p1,model);Check(pick.Ids.OrderBy(x=>x.Handle.Value).SequenceEqual(new[]{p1,p2,p3,locked}.OrderBy(x=>x.Handle.Value))&&pick.Matched==5&&pick.OtherSpaces==1&&pick.Locked==2,"First-pick system expands all diameters across arbitrary layers, aliases and overrides; limits current space");
        Check(PipeSelection.FromSource(db,p3,model).Ids.Length==4,"Foreign source with no Mr.T naming selects its actual text system");
        pick=PipeSelection.FromSource(db,plain1,model);Check(pick.Ids.OrderBy(x=>x.Handle.Value).SequenceEqual(new[]{plain1,plain2}.OrderBy(x=>x.Handle.Value)),"Unlabeled fallback intersects same layer and effective type, excluding labels and other plain layers");
        var multiple=PipeSelection.FromSources(db,new[]{p1,different,p2,p1},model);Check(multiple.Ids.Length==6&&multiple.Ids.Contains(labelOnPlain)&&multiple.FamilySeeds==3&&multiple.OtherSpaces==1,"Multiple sources expand multiple systems, deduplicate families and exclude other layouts");
        multiple=PipeSelection.FromSources(db,new[]{p1,plain1},model);Check(multiple.Ids.Length==6&&multiple.Ids.Contains(plain2)&&!multiple.Ids.Contains(otherPlain)&&!multiple.Ids.Contains(labelOnPlain),"Combined system scan preserves exact Layer/type fallback for unlabeled sources");
        multiple=PipeSelection.FromSources(db,new[]{nestedPipe,ObjectId.Null},model);Check(multiple.Ids.Length==0&&multiple.OtherSeeds==2,"Non-direct and invalid samples do not expand block definitions");
        Check(PipeSelection.InSpace(db,new[]{p1,p1,paperPipe,nestedPipe},model).SequenceEqual(new[]{p1}),"Final combination filters invalid spaces and removes duplicate IDs");
        var catalog=LabelService.Read(db);pick=PipeSelection.FromLabels(db,new LabelScope{Keys=new[]{LabelRules.Key("D15.Oxy",false),LabelRules.Key("D15.CO2",false)},Fingerprint=catalog.Fingerprint,LayerPrefix="mr.t_",LayerSuffix="_raw"},model);Check(pick.Ids.Length==2&&pick.Ids.Contains(p1)&&pick.Ids.Contains(different),"Checked-group union respects actual layer prefix and suffix");
        bool rejected=false;try{PipeSelection.FromSource(db,nestedPipe,model);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Nested block source is rejected without expanding definition members");
        rejected=false;try{PipeSelection.FromLabels(db,new LabelScope{All=true,Fingerprint="stale"},model);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Stale catalog cannot silently select a changed drawing");
        Check(State(db,ids)==before&&Symbols(db)==symbols,"Selection leaves geometry, colors, scales, layers and symbol definitions unchanged");
    }log.Add("RESULT: PASS; scratch database only. First-pick grips, active viewport and ribbon require live CAD checks.");}catch(System.Exception ex){log.Add("RESULT: FAIL");log.Add(ex.ToString());}finally{HostApplicationServices.WorkingDatabase=previous;File.WriteAllLines(Path.Combine(root,"evidence","selection-runtime-tests.txt"),log);var doc=Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\n"+String.Join("\n",log)+"\n");}}
}
