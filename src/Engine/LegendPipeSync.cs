using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using MedGas2024.Core;

namespace MedGas2024.Engine
{
    public sealed class LegendPipeChange
    {
        public ObjectId Table,Sample,OldType,NewType;
        public int Row,Column;
        public string Label,Layer,RowId,NewLabel;
        public Autodesk.AutoCAD.Colors.Color NewColor;
        public LineWeight NewLineWeight;
        public List<ObjectId> Sources=new List<ObjectId>();
    }
    public sealed class LegendPipePlan
    {
        public readonly List<LegendPipeChange> Changes=new List<LegendPipeChange>();
        public readonly List<string> Skipped=new List<string>();
        public int Tables,Already;
    }
    // A table cell owns a block sample, not the drawing pipe. Resolve the effective
    // type of the source and replace only that cell's sample; never edit shared
    // source definitions or unrelated block symbols.
    public static class LegendPipeSync
    {
        const string Key="MRT_LEGEND_V1";
        static ObjectId Id(Database db,string handle)
        {
            long n;if(String.IsNullOrEmpty(handle)||!Int64.TryParse(handle,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out n))return ObjectId.Null;
            try{var id=db.GetObjectId(false,new Handle(n),0);return id.IsNull||!id.IsValid||id.IsErased?ObjectId.Null:id;}catch(Autodesk.AutoCAD.Runtime.Exception){return ObjectId.Null;}
        }
        static ObjectId Type(Database db,Transaction tr,Entity e)
        {
            if(e.LinetypeId==db.ByBlockLinetype)return ObjectId.Null;
            return e.LinetypeId==db.ByLayerLinetype?((LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead)).LinetypeObjectId:e.LinetypeId;
        }
        static bool Near(double a,double b){return Math.Abs(a-b)<=Math.Max(1e-9,Math.Max(Math.Abs(a),Math.Abs(b))*1e-8);}
        static bool SameLabel(string a,string b){return !String.IsNullOrWhiteSpace(a)&&String.Equals(LabelRules.Canonical(a),LabelRules.Canonical(b),StringComparison.OrdinalIgnoreCase);}
        static string Text(Transaction tr,ObjectId id)
        {
            var t=tr.GetObject(id,OpenMode.ForRead) as LinetypeTableRecord;
            return t==null?"":LabelService.Definition(t,tr).Text;
        }
        // Same resolution as the legend sample builder: ByLayer → layer value, ByBlock → kept.
        static Autodesk.AutoCAD.Colors.Color ColorOf(Transaction tr,Entity e)
        {
            var c=e.Color;if(c.ColorMethod==Autodesk.AutoCAD.Colors.ColorMethod.ByLayer)c=((LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead)).Color;
            return c.ColorMethod==Autodesk.AutoCAD.Colors.ColorMethod.ByBlock?Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci,7):c;
        }
        static LineWeight WeightOf(Transaction tr,Entity e){return e.LineWeight==LineWeight.ByLayer?((LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead)).LineWeight:e.LineWeight;}
        static bool SameColor(Autodesk.AutoCAD.Colors.Color a,Autodesk.AutoCAD.Colors.Color b){return a.ColorMethod==b.ColorMethod&&a.ColorIndex==b.ColorIndex&&a.Red==b.Red&&a.Green==b.Green&&a.Blue==b.Blue;}
        // The remembered source pipe is authoritative: when it still exists in this drawing,
        // follow it even if its label was renamed after the table was created.
        static Entity TrustedSource(Database db,Transaction tr,LegendRow row)
        {
            if(row.PipeSourceDatabaseKey!=LegendService.DatabaseKey(db))return null;
            var id=Id(db,row.PipeSourceHandle);var e=id.IsNull?null:tr.GetObject(id,OpenMode.ForRead) as Entity;
            if(e==null||!(e is Line||e is Polyline)||Type(db,tr,e).IsNull)return null;return e;
        }
        public static LegendPipePlan Inspect(Database db,List<DensityRow> drawing)
        {
            var plan=new LegendPipePlan();
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
                foreach(ObjectId bid in blocks)
                {
                    var owner=(BlockTableRecord)tr.GetObject(bid,OpenMode.ForRead);
                    if(owner.IsFromExternalReference||owner.IsFromOverlayReference)continue;
                    foreach(ObjectId tid in owner)
                    {
                        var table=tr.GetObject(tid,OpenMode.ForRead) as Table;
                        if(table==null||table.ExtensionDictionary.IsNull)continue;
                        var dict=(DBDictionary)tr.GetObject(table.ExtensionDictionary,OpenMode.ForRead);if(!dict.Contains(Key))continue;
                        plan.Tables++;
                        LegendSpec spec;
                        try{var x=(Xrecord)tr.GetObject(dict.GetAt(Key),OpenMode.ForRead);using(var data=x.Data)spec=LegendSpec.Parse(String.Concat(data.AsArray().Select(v=>Convert.ToString(v.Value))));}
                        catch(Exception ex){plan.Skipped.Add("Bảng "+tid.Handle+": dữ liệu không đọc được: "+ex.Message);continue;}
                        if(((LayerTableRecord)tr.GetObject(table.LayerId,OpenMode.ForRead)).IsLocked){plan.Skipped.Add("Bảng "+tid.Handle+": layer đang khóa.");continue;}
                        for(int r=2;r<table.Rows.Count;r++)for(int c=0;c<table.Columns.Count;c+=2)
                        {
                            string marker=table.Cells[r,c].ToolTip??"";var row=spec.Rows.FirstOrDefault(x=>marker=="MRTLG:"+x.Id);
                            if(row==null||row.Kind!="Nét ống")continue;
                            string label="Bảng "+tid.Handle+", "+row.Label+": ";
                            try
                            {
                                if((table.Cells[r,c].ContentTypes&CellContentTypes.Block)==0||table.Cells[r,c].Contents.Count!=1)throw new InvalidOperationException("ô mẫu không phải block đơn.");
                                var sample=table.Cells[r,c].BlockTableRecordId;var b=(BlockTableRecord)tr.GetObject(sample,OpenMode.ForRead);
                                if(!b.Name.StartsWith("Mr.T_LG_",StringComparison.OrdinalIgnoreCase)||b.IsFromExternalReference||b.IsFromOverlayReference)throw new InvalidOperationException("mẫu không phải block mẫu Mr.T nội bộ.");
                                var entities=b.Cast<ObjectId>().ToArray();if(entities.Length!=1)throw new InvalidOperationException("mẫu gồm nhiều đối tượng.");
                                var pipe=tr.GetObject(entities[0],OpenMode.ForRead) as Polyline;if(pipe==null)throw new InvalidOperationException("mẫu không phải polyline.");
                                var old=Type(db,tr,pipe);
                                var trusted=TrustedSource(db,tr,row);
                                if(old.IsNull||(trusted==null&&!SameLabel(Text(tr,old),row.Label)))throw new InvalidOperationException("nhãn của nét mẫu không khớp thông tin bảng và không còn ống nguồn đã pick.");
                                var sources=new List<ObjectId>();string sourceLayer=null;
                                if(trusted!=null){sources.Add(trusted.ObjectId);sourceLayer=trusted.Layer;}
                                if(sources.Count==0)
                                {
                                    var candidates=drawing.Where(x=>x.Direct>0&&SameLabel(x.Text,row.Label)).ToList();
                                    if(!String.IsNullOrEmpty(row.PipeSourceLayer)&&row.PipeSourceDatabaseKey==LegendService.DatabaseKey(db))candidates=candidates.Where(x=>String.Equals(x.Layer,row.PipeSourceLayer,StringComparison.OrdinalIgnoreCase)).ToList();
                                    else {var exact=candidates.Where(x=>String.Equals(x.TypeHandle,old.Handle.ToString(),StringComparison.OrdinalIgnoreCase)).ToList();if(exact.Count>0)candidates=exact;}
                                    var layers=candidates.Select(x=>x.Layer).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                                    if(layers.Length!=1)throw new InvalidOperationException(layers.Length==0?"không tìm thấy ống nguồn; hãy sửa bảng và pick lại mẫu.":"nhiều layer cùng nhãn; hãy sửa bảng và pick lại mẫu.");
                                    sourceLayer=layers[0];
                                    var members=candidates.SelectMany(x=>x.Members).ToList();
                                    if(row.PipeBaseline){var scaled=members.Where(x=>Near(x.Scale,row.PipeBaseScale)).ToList();if(scaled.Count>0)members=scaled;}
                                    foreach(var member in members){var id=Id(db,member.Handle);if(!id.IsNull&&!sources.Contains(id))sources.Add(id);}
                                }
                                if(sources.Count==0)throw new InvalidOperationException("không còn đường nguồn để đối chiếu.");
                                var types=new HashSet<ObjectId>();foreach(var id in sources){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||!String.Equals(e.Layer,sourceLayer,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("đường nguồn đã đổi layer.");var type=Type(db,tr,e);if(type.IsNull||(trusted==null&&!SameLabel(Text(tr,type),row.Label)))throw new InvalidOperationException("đường nguồn có nét hoặc nhãn không khớp.");types.Add(type);}
                                if(types.Count!=1)throw new InvalidOperationException("nhiều nét nguồn/scale khác nhau; hãy pick lại mẫu chính xác.");
                                var target=types.Single();var first=(Entity)tr.GetObject(sources[0],OpenMode.ForRead);var color=ColorOf(tr,first);var weight=WeightOf(tr,first);
                                string current=Text(tr,target);string renamed=!String.IsNullOrWhiteSpace(current)&&!SameLabel(current,row.Label)?current:null;
                                if(target==old&&SameColor(color,pipe.Color)&&weight==pipe.LineWeight&&renamed==null){plan.Already++;continue;}
                                plan.Changes.Add(new LegendPipeChange{Table=tid,Row=r,Column=c,Sample=sample,OldType=old,NewType=target,Label=row.Label,Layer=sourceLayer,Sources=sources,RowId=row.Id,NewLabel=renamed,NewColor=color,NewLineWeight=weight});
                            }
                            catch(Exception ex){plan.Skipped.Add(label+ex.Message);}
                        }
                    }
                }
            }
            return plan;
        }
        // Replaced Mr.T_LG_ samples used to accumulate forever. Erase only Mr.T samples that
        // no table cell and no block reference still uses.
        static void PurgeSamples(Database db,Transaction tr,HashSet<ObjectId> candidates)
        {
            if(candidates.Count==0)return;var used=new HashSet<ObjectId>();
            var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
            foreach(ObjectId bid in blocks){var owner=(BlockTableRecord)tr.GetObject(bid,OpenMode.ForRead);if(owner.IsFromExternalReference||owner.IsFromOverlayReference)continue;
                foreach(ObjectId id in owner){var t=tr.GetObject(id,OpenMode.ForRead) as Table;if(t==null)continue;
                    for(int r=0;r<t.Rows.Count;r++)for(int c=0;c<t.Columns.Count;c++){try{var cell=t.Cells[r,c];if((cell.ContentTypes&CellContentTypes.Block)==0)continue;foreach(CellContent content in cell.Contents)if(!content.BlockTableRecordId.IsNull)used.Add(content.BlockTableRecordId);}catch(Autodesk.AutoCAD.Runtime.Exception){}}}}
            foreach(var id in candidates){if(used.Contains(id)||id.IsErased)continue;var b=tr.GetObject(id,OpenMode.ForRead) as BlockTableRecord;
                if(b==null||!b.Name.StartsWith("Mr.T_LG_",StringComparison.OrdinalIgnoreCase)||b.GetBlockReferenceIds(true,false).Count>0)continue;
                b.UpgradeOpen();foreach(ObjectId child in b){var e=tr.GetObject(child,OpenMode.ForWrite);e.Erase();}b.Erase();}
        }
        public static int Apply(Database db,LegendPipePlan plan)
        {
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);var copies=new Dictionary<string,ObjectId>();var modified=new HashSet<ObjectId>();var replaced=new HashSet<ObjectId>();var specChanges=new Dictionary<ObjectId,List<LegendPipeChange>>();
                foreach(var change in plan.Changes)
                {
                    var table=(Table)tr.GetObject(change.Table,OpenMode.ForWrite);
                    if(((LayerTableRecord)tr.GetObject(table.LayerId,OpenMode.ForRead)).IsLocked||table.Cells[change.Row,change.Column].BlockTableRecordId!=change.Sample)throw new InvalidOperationException("Bảng đã đổi sau xem trước; hãy kiểm tra lại.");
                    var sample=(BlockTableRecord)tr.GetObject(change.Sample,OpenMode.ForRead);var ids=sample.Cast<ObjectId>().ToArray();if(ids.Length!=1)throw new InvalidOperationException("Mẫu ống đã thay đổi; hãy kiểm tra lại.");var line=tr.GetObject(ids[0],OpenMode.ForRead) as Polyline;
                    if(line==null||Type(db,tr,line)!=change.OldType)throw new InvalidOperationException("Nét mẫu đã thay đổi; hãy kiểm tra lại.");
                    foreach(var id in change.Sources){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||Type(db,tr,e)!=change.NewType||!String.Equals(e.Layer,change.Layer,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Ống nguồn đã thay đổi; hãy kiểm tra lại.");}
                    bool restyle=change.NewType!=change.OldType||(change.NewColor!=null&&!SameColor(change.NewColor,line.Color))||line.LineWeight!=change.NewLineWeight;
                    ObjectId replacement=change.Sample;
                    if(restyle)
                    {
                        string key=change.Sample.Handle+"|"+change.NewType.Handle+"|"+(change.NewColor==null?"":change.NewColor.ToString())+"|"+(int)change.NewLineWeight;
                        if(!copies.TryGetValue(key,out replacement))
                        {
                            string name;do{name="Mr.T_LG_"+Guid.NewGuid().ToString("N").Substring(0,8);}while(blocks.Has(name));
                            var b=new BlockTableRecord{Name=name,Origin=sample.Origin};replacement=blocks.Add(b);tr.AddNewlyCreatedDBObject(b,true);
                            var clone=(Polyline)line.Clone();clone.LinetypeId=change.NewType;if(change.NewColor!=null)clone.Color=change.NewColor;clone.LineWeight=change.NewLineWeight;b.AppendEntity(clone);tr.AddNewlyCreatedDBObject(clone,true);copies.Add(key,replacement);
                        }
                        var content=table.Cells[change.Row,change.Column].Contents[0];bool auto=content.IsAutoScale;double scale=content.Scale,rotation=content.Rotation;
                        content.BlockTableRecordId=replacement;content.IsAutoScale=auto;if(!auto)content.Scale=scale;content.Rotation=rotation;replaced.Add(change.Sample);
                    }
                    if(change.NewLabel!=null&&change.Column+1<table.Columns.Count)
                    {
                        // Only an untouched description (exactly the old label) follows the rename.
                        var cell=table.Cells[change.Row,change.Column+1];string shown=(cell.TextString??"").Trim();
                        if(SameLabel(shown,change.Label))cell.TextString=change.NewLabel;
                    }
                    List<LegendPipeChange> list;if(!specChanges.TryGetValue(change.Table,out list))specChanges[change.Table]=list=new List<LegendPipeChange>();
                    change.Sample=replacement;list.Add(change);modified.Add(change.Table);
                }
                foreach(var pair in specChanges)
                {
                    var table=(Table)tr.GetObject(pair.Key,OpenMode.ForWrite);var dict=(DBDictionary)tr.GetObject(table.ExtensionDictionary,OpenMode.ForRead);LegendSpec spec;
                    var x=(Xrecord)tr.GetObject(dict.GetAt(Key),OpenMode.ForRead);using(var data=x.Data)spec=LegendSpec.Parse(String.Concat(data.AsArray().Select(v=>Convert.ToString(v.Value))));
                    foreach(var change in pair.Value){var row=spec.Rows.FirstOrDefault(z=>z.Id==change.RowId);if(row==null)continue;if(change.NewLabel!=null)row.Label=change.NewLabel;row.SampleBlock=change.Sample.Handle.ToString();}
                    LegendService.SaveSpec(table,tr,spec);
                }
                foreach(var id in modified)((Table)tr.GetObject(id,OpenMode.ForWrite)).GenerateLayout();
                PurgeSamples(db,tr,replaced);
                tr.Commit();return plan.Changes.Count;
            }
        }
    }
}
