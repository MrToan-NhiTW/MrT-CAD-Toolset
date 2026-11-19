using System;
using System.Collections.Generic;
using System.Linq;

namespace MedGas2024.Core
{
    public class RoomNameMatch
    {
        public string Text,Layer,Status,Tag;
        public RoomEntry Entry;
        public bool Ready,SingleLine;
        // Reason found while reading the drawing (hidden, locked, field...). Match
        // copies it to Status, so the same scan can be matched again with other options.
        public string Blocked;
        // Text inside a shared block definition: it cannot carry its own ID, so a
        // separate MTEXT label is created at this instance's position.
        public bool NeedsLabel;
        // Unique address of this occurrence (INSERT path + handle) and, for a managed
        // label, the address of the block text it was created for.
        public string Place,Linked;
        public string Action,ExcelTargets;
    }
    public sealed class RoomMatchOptions
    {
        public bool CreateLabels,AllowRepeats;
    }
    public static class RoomNameMatcher
    {
        public const string ActionAdopt="Gắn trực tiếp",ActionLabel="Tạo nhãn riêng",ActionNone="—";
        public static bool SingleLineSafe(string text)
        {
            return !String.IsNullOrWhiteSpace(text)&&text.Length<=255&&text.IndexOfAny(new[]{'\r','\n'})<0
                &&!text.Contains("%%")&&!text.Contains("\\U+")&&!text.Contains("%<");
        }
        public static List<T> Match<T>(IEnumerable<T> found,RoomCatalog catalog,bool newMode) where T:RoomNameMatch,new()
        {
            return Match(found,catalog,newMode,new RoomMatchOptions());
        }
        public static List<T> Match<T>(IEnumerable<T> found,RoomCatalog catalog,bool newMode,RoomMatchOptions options) where T:RoomNameMatch,new()
        {
            options=options??new RoomMatchOptions();
            var hits=found.ToList();
            foreach(var h in hits){h.Status=h.Blocked;h.Ready=false;h.Action=null;h.ExcelTargets=null;if(h.Tag==null)h.Entry=null;}
            var tagged=hits.Where(h=>h.Tag!=null).ToList();
            var attached=new HashSet<string>(tagged.Select(h=>h.Tag),StringComparer.Ordinal);
            // IDs attached directly to drawing text or placed by hand (not a label made for a block text).
            var direct=new HashSet<string>(tagged.Where(h=>String.IsNullOrEmpty(h.Linked)).Select(h=>h.Tag),StringComparer.Ordinal);
            var labelled=new HashSet<string>(tagged.Where(h=>!String.IsNullOrEmpty(h.Linked)).Select(h=>h.Linked),StringComparer.OrdinalIgnoreCase);
            var byName=catalog.Unique.Values.ToLookup(r=>RoomCatalog.MatchName(r.OldName),StringComparer.OrdinalIgnoreCase);
            var names=hits.Where(h=>h.Tag==null).ToLookup(h=>RoomCatalog.MatchName(h.Text),StringComparer.OrdinalIgnoreCase);
            foreach(var group in names)
            {
                var rows=byName[group.Key].ToList();
                List<string> conflict;catalog.ConflictTargets.TryGetValue(group.Key,out conflict);
                var open=group.Where(h=>h.Blocked==null&&!(h.Place!=null&&labelled.Contains(h.Place))).ToList();
                foreach(var h in group)
                {
                    if(rows.Count==1)h.Entry=rows[0];
                    else if(rows.Count>1)h.ExcelTargets=String.Join(" | ",rows.Select(r=>r.NewName).Distinct());
                    if(conflict!=null)h.ExcelTargets=String.Join(" | ",conflict);
                    h.Action=ActionNone;
                    if(h.Status!=null)continue;
                    if(h.Place!=null&&labelled.Contains(h.Place)){h.Status="Đã có nhãn riêng tại vị trí này";continue;}
                    string desired=RoomRules.Desired(h.Entry,newMode);
                    if(conflict!=null)h.Status="Xung đột Excel: mã cũ có nhiều tên mới ("+h.ExcelTargets+")";
                    else if(catalog.ErrorNames.Contains(group.Key))h.Status="Dòng Excel của mã này có lỗi — bấm Xem lỗi Excel";
                    else if(catalog.BlockedNames.Contains(group.Key)||rows.Count>1)h.Status="Tên cũ ứng với nhiều ID trong Excel";
                    else if(rows.Count==0)h.Status="Thiếu ánh xạ: không có dòng Excel cho chữ này";
                    else if(open.Count>1&&!options.AllowRepeats)h.Status=open.Count+" vị trí cùng mã trong DWG — bật «Áp dụng mọi vị trí trùng mã» hoặc chọn điểm riêng";
                    else if(attached.Contains(h.Entry.Id)&&(!options.AllowRepeats||direct.Contains(h.Entry.Id)))h.Status="ID đã gắn trong không gian này";
                    else if(String.IsNullOrWhiteSpace(desired))h.Status="Tên cần hiển thị đang trống";
                    else if(h.NeedsLabel&&!options.CreateLabels)h.Status="Chữ trong block dùng chung — bật «Tạo nhãn riêng» để áp dụng";
                    else if(!h.NeedsLabel&&h.SingleLine&&!SingleLineSafe(desired))h.Status="Tên mới không phù hợp với TEXT một dòng";
                    else
                    {
                        h.Ready=true;h.Action=h.NeedsLabel?ActionLabel:ActionAdopt;
                        h.Status=open.Count>1?"Sẵn sàng ("+open.Count+" vị trí cùng mã)":"Sẵn sàng";
                    }
                }
            }
            foreach(var entry in catalog.Unique.Values)
                if(!attached.Contains(entry.Id)&&!names.Contains(RoomCatalog.MatchName(entry.OldName)))
                    hits.Add(new T{Entry=entry,Text=entry.OldName,Action=ActionNone,Status="Chưa tìm thấy vị trí trên layer nguồn"});
            return hits;
        }
    }
}
