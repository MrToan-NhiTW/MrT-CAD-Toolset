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
    }
    public static class RoomNameMatcher
    {
        public static bool SingleLineSafe(string text)
        {
            return !String.IsNullOrWhiteSpace(text)&&text.Length<=255&&text.IndexOfAny(new[]{'\r','\n'})<0
                &&!text.Contains("%%")&&!text.Contains("\\U+")&&!text.Contains("%<");
        }
        public static List<T> Match<T>(IEnumerable<T> found,RoomCatalog catalog,bool newMode) where T:RoomNameMatch,new()
        {
            var hits=found.ToList();
            var attached=new HashSet<string>(hits.Where(h=>h.Tag!=null).Select(h=>h.Tag),StringComparer.Ordinal);
            var byName=catalog.Unique.Values.ToLookup(r=>RoomCatalog.MatchName(r.OldName),StringComparer.OrdinalIgnoreCase);
            var names=hits.Where(h=>h.Tag==null).ToLookup(h=>RoomCatalog.MatchName(h.Text),StringComparer.OrdinalIgnoreCase);
            foreach(var group in names)
            {
                var rows=byName[group.Key].ToList();
                foreach(var h in group)
                {
                    if(rows.Count==1)h.Entry=rows[0];
                    if(h.Status!=null)continue;
                    if(catalog.BlockedNames.Contains(group.Key)||rows.Count>1)h.Status="Tên cũ ứng với nhiều ID hoặc dòng Excel có lỗi";
                    else if(rows.Count==0)h.Status="Không khớp tên cũ trong Excel";
                    else if(group.Count()>1)h.Status="Nhiều vị trí cùng tên trong DWG";
                    else if(attached.Contains(h.Entry.Id))h.Status="ID đã gắn trong không gian này";
                    else if(String.IsNullOrWhiteSpace(RoomRules.Desired(h.Entry,newMode)))h.Status="Tên cần hiển thị đang trống";
                    else if(h.SingleLine&&!SingleLineSafe(RoomRules.Desired(h.Entry,newMode)))h.Status="Tên mới không phù hợp với TEXT một dòng";
                    else {h.Ready=true;h.Status="Sẵn sàng";}
                }
            }
            foreach(var entry in catalog.Unique.Values)
                if(!attached.Contains(entry.Id)&&!names.Contains(RoomCatalog.MatchName(entry.OldName)))
                    hits.Add(new T{Entry=entry,Text=entry.OldName,Status="Chưa tìm thấy vị trí trên layer nguồn"});
            return hits;
        }

    }
}
