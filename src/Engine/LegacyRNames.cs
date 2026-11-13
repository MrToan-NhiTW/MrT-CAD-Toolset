using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using MedGas2024.Core;
namespace MedGas2024.Engine
{
    public sealed class LegacyRName
    {
        public string Before{get;set;}public string After{get;set;}public string Label{get;set;}public string State{get;set;}public int Objects{get;set;}
        public ObjectId Source,Target;public string SourceState,TargetState;public bool Ready;
    }
    public static class LegacyRNames
    {
        public static List<LegacyRName> Read(Database db)
        {
            var rows=new List<LegacyRName>();var catalog=LabelService.Read(db);
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                foreach(var def in catalog.Definitions.Where(d=>Regex.IsMatch(d.LinetypeName,@"^Mr\.T_R0_[0-9a-f]{16}(?:_[0-9a-f]{8})?(?:_D_[0-9a-f]{8})*(?:_SP_[0-9]+)?$",RegexOptions.IgnoreCase)))
                {
                    var members=catalog.Members.Where(m=>m.Type==def.LinetypeName).ToList();var row=new LegacyRName{Source=types[def.LinetypeName],Before=def.LinetypeName,Label=def.Text,Objects=members.Count,SourceState=def.Fingerprint};rows.Add(row);
                    var targets=new HashSet<ObjectId>();foreach(var member in members){if(!layers.Has(member.Layer))continue;var layer=(LayerTableRecord)tr.GetObject(layers[member.Layer],OpenMode.ForRead);var type=(LinetypeTableRecord)tr.GetObject(layer.LinetypeObjectId,OpenMode.ForRead);if(type.ObjectId==row.Source||!Regex.IsMatch(type.Name,@"^.+_.+_D\d+_.+$"))continue;var target=LabelService.Definition(type,tr);if(target!=null&&String.Equals(LabelRules.Canonical(target.Text),LabelRules.Canonical(def.Text),StringComparison.OrdinalIgnoreCase))targets.Add(type.ObjectId);}
                    if(targets.Count!=1){row.State="Bỏ qua: không xác định duy nhất nét gốc từ Layer; không tự đoán.";continue;}row.Target=targets.Single();var td=(LinetypeTableRecord)tr.GetObject(row.Target,OpenMode.ForRead);row.After=td.Name;row.TargetState=LabelService.Definition(td,tr).Fingerprint;row.Ready=true;row.State="Chuyển định nghĩa R hiện tại vào tên gốc; giữ font/chữ/khoảng nét. Không xóa nét cũ.";
                }
                foreach(var conflict in rows.Where(r=>r.Ready).GroupBy(r=>r.Target).Where(g=>g.Count()>1))foreach(var row in conflict){row.Ready=false;row.State="Bỏ qua: nhiều bản R khác nhau cùng trỏ một tên gốc; cần chọn mẫu chuẩn riêng.";}
            }return rows;
        }
        public static int Apply(Database db,List<LegacyRName> reviewed)
        {
            var fresh=Read(db);foreach(var old in reviewed.Where(r=>r.Ready))if(!fresh.Any(r=>r.Ready&&r.Source==old.Source&&r.Target==old.Target&&r.SourceState==old.SourceState&&r.TargetState==old.TargetState))throw new InvalidOperationException("Nét/Layer đã đổi sau khi xem trước; chưa chuẩn hóa.");
            var changes=reviewed.Where(r=>r.Ready).ToList();if(changes.Count==0)return 0;using(var tr=db.TransactionManager.StartTransaction())
            {
                foreach(var row in changes)LabelService.RestoreLegacyR(db,tr,row.Source,row.Target);
                var mapping=changes.ToDictionary(r=>r.Source,r=>r.Target);var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);foreach(ObjectId lid in layers){var l=(LayerTableRecord)tr.GetObject(lid,OpenMode.ForRead);ObjectId to;if(mapping.TryGetValue(l.LinetypeObjectId,out to)){if(l.IsDependent||l.IsLocked)throw new InvalidOperationException("Layer khóa/Xref đang dùng nét mã; chưa hợp nhất.");l.UpgradeOpen();l.LinetypeObjectId=to;}}
                var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);foreach(ObjectId bid in blocks){var b=(BlockTableRecord)tr.GetObject(bid,OpenMode.ForRead);if(b.IsFromExternalReference||b.IsFromOverlayReference)continue;foreach(ObjectId eid in b){var e=tr.GetObject(eid,OpenMode.ForRead) as Entity;if(e==null)continue;ObjectId to;if(!mapping.TryGetValue(e.LinetypeId,out to))continue;var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);if(layer.IsLocked||layer.IsDependent)throw new InvalidOperationException("Có đối tượng trên Layer khóa/Xref; chưa hợp nhất.");e.UpgradeOpen();e.LinetypeId=layer.LinetypeObjectId==to?db.ByLayerLinetype:to;}}
                tr.Commit();return changes.Count;
            }
        }
    }
}
