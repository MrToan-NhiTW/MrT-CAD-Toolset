using System;
using System.Collections.Generic;
using System.Globalization;
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
        // Address (INSERT handles/text handle) of the block text a separate label was created for.
        [DataMember(EmitDefaultValue=false)] public string Source;
        // Room block labels (2.0.2.4): display in upper case, the frame polyline handle
        // and its shape so switching names keeps the same look.
        [DataMember(EmitDefaultValue=false)] public bool Upper;
        [DataMember(EmitDefaultValue=false)] public string Frame,FrameShape,Status;
        [DataMember(EmitDefaultValue=false)] public double FramePad,FrameRadius;
        // 2.0.2.5: WIPEOUT matching the frame (Mask) and its handle.
        [DataMember(EmitDefaultValue=false)] public bool Mask;
        [DataMember(EmitDefaultValue=false)] public string Wipeout;
        // 2.0.2.7: name layout (Max lines with automatic column width, Width factor, Fixed width)
        // and the review flag behind the Checked column. LayoutSet/ReviewSet mark tags that carry them.
        [DataMember(EmitDefaultValue=false)] public bool LayoutSet,ReviewSet,Review;
        [DataMember(EmitDefaultValue=false)] public int MaxLines;
        [DataMember(EmitDefaultValue=false)] public double WidthFactor,FixedWidth;
        public string Serialize(){using(var s=new MemoryStream()){new DataContractJsonSerializer(typeof(RoomTag)).WriteObject(s,this);return Encoding.UTF8.GetString(s.ToArray());}}
        public static RoomTag Parse(string json){if(json==null||json.Length>100000)throw new ArgumentException("Dữ liệu tên phòng quá dài.");using(var s=new MemoryStream(Encoding.UTF8.GetBytes(json))){var v=(RoomTag)new DataContractJsonSerializer(typeof(RoomTag)).ReadObject(s);if(v==null||v.Schema!=1||String.IsNullOrEmpty(v.Id))throw new ArgumentException("Dữ liệu tên phòng không hợp lệ.");return v;}}
    }
    public sealed class RoomEntry
    {
        public string Id{get;set;}public string OldName{get;set;}public string NewName{get;set;}public int ExcelRow{get;set;}
        // Đổi tên / Giữ tên / Ghép (several old rooms -> one new name) / Tách (one old room -> several new names).
        public string Status{get;set;}
        // Base ID when one old room is split; this entry's ID is SplitOf-1, SplitOf-2...
        public string SplitOf{get;set;}
    }
    public sealed class RoomCatalog
    {
        public readonly Dictionary<string,RoomEntry> Unique=new Dictionary<string,RoomEntry>(StringComparer.Ordinal);
        public readonly HashSet<string> Ambiguous=new HashSet<string>(StringComparer.Ordinal);
        public const string DefaultLayer="Mr.T Tên phòng";
        // Default target for room blocks; old names often already sit on DefaultLayer.
        public const string NewLayer="Mr.T Tên phòng mới";
        public const string DefaultStyle="Mr.T Arial";
        public const string StatusRename="Đổi tên",StatusKeep="Giữ tên",StatusMerge="Ghép",StatusSplit="Tách",
            StatusPending="Chưa rõ tên mới",StatusNotInExcel="Chưa có trong Excel";
        // "(Chưa rõ)", "?", "-"... in the new-name column mean no new name yet, not a name shared by many rooms.
        public static bool IsPlaceholder(string name)
        {
            var k=String.Join(" ",new string(RoomSearch.Fold(name).Select(ch=>Char.IsLetterOrDigit(ch)||ch=='?'?ch:' ').ToArray()).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries));
            return k.Length==0||k=="?"||k=="chua ro"||k=="chua co"||k=="chua xac dinh"||k=="chua co ten"||k=="x"||k=="na"||k=="n a"||k=="tbd";
        }
        public readonly HashSet<string> BlockedNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Old name -> the different new names Excel gives it (conflict), and old names whose rows had cell errors.
        public readonly Dictionary<string,List<string>> ConflictTargets=new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> ErrorNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Conflict(string old,params string[] targets)
        {
            var key=MatchName(old);List<string> list;if(!ConflictTargets.TryGetValue(key,out list))ConflictTargets[key]=list=new List<string>();
            foreach(var t in targets)if(!list.Contains(t??""))list.Add(t??"");
        }
        public static string MatchName(string text)
        {
            return String.Join(" ",Key(text).Split((char[])null,StringSplitOptions.RemoveEmptyEntries));
        }
        public readonly List<string> Problems=new List<string>();
        // The original name is an identifier, so do not remove accents, change
        // case, or collapse interior whitespace to guess a match.
        public static string Key(string value){return (value??"").Trim().Normalize(NormalizationForm.FormC);}
        public static RoomCatalog Build(IEnumerable<RoomEntry> input)
        {
            var c=new RoomCatalog();var order=new List<string>();var groups=new Dictionary<string,List<RoomEntry>>(StringComparer.Ordinal);
            foreach(var source in input)
            {
                var id=Key(source.Id);var old=(source.OldName??"").Trim();var next=(source.NewName??"").Trim();if(IsPlaceholder(next))next="";
                if(id.Length==0){if(old.Length>0)c.BlockedNames.Add(MatchName(old));if(old.Length>0||next.Length>0)c.Problems.Add("Dòng "+source.ExcelRow+": thiếu ID/tên gốc.");continue;}
                List<RoomEntry> list;if(!groups.TryGetValue(id,out list)){groups[id]=list=new List<RoomEntry>();order.Add(id);}
                // Identical repeated rows are one room.
                if(!list.Any(r=>r.OldName==old&&r.NewName==next))list.Add(new RoomEntry{Id=id,OldName=old,NewName=next,ExcelRow=source.ExcelRow});
            }
            foreach(var id in order)
            {
                var rows=groups[id];
                if(rows.Count==1){c.Unique[id]=rows[0];continue;}
                bool split=rows.All(r=>r.OldName==rows[0].OldName)&&rows.All(r=>r.NewName.Length>0)&&
                    Enumerable.Range(1,rows.Count).All(k=>!groups.ContainsKey(id+"-"+k));
                if(split)
                {
                    // One old room becomes several new rooms: each gets its own ID (402-1, 402-2...).
                    for(int k=0;k<rows.Count;k++)
                    {
                        var r=rows[k];c.Unique[id+"-"+(k+1)]=new RoomEntry{Id=id+"-"+(k+1),OldName=r.OldName,NewName=r.NewName,ExcelRow=r.ExcelRow,SplitOf=id,Status=StatusSplit};
                    }
                    c.Problems.Add("Tách phòng "+id+": "+String.Join(" | ",rows.Select(r=>r.NewName))+" → ID "+String.Join(", ",Enumerable.Range(1,rows.Count).Select(k=>id+"-"+k))+" (dòng "+String.Join(", ",rows.Select(r=>r.ExcelRow))+").");
                    continue;
                }
                c.Ambiguous.Add(id);
                foreach(var r in rows){c.BlockedNames.Add(MatchName(r.OldName));c.Conflict(r.OldName,rows.Select(x=>x.NewName).ToArray());}
                c.Problems.Add("ID trùng có nội dung khác: "+id+" (dòng "+String.Join(", ",rows.Select(r=>r.ExcelRow))+").");
            }
            var merged=c.Unique.Values.Where(r=>r.SplitOf==null&&r.NewName.Length>0).GroupBy(r=>r.NewName,StringComparer.CurrentCultureIgnoreCase)
                .Where(g=>g.Select(r=>MatchName(r.OldName)).Distinct(StringComparer.OrdinalIgnoreCase).Count()>1).SelectMany(g=>g);
            var mergedSet=new HashSet<RoomEntry>(merged);
            foreach(var r in c.Unique.Values.Where(r=>r.SplitOf==null))
                r.Status=r.NewName.Length==0?StatusPending:mergedSet.Contains(r)?StatusMerge:
                    String.Equals(MatchName(r.OldName),MatchName(r.NewName),StringComparison.CurrentCultureIgnoreCase)?StatusKeep:StatusRename;
            return c;
        }
        public RoomEntry Find(string id){RoomEntry row;return Unique.TryGetValue(Key(id),out row)?row:null;}
    }
    public static class RoomRules
    {
        static readonly CultureInfo Vietnamese=new CultureInfo("vi-VN");
        public static string Desired(RoomEntry row,bool newName){return row==null?null:(newName?row.NewName:row.OldName);}
        public static string Display(RoomEntry row,bool newName,bool upper)
        {
            var text=Desired(row,newName);return text==null||!upper?text:text.ToUpper(Vietnamese);
        }
        // Text a new room block shows: the new name, or the old one while Excel has no new name yet.
        public static string Label(RoomEntry row,bool newName,bool upper)
        {
            var text=Display(row,newName,upper);
            return String.IsNullOrWhiteSpace(text)&&newName?Display(row,false,upper):text;
        }
        public static bool CanUpdate(RoomTag tag,string displayed,RoomEntry row,bool newName,out string reason)
        {
            if(tag==null){reason="Không có ID Mr.T.";return false;}
            if(row==null){reason="Không có ID duy nhất trong Excel.";return false;}
            if(!String.Equals(RoomLayout.Plain(displayed),RoomLayout.Plain(tag.LastText),StringComparison.Ordinal)){reason="Chữ đã sửa thủ công; cần kiểm tra.";return false;}
            if(String.IsNullOrWhiteSpace(Desired(row,newName))){reason=newName?"Tên mới đang trống.":"Tên cũ đang trống.";return false;}
            reason="";return true;
        }
    }

    // How new room labels look (block Mr.T_PHONG). Remembered between sessions.
    [DataContract]
    public sealed class RoomLabelStyle
    {
        public const string FrameRounded="Chữ nhật bo góc",FrameSlot="Viên thuốc",FrameRect="Chữ nhật",FrameNone="Không khung";
        public static readonly string[] Frames={FrameRounded,FrameSlot,FrameRect,FrameNone};
        [DataMember] public string Layer=RoomCatalog.NewLayer,Style=RoomCatalog.DefaultStyle,Frame=FrameRounded;
        // Height in drawing units; width 0 = no wrapping; pad and radius are multiples of the height.
        [DataMember] public double Height=250,Width=0,Pad=0.4,Radius=0.6;
        [DataMember] public bool Upper=true,ReplaceOld=true,Mask;
        // AutoCAD colour index: normal rooms and rooms that need a check (split by position, stacked split, repeated code).
        [DataMember] public int Color=158,ReviewColor=1;
        // 2.0.2.7: wrap to at most MaxLines lines (0 = one line) unless Width (Fixed width) is set; WidthFactor squeezes letters.
        [DataMember] public int MaxLines=3;[DataMember] public double WidthFactor=1;[DataMember] public bool LayoutDecided;
        public RoomLabelStyle Copy(){return (RoomLabelStyle)MemberwiseClone();}
        // Settings read from an older or edited file may miss values.
        public RoomLabelStyle Valid()
        {
            var v=Copy();
            if(String.IsNullOrWhiteSpace(v.Layer))v.Layer=RoomCatalog.NewLayer;
            if(String.IsNullOrWhiteSpace(v.Style))v.Style=RoomCatalog.DefaultStyle;
            if(!Frames.Contains(v.Frame))v.Frame=FrameRounded;
            if(!Profile.Finite(v.Height)||v.Height<=0)v.Height=250;
            if(!Profile.Finite(v.Width)||v.Width<0)v.Width=0;
            if(!Profile.Finite(v.Pad)||v.Pad<0||v.Pad>10)v.Pad=0.4;
            if(!Profile.Finite(v.Radius)||v.Radius<0||v.Radius>10)v.Radius=0.6;
            if(v.Color<1||v.Color>255)v.Color=158;
            if(v.ReviewColor<1||v.ReviewColor>255)v.ReviewColor=1;
            // Settings saved before 2.0.2.7 used a fixed MTEXT width; switch them to Max lines 3.
            if(!v.LayoutDecided){v.MaxLines=3;v.WidthFactor=1;v.Width=0;v.LayoutDecided=true;}
            if(v.MaxLines<0||v.MaxLines>20)v.MaxLines=3;
            if(!Profile.Finite(v.WidthFactor)||v.WidthFactor<0.3||v.WidthFactor>3)v.WidthFactor=1;
            return v;
        }
    }

    // Everything the room form remembers between sessions (per Windows user).
    [DataContract]
    public sealed class RoomSettings
    {
        [DataMember] public string ExcelPath,Sheet,SourceLayer;
        [DataMember] public int HeaderRow,OldColumn,NewColumn,IdColumn;
        [DataMember] public bool ShowNew,AllowRepeats,HideSource;
        // 2.0.2.5 defaults: apply repeated codes and keep rooms missing from Excel (both flagged for review).
        [DataMember] public bool RepeatsDecided,KeepUnmatched=true;
        [DataMember] public RoomLabelStyle Label=new RoomLabelStyle();
        public static string DefaultPath{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MrT","CADToolset","rooms-settings.json");}}
        public static RoomSettings Load(string path)
        {
            try
            {
                if(path==null||!File.Exists(path)||new FileInfo(path).Length>200000)return new RoomSettings();
                using(var s=File.OpenRead(path)){var v=(RoomSettings)new DataContractJsonSerializer(typeof(RoomSettings)).ReadObject(s);if(v!=null&&v.Label==null)v.Label=new RoomLabelStyle();return v??new RoomSettings();}
            }
            catch(Exception){return new RoomSettings();}
        }
        public void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using(var s=new MemoryStream()){new DataContractJsonSerializer(typeof(RoomSettings)).WriteObject(s,this);File.WriteAllBytes(path,s.ToArray());}
            }
            catch(Exception){}
        }
    }
}
