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
        public string Label,Layer;
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
                                var old=Type(db,tr,pipe);if(old.IsNull||!SameLabel(Text(tr,old),row.Label))throw new InvalidOperationException("nhãn của nét mẫu không khớp thông tin bảng.");
                                var sources=new List<ObjectId>();string sourceLayer=null;
                                // New tables remember the original selected pipe. Check both drawing identity and actual text.
                                if(row.PipeSourceDatabaseKey==LegendService.DatabaseKey(db))
                                {
                                    var source=Id(db,row.PipeSourceHandle);var e=source.IsNull?null:tr.GetObject(source,OpenMode.ForRead) as Entity;
                                    if(e!=null&&(e is Line||e is Polyline)&&!Type(db,tr,e).IsNull&&SameLabel(Text(tr,Type(db,tr,e)),row.Label)){sources.Add(source);sourceLayer=e.Layer;}
                                }
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
                                var types=new HashSet<ObjectId>();foreach(var id in sources){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||!String.Equals(e.Layer,sourceLayer,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("đường nguồn đã đổi layer.");var type=Type(db,tr,e);if(type.IsNull||!SameLabel(Text(tr,type),row.Label))throw new InvalidOperationException("đường nguồn có nét hoặc nhãn không khớp.");types.Add(type);}
                                if(types.Count!=1)throw new InvalidOperationException("nhiều nét nguồn/scale khác nhau; hãy pick lại mẫu chính xác.");
                                var target=types.Single();if(target==old){plan.Already++;continue;}
                                plan.Changes.Add(new LegendPipeChange{Table=tid,Row=r,Column=c,Sample=sample,OldType=old,NewType=target,Label=row.Label,Layer=sourceLayer,Sources=sources});
                            }
                            catch(Exception ex){plan.Skipped.Add(label+ex.Message);}
                        }
                    }
                }
            }
            return plan;
        }
        public static int Apply(Database db,LegendPipePlan plan)
        {
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);var copies=new Dictionary<string,ObjectId>();var modified=new HashSet<ObjectId>();
                foreach(var change in plan.Changes)
                {
                    var table=(Table)tr.GetObject(change.Table,OpenMode.ForWrite);
                    if(((LayerTableRecord)tr.GetObject(table.LayerId,OpenMode.ForRead)).IsLocked||table.Cells[change.Row,change.Column].BlockTableRecordId!=change.Sample)throw new InvalidOperationException("Bảng đã đổi sau xem trước; hãy kiểm tra lại.");
                    var sample=(BlockTableRecord)tr.GetObject(change.Sample,OpenMode.ForRead);var ids=sample.Cast<ObjectId>().ToArray();if(ids.Length!=1)throw new InvalidOperationException("Mẫu ống đã thay đổi; hãy kiểm tra lại.");var line=tr.GetObject(ids[0],OpenMode.ForRead) as Polyline;
                    if(line==null||Type(db,tr,line)!=change.OldType)throw new InvalidOperationException("Nét mẫu đã thay đổi; hãy kiểm tra lại.");
                    foreach(var id in change.Sources){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||Type(db,tr,e)!=change.NewType||!String.Equals(e.Layer,change.Layer,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Ống nguồn đã thay đổi; hãy kiểm tra lại.");}
                    string key=change.Sample.Handle+"|"+change.NewType.Handle;ObjectId replacement;
                    if(!copies.TryGetValue(key,out replacement))
                    {
                        string name;do{name="Mr.T_LG_"+Guid.NewGuid().ToString("N").Substring(0,8);}while(blocks.Has(name));
                        var b=new BlockTableRecord{Name=name,Origin=sample.Origin};replacement=blocks.Add(b);tr.AddNewlyCreatedDBObject(b,true);
                        var clone=(Polyline)line.Clone();clone.LinetypeId=change.NewType;b.AppendEntity(clone);tr.AddNewlyCreatedDBObject(clone,true);copies.Add(key,replacement);
                    }
                    var content=table.Cells[change.Row,change.Column].Contents[0];bool auto=content.IsAutoScale;double scale=content.Scale,rotation=content.Rotation;
                    content.BlockTableRecordId=replacement;content.IsAutoScale=auto;if(!auto)content.Scale=scale;content.Rotation=rotation;modified.Add(change.Table);
                }
                foreach(var id in modified)((Table)tr.GetObject(id,OpenMode.ForWrite)).GenerateLayout();
                tr.Commit();return plan.Changes.Count;
            }
        }
    }
}
