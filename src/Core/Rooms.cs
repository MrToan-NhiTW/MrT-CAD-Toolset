using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;

namespace MedGas2024.Core
{
    [DataContract]
    public sealed class RoomTag
    {
        [DataMember] public int Schema=1;
        [DataMember] public string Id,Original,OldName,NewName,LastText;
        [DataMember] public bool ShowingNew;
        public string Serialize(){using(var s=new MemoryStream()){new DataContractJsonSerializer(typeof(RoomTag)).WriteObject(s,this);return Encoding.UTF8.GetString(s.ToArray());}}
        public static RoomTag Parse(string json){if(json==null||json.Length>100000)throw new ArgumentException("Dữ liệu tên phòng quá dài.");using(var s=new MemoryStream(Encoding.UTF8.GetBytes(json))){var v=(RoomTag)new DataContractJsonSerializer(typeof(RoomTag)).ReadObject(s);if(v==null||v.Schema!=1||String.IsNullOrEmpty(v.Id))throw new ArgumentException("Dữ liệu tên phòng không hợp lệ.");return v;}}
    }
    public sealed class RoomEntry
    {
        public string Id{get;set;}public string OldName{get;set;}public string NewName{get;set;}public int ExcelRow{get;set;}
    }
    public sealed class RoomCatalog
    {
        public readonly Dictionary<string,RoomEntry> Unique=new Dictionary<string,RoomEntry>(StringComparer.Ordinal);
        public readonly HashSet<string> Ambiguous=new HashSet<string>(StringComparer.Ordinal);
        public readonly List<string> Problems=new List<string>();
        // The original name is an identifier, so do not remove accents, change
        // case, or collapse interior whitespace to guess a match.
        public static string Key(string value){return (value??"").Trim().Normalize(NormalizationForm.FormC);}
        public static RoomCatalog Build(IEnumerable<RoomEntry> input)
        {
            var c=new RoomCatalog();foreach(var source in input){var id=Key(source.Id);var old=(source.OldName??"").Trim();var next=(source.NewName??"").Trim();
                if(id.Length==0){if(old.Length>0||next.Length>0)c.Problems.Add("Dòng "+source.ExcelRow+": thiếu ID/tên gốc.");continue;}
                var row=new RoomEntry{Id=id,OldName=old,NewName=next,ExcelRow=source.ExcelRow};RoomEntry first;
                if(c.Unique.TryGetValue(id,out first)){
                    if(first.OldName!=row.OldName||first.NewName!=row.NewName){c.Unique.Remove(id);c.Ambiguous.Add(id);c.Problems.Add("ID trùng có nội dung khác: "+id+" (dòng "+first.ExcelRow+", "+row.ExcelRow+").");}
                }else if(!c.Ambiguous.Contains(id))c.Unique.Add(id,row);
            }return c;
        }
        public RoomEntry Find(string id){RoomEntry row;return Unique.TryGetValue(Key(id),out row)?row:null;}
    }
    public static class RoomRules
    {
        public static string Desired(RoomEntry row,bool newName){return row==null?null:(newName?row.NewName:row.OldName);}
        public static bool CanUpdate(RoomTag tag,string displayed,RoomEntry row,bool newName,out string reason)
        {
            if(tag==null){reason="Không có ID Mr.T.";return false;}
            if(row==null){reason="Không có ID duy nhất trong Excel.";return false;}
            if(!String.Equals(displayed??"",tag.LastText??"",StringComparison.Ordinal)){reason="Chữ đã sửa thủ công; cần kiểm tra.";return false;}
            if(String.IsNullOrWhiteSpace(Desired(row,newName))){reason=newName?"Tên mới đang trống.":"Tên cũ đang trống.";return false;}
            reason="";return true;
        }
    }
}
