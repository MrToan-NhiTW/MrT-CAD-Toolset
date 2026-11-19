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
        // One old room split into several new rooms: one label per entry.
        public List<RoomEntry> Splits;
        // Plain TEXT/MTEXT directly in the space (can be replaced); false for attributes.
        public bool Direct;
        // A 2.0.2.4 MTEXT label made for block text; with room blocks it is replaced, not counted as attached.
        public bool LegacyLabel;
        public RoomNameMatch Supersedes;
        // Needs a human check (split assigned by position, split stacked, repeated code): drawn in the review colour.
        public bool Review;public string Note;
        // World position of the text, used to give split rooms to positions left to right.
        public double X,Y;
    }
    public sealed class RoomMatchOptions
    {
        public bool CreateLabels,AllowRepeats;
        // Room blocks (2.0.2.4): every match becomes a Mr.T_PHONG block; direct old text is replaced.
        public bool Blocks,ReplaceOld;
    }
    public static class RoomNameMatcher
    {
        public const string ActionAdopt="Gắn trực tiếp",ActionLabel="Tạo nhãn riêng",ActionNone="—",
            ActionUpgrade="Đổi nhãn MTEXT cũ thành block",ActionBlock="Tạo block phòng (giữ chữ cũ)",ActionReplace="Thay chữ cũ bằng block",ActionOver="Tạo block (chữ cũ trong block giữ nguyên)";
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
            foreach(var h in hits){h.Status=h.Blocked;h.Ready=false;h.Action=null;h.ExcelTargets=null;h.Splits=null;h.Supersedes=null;h.Review=false;h.Note=null;if(h.Tag==null)h.Entry=null;}
            var legacy=new Dictionary<string,T>(StringComparer.OrdinalIgnoreCase);
            if(options.Blocks)foreach(var h in hits.Where(h=>h.Tag!=null&&h.LegacyLabel&&!String.IsNullOrEmpty(h.Linked)))if(!legacy.ContainsKey(h.Linked))legacy[h.Linked]=h;
            var tagged=hits.Where(h=>h.Tag!=null&&!legacy.Values.Contains(h)).ToList();
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
                bool splitRows=rows.Count>1&&rows.All(r=>r.SplitOf!=null&&r.SplitOf==rows[0].SplitOf);
                // One old room split into n new rooms found at n places: give each place one new
                // room, places left to right (then top to bottom), names in Excel row order.
                var assigned=new Dictionary<T,int>();
                if(splitRows&&options.Blocks&&open.Count>1&&open.Count==rows.Count)
                {
                    int k=0;foreach(var h in open.OrderBy(h=>Math.Round(h.X,3)).ThenByDescending(h=>h.Y))assigned[h]=k++;
                }
                foreach(var h in group)
                {
                    bool split=splitRows;int slot;bool given=assigned.TryGetValue(h,out slot);
                    if(given){h.Entry=rows[slot];h.Splits=new List<RoomEntry>{rows[slot]};}
                    else{if(rows.Count==1||split)h.Entry=rows[0];if(split)h.Splits=rows;}
                    if(rows.Count>1)h.ExcelTargets=String.Join(" | ",rows.Select(r=>r.NewName).Distinct());
                    if(conflict!=null)h.ExcelTargets=String.Join(" | ",conflict);
                    h.Action=ActionNone;
                    if(h.Status!=null)continue;
                    if(h.Place!=null&&labelled.Contains(h.Place)){h.Status="Đã có nhãn riêng tại vị trí này";continue;}
                    string desired=RoomRules.Desired(h.Entry,newMode);
                    if(conflict!=null)h.Status="Xung đột Excel: mã cũ có nhiều tên mới ("+h.ExcelTargets+")";
                    else if(catalog.ErrorNames.Contains(group.Key))h.Status="Dòng Excel của mã này có lỗi — bấm Xem lỗi Excel";
                    else if(catalog.BlockedNames.Contains(group.Key)||(rows.Count>1&&h.Splits==null))h.Status="Tên cũ ứng với nhiều ID trong Excel";
                    else if(rows.Count==0)h.Status="Thiếu ánh xạ: không có dòng Excel cho chữ này";
                    else if(splitRows&&options.Blocks&&open.Count>1&&!given)h.Status=open.Count+" vị trí nhưng "+rows.Count+" tên mới — chọn điểm riêng cho từng phòng";
                    else if(open.Count>1&&!given&&!options.AllowRepeats)h.Status=open.Count+" vị trí cùng mã trong DWG — bật «Áp dụng mọi vị trí trùng mã» hoặc chọn điểm riêng";
                    else if((h.Splits??new List<RoomEntry>{h.Entry}).Any(r=>attached.Contains(r.Id)&&(!options.AllowRepeats||direct.Contains(r.Id))))h.Status="ID đã gắn trong không gian này";
                    else if(String.IsNullOrWhiteSpace(desired))h.Status="Tên cần hiển thị đang trống";
                    else if(h.NeedsLabel&&!options.CreateLabels&&!options.Blocks)h.Status="Chữ trong block dùng chung — bật «Tạo nhãn riêng» để áp dụng";
                    else if(!options.Blocks&&!h.NeedsLabel&&h.SingleLine&&!SingleLineSafe(desired))h.Status="Tên mới không phù hợp với TEXT một dòng";
                    else
                    {
                        h.Ready=true;
                        h.Action=options.Blocks?(h.Direct?(options.ReplaceOld?ActionReplace:ActionBlock):ActionOver):h.NeedsLabel?ActionLabel:ActionAdopt;
                        h.Status=open.Count>1?"Sẵn sàng ("+open.Count+" vị trí cùng mã)":"Sẵn sàng";
                        if(given)
                        {
                            h.Status="Sẵn sàng — tách "+rows[0].SplitOf+": vị trí "+(slot+1)+"/"+rows.Count+" → "+h.Entry.NewName+" (cần kiểm tra)";
                            h.Review=true;h.Note="Tách "+rows[0].SplitOf+": gán vị trí "+(slot+1)+"/"+rows.Count+" theo thứ tự trái→phải, cần kiểm tra";
                        }
                        else if(h.Splits!=null)
                        {
                            h.Status+=" — tách "+h.Splits.Count+" phòng (cần kiểm tra)";
                            h.Review=true;h.Note="Tách "+h.Splits.Count+" phòng xếp chồng tại vị trí cũ — kéo vào đúng phòng";
                        }
                        else if(open.Count>1)
                        {
                            h.Status+=" (cần kiểm tra)";
                            h.Review=true;h.Note=open.Count+" vị trí cùng mã "+h.Text+" trong bản vẽ — cần kiểm tra";
                        }
                        T old;if(h.Place!=null&&legacy.TryGetValue(h.Place,out old)){h.Supersedes=old;h.Action=ActionUpgrade;}
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
