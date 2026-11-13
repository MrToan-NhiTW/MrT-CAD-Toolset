using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using MedGas2024.Core;

namespace MedGas2024.Engine
{
    public sealed class RNameAction
    {
        public RNameReviewRow Row;public ObjectId Standard,ExistingTarget;public readonly HashSet<ObjectId> Sources=new HashSet<ObjectId>();
        public readonly List<ObjectId> Layers=new List<ObjectId>();public readonly List<ObjectId> Explicit=new List<ObjectId>();public readonly List<ObjectId> UnusedCandidates=new List<ObjectId>();
    }
    public sealed class RNamePlan
    {
        public readonly List<RNameAction> Actions=new List<RNameAction>();public readonly List<RNameReviewRow> Rows=new List<RNameReviewRow>();public string Fingerprint;
    }
    public sealed class RNameResult{public int Families,Layers,Entities,Purged,Retained;public readonly List<string> RetainedNames=new List<string>();}
    public static class RNameService
    {
        static string Semantics(DrawingLayerInfo r,LinetypeTableRecord t,bool relative){return String.Join("|",new[]{r.Text??"",Profile.N(r.Height),Profile.N(r.Dash),Profile.N(r.Before),Profile.N(r.After),Profile.N(r.X),Profile.N(r.Y),r.Font??"",relative.ToString(),t.ShapeIsUcsOrientedAt(1).ToString(),Profile.N(t.ShapeRotationAt(1)),Profile.N(t.PatternLength),t.IsScaledToFit.ToString()});}
        static bool Curve(Entity e){return e is Curve;}
        static bool MrTLayer(string s){return !String.IsNullOrEmpty(s)&&s.StartsWith("Mr.T",StringComparison.OrdinalIgnoreCase);}
        public static RNamePlan Inspect(Database db)
        {
            var result=new RNamePlan();var snapshot=LabelService.Read(db);var drawing=DrawingService.Read(db);var signature=new List<string>{snapshot.Fingerprint};
            var standards=drawing.Where(r=>RNameRules.CanonicalLayer(r.LayerName)&&!String.IsNullOrWhiteSpace(r.Text)).ToList();
            var labelCounts=standards.GroupBy(r=>RNameRules.LabelKey(r.Text)).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.OrdinalIgnoreCase);
            var typeIds=new List<ObjectId>();
            using(var tr=db.TransactionManager.StartOpenCloseTransaction()){
                var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);foreach(ObjectId id in types)typeIds.Add(id);
            }
            var relative=RelativeTypeProbe.Read(db,typeIds);
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var lt=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                var definitions=new Dictionary<ObjectId,DrawingLayerInfo>();foreach(ObjectId id in lt){var t=(LinetypeTableRecord)tr.GetObject(id,OpenMode.ForRead);if(t.IsDependent)continue;try{definitions[id]=LabelService.Definition(t,tr);}catch(Autodesk.AutoCAD.Runtime.Exception){}}
                foreach(var standard in standards)
                {
                    string key=RNameRules.LabelKey(standard.Text);var members=snapshot.Members.Where(m=>MrTLayer(m.Layer)&&RNameRules.LabelKey(m.Text)==key).ToList();
                    var candidates=definitions.Where(kv=>RNameRules.OldName(kv.Value.LinetypeName)&&RNameRules.LabelKey(kv.Value.Text)==key).ToList();
                    bool active=RNameRules.OldName(standard.LinetypeName)||members.Any(m=>RNameRules.OldName(m.Type)||!String.Equals(m.Layer,standard.LayerName,StringComparison.OrdinalIgnoreCase))||candidates.Count>0;if(!active)continue;
                    var row=new RNameReviewRow{Key=standard.LayerName,Label=standard.Text,StandardLayer=standard.LayerName,Target=DensityMath.ReadableStem(standard.LayerName,standard.LinetypeName,standard.Text,false),Source=standard.LinetypeName,Ready=true};var action=new RNameAction{Row=row};result.Rows.Add(row);result.Actions.Add(action);
                    if(labelCounts[key]!=1){row.Ready=false;row.State="Cùng nhãn có nhiều layer chuẩn RAW/BASE; chưa xác định duy nhất.";continue;}
                    if(members.Any(m=>m.Locked||m.Linked)){row.Ready=false;row.State="Có đường cùng họ bị khóa hoặc có nhãn liên kết cũ; kiểm tra trước.";continue;}
                    var layer=(LayerTableRecord)tr.GetObject(layers[standard.LayerName],OpenMode.ForRead);action.Standard=layer.LinetypeObjectId;DrawingLayerInfo baseDef;bool isR;
                    if(!definitions.TryGetValue(action.Standard,out baseDef)||!baseDef.Editable||!relative.TryGetValue(action.Standard,out isR)||!isR){row.Ready=false;row.State="Nét layer chuẩn không phải cấu trúc chữ R được hỗ trợ.";continue;}
                    var sourceType=(LinetypeTableRecord)tr.GetObject(action.Standard,OpenMode.ForRead);if(sourceType.ShapeIsUcsOrientedAt(1)||Math.Abs(sourceType.ShapeRotationAt(1))>1e-10){row.Ready=false;row.State="Nét layer chuẩn không phải R=0.";continue;}
                    try{SymbolUtilityServices.ValidateSymbolName(row.Target,false);}catch(Autodesk.AutoCAD.Runtime.Exception){row.Ready=false;row.State="Tên chuẩn không hợp lệ.";continue;}if(RNameRules.OldName(row.Target)){row.Ready=false;row.State="Tên layer chuẩn đang chứa mã; chọn tên nét thủ công.";continue;}
                    action.Sources.Add(action.Standard);foreach(var c in candidates){bool r;if(relative.TryGetValue(c.Key,out r)&&r&&c.Value.Editable){action.Sources.Add(c.Key);action.UnusedCandidates.Add(c.Key);}else if(members.Any(m=>String.Equals(m.Type,c.Value.LinetypeName,StringComparison.OrdinalIgnoreCase))){row.Ready=false;row.State="Có nét mã đang dùng chưa xác nhận R hoặc cấu trúc không hỗ trợ: "+c.Value.LinetypeName;}}
                    foreach(var m in members){if(lt.Has(m.Type)){var id=lt[m.Type];DrawingLayerInfo d;bool r;if(definitions.TryGetValue(id,out d)&&d.Editable&&relative.TryGetValue(id,out r)&&r)action.Sources.Add(id);else{row.Ready=false;row.State="Có nét cùng họ chưa xác nhận R hoặc cấu trúc không hỗ trợ: "+m.Type;}}if(layers.Has(m.Layer)){var l=(LayerTableRecord)tr.GetObject(layers[m.Layer],OpenMode.ForRead);DrawingLayerInfo d;bool r;if(!l.IsDependent&&definitions.TryGetValue(l.LinetypeObjectId,out d)&&d.Editable&&RNameRules.LabelKey(d.Text)==key&&relative.TryGetValue(l.LinetypeObjectId,out r)&&r)action.Sources.Add(l.LinetypeObjectId);}}
                    if(lt.Has(row.Target)){action.ExistingTarget=lt[row.Target];DrawingLayerInfo existing;if(!definitions.TryGetValue(action.ExistingTarget,out existing)||RNameRules.LabelKey(existing.Text)!=key){row.Ready=false;row.State="Tên chuẩn đã có nhưng thuộc nội dung khác; không ghi đè.";continue;}action.Sources.Add(action.ExistingTarget);}else action.ExistingTarget=ObjectId.Null;
                    var related=definitions.Where(kv=>action.Sources.Contains(kv.Key)).ToList();row.Variants=related.Select(kv=>{bool r;relative.TryGetValue(kv.Key,out r);return kv.Value.Editable?Semantics(kv.Value,(LinetypeTableRecord)tr.GetObject(kv.Key,OpenMode.ForRead),r):kv.Value.Fingerprint;}).Distinct(StringComparer.Ordinal).Count();
                    foreach(ObjectId lid in layers){var l=(LayerTableRecord)tr.GetObject(lid,OpenMode.ForRead);if(l.IsDependent||!action.Sources.Contains(l.LinetypeObjectId))continue;action.Layers.Add(lid);if(l.IsLocked){row.Ready=false;row.State="Có layer liên quan đang khóa: "+l.Name;}}
                    var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);int byLayer=0,nested=0;foreach(ObjectId bid in blocks){var b=(BlockTableRecord)tr.GetObject(bid,OpenMode.ForRead);if(b.IsFromExternalReference||b.IsFromOverlayReference)continue;foreach(ObjectId eid in b){var e=tr.GetObject(eid,OpenMode.ForRead) as Entity;if(e==null)continue;bool explicitType=action.Sources.Contains(e.LinetypeId),isByLayer=e.LinetypeId==db.ByLayerLinetype&&action.Layers.Contains(e.LayerId);if(!explicitType&&!isByLayer)continue;var l=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);if(l.IsDependent||l.IsLocked){row.Ready=false;row.State="Có đối tượng liên quan trên layer khóa/Xref: "+l.Name;continue;}if(explicitType)action.Explicit.Add(eid);if(isByLayer)byLayer++;if(!b.IsLayout)nested++;signature.Add("E|"+eid.Handle+"|"+e.LayerId.Handle+"|"+e.LinetypeId.Handle+"|"+bid.Handle);}}
                    row.Layers=action.Layers.Count;row.Entities=action.Explicit.Count+byLayer;row.Nested=nested;row.State=row.Ready?(row.Variants>1?"Khác thông số: dùng nét layer chuẩn":"Có thể hợp nhất"):(row.State??"Không thể xử lý");row.Use=row.Ready;
                    row.Details="Chuẩn: "+row.Source+" | chữ "+baseDef.Text+" | chu kỳ "+Profile.N(baseDef.Dash+baseDef.Before+baseDef.After)+" | cao "+Profile.N(baseDef.Height)+" | "+baseDef.Font+". "+row.Variants+" kiểu thông số; "+row.Layers+" layer; "+row.Entities+" đối tượng ("+nested+" trong định nghĩa block). Các nét khác sẽ đổi theo chuẩn; scale đường không đổi, nên khoảng chữ có thể thay đổi. Xref không xử lý.\r\nCác nét liên quan:\r\n"+String.Join("\r\n",related.Select(kv=>kv.Value.LinetypeName+" | chữ="+kv.Value.Text+" | chu kỳ="+Profile.N(kv.Value.Dash+kv.Value.Before+kv.Value.After)+" | cao="+Profile.N(kv.Value.Height)+" | "+kv.Value.Font));
                    if(!action.ExistingTarget.IsNull&&action.ExistingTarget!=action.Standard)row.Details+=" Tên đích đã có: định nghĩa cũ được đổi sang tên lưu tạm dễ đọc trước khi thay bằng bản sao chuẩn; chỉ xóa khi AutoCAD xác nhận không còn dùng.";
                    foreach(var pair in related)signature.Add("T|"+pair.Key.Handle+"|"+pair.Value.Fingerprint);foreach(var lid in action.Layers){var l=(LayerTableRecord)tr.GetObject(lid,OpenMode.ForRead);signature.Add("L|"+lid.Handle+"|"+l.LinetypeObjectId.Handle+"|"+l.IsLocked);}signature.Add("G|"+row.Key+"|"+row.Target+"|"+action.Standard.Handle+"|"+row.State);
                }
                foreach(var d in definitions.Values.Where(d=>RNameRules.OldName(d.LinetypeName)))if(!standards.Any(s=>RNameRules.LabelKey(s.Text)==RNameRules.LabelKey(d.Text))&&!result.Rows.Any(r=>!r.Ready&&RNameRules.LabelKey(r.Label)==RNameRules.LabelKey(d.Text)))result.Rows.Add(new RNameReviewRow{Label=d.Text,Source=d.LinetypeName,StandardLayer="Chưa xác định",State="Chưa có layer chuẩn Mr.T cùng nhãn; cần ánh xạ thủ công.",Details="Dùng MRTRESTORERAW để tự chọn nét/layer cho đối tượng trực tiếp; nét chỉ nằm trong block cần kiểm tra riêng. Công cụ không tự đoán họ.",Ready=false});
            }
            result.Fingerprint=Profile.Hash(String.Join("|",signature.OrderBy(x=>x,StringComparer.Ordinal)));return result;
        }
        public static RNameResult Apply(Database db,RNamePlan reviewed,IEnumerable<string> selected,bool injectFailure=false)
        {
            var current=Inspect(db);if(reviewed==null||reviewed.Fingerprint!=current.Fingerprint)throw new InvalidOperationException("Bản vẽ đã đổi sau xem trước; đọc lại trước khi áp dụng.");var keys=new HashSet<string>(selected,StringComparer.OrdinalIgnoreCase);var actions=current.Actions.Where(a=>a.Row.Ready&&keys.Contains(a.Row.Key)).ToList();if(actions.Count!=keys.Count||actions.Count==0)throw new InvalidOperationException("Lựa chọn không hợp lệ; xem trước lại.");
            if(actions.GroupBy(a=>a.Row.Target,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new InvalidOperationException("Nhiều họ cùng tên đích; chưa áp dụng.");
            var overlaps=actions.SelectMany(a=>a.Sources.Select(id=>new{id,a.Row.Key})).GroupBy(x=>x.id).Any(g=>g.Select(x=>x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count()>1);if(overlaps)throw new InvalidOperationException("Một nét nguồn được dùng cho nhiều họ; xử lý từng họ để tránh gán nhầm.");
            var output=new RNameResult();using(var tr=db.TransactionManager.StartTransaction()){
                var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForWrite);var targets=new Dictionary<string,ObjectId>(StringComparer.OrdinalIgnoreCase);var purge=new ObjectIdCollection();
                foreach(var action in actions){ObjectId dest;if(action.ExistingTarget==action.Standard)dest=action.Standard;else{if(!action.ExistingTarget.IsNull){var old=(LinetypeTableRecord)tr.GetObject(action.ExistingTarget,OpenMode.ForWrite);string backup;int n=1;do{backup=action.Row.Target+"_OLD_"+n++;}while(types.Has(backup));old.Name=backup;purge.Add(old.ObjectId);}var src=(LinetypeTableRecord)tr.GetObject(action.Standard,OpenMode.ForRead);var clone=(LinetypeTableRecord)src.Clone();clone.Name=action.Row.Target;types.Add(clone);tr.AddNewlyCreatedDBObject(clone,true);dest=clone.ObjectId;}targets[action.Row.Key]=dest;foreach(var id in action.Sources)if(id!=dest&&!purge.Contains(id))purge.Add(id);}
                foreach(var action in actions){var dest=targets[action.Row.Key];foreach(var id in action.Layers){var layer=(LayerTableRecord)tr.GetObject(id,OpenMode.ForWrite);layer.LinetypeObjectId=dest;output.Layers++;}foreach(var id in action.Explicit){var e=(Entity)tr.GetObject(id,OpenMode.ForWrite);var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);e.LinetypeId=layer.LinetypeObjectId==dest?db.ByLayerLinetype:dest;output.Entities++;}output.Families++;if(injectFailure)throw new InvalidOperationException("Injected R name rollback.");}
                var requested=purge.Cast<ObjectId>().ToArray();int before=requested.Length;if(before>0){db.Purge(purge);var canPurge=new HashSet<ObjectId>(purge.Cast<ObjectId>());foreach(var id in requested.Where(x=>!canPurge.Contains(x))){var type=tr.GetObject(id,OpenMode.ForRead) as LinetypeTableRecord;output.RetainedNames.Add(type==null?id.Handle.ToString():type.Name);}foreach(ObjectId id in purge){var obj=tr.GetObject(id,OpenMode.ForWrite);obj.Erase();output.Purged++;}}output.Retained=before-output.Purged;tr.Commit();
            }return output;
        }
    }
}
