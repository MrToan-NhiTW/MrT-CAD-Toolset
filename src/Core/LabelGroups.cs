using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
namespace MedGas2024.Core
{
    public sealed class LabelMember
    {
        public string Handle,Layer,Type,Text,Color,State;public double Scale,NativeHeight;public bool Locked,Linked;
    }
    public sealed class LabelGroup
    {
        public double MinScale{get;set;}
        public string Key{get;set;}public string Label{get;set;}public int Lines{get;set;}public int Layers{get;set;}public int Types{get;set;}public int Locked{get;set;}public int Linked{get;set;}public string Colors{get;set;}public string Scales{get;set;}
    }
    public sealed class LabelCatalog
    {
        public string Fingerprint;public int NoLabel,Ignored;public readonly List<LabelMember> Members=new List<LabelMember>();public readonly List<DrawingLayerInfo> Definitions=new List<DrawingLayerInfo>();
        public List<LabelGroup> Groups(bool system)
        {
            return Members.GroupBy(m=>LabelRules.Key(m.Text,system),StringComparer.OrdinalIgnoreCase).Select(g=>new LabelGroup{MinScale=g.Min(x=>x.Scale),Key=g.Key,Label=system?LabelRules.System(g.First().Text)+" (mọi cỡ)":LabelRules.Canonical(g.First().Text),Lines=g.Count(),Layers=g.Select(x=>x.Layer).Distinct(StringComparer.OrdinalIgnoreCase).Count(),Types=g.Select(x=>x.Type).Distinct(StringComparer.OrdinalIgnoreCase).Count(),Locked=g.Count(x=>x.Locked),Linked=g.Count(x=>x.Linked),Colors=String.Join("; ",g.Select(x=>x.Color).Distinct().Take(5)),Scales=g.Min(x=>x.Scale)==g.Max(x=>x.Scale)?Profile.N(g.First().Scale):Profile.N(g.Min(x=>x.Scale))+" … "+Profile.N(g.Max(x=>x.Scale))}).OrderBy(g=>g.Label,StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
    public static class LabelRules
    {
        public static string Canonical(string text)
        {
            text=(text??"").Trim();var start=Regex.Match(text,@"^(?:DN|D|%%C|[φΦøØ⌀∅])\s*([0-9]+)\s*[._\s-]+\s*(.+)$",RegexOptions.IgnoreCase);if(start.Success)return "D"+start.Groups[1].Value+"."+start.Groups[2].Value;var old=Regex.Match(text,@"^(.+?)\s*[._\s-]+\s*(?:DN|D|%%C|[φΦøØ⌀∅])\s*([0-9]+)$",RegexOptions.IgnoreCase);if(old.Success)return "D"+old.Groups[2].Value+"."+old.Groups[1].Value;return text;
        }
        public static string System(string text){text=Canonical(text);return Regex.Replace(text,@"^D[0-9]+\.","",RegexOptions.IgnoreCase);}
        public static string Key(string text,bool system){return (system?"S|":"L|")+(system?System(text):Canonical(text)).ToUpperInvariant();}
        static string SearchKey(string value){return new string((value??"").Replace('đ','d').Replace('Đ','D').Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark&&Char.IsLetterOrDigit(c)).Select(Char.ToUpperInvariant).ToArray());}
        public static bool Matches(string query,string label,IEnumerable<string> texts)
        {
            var hay=SearchKey(label+" "+String.Join(" ",texts.Select(t=>Canonical(t)+" "+Regex.Replace(Canonical(t),@"^D([0-9]+)\.(.+)$","$2$1",RegexOptions.IgnoreCase))));return Regex.Split(query??"",@"\s+").Select(SearchKey).Where(t=>t.Length>0).All(hay.Contains);
        }
        public static double Scale(double before,LabelEdit edit){double after=edit.Multiply?before*edit.Scale:edit.Scale;if(!Profile.Finite(after)||after<=0||after>1e6)throw new ArgumentException("Scale sau tính toán phải >0 và ≤1000000.");return after;}
        public static List<LabelMember> Select(LabelCatalog catalog,LabelScope scope){var keys=new HashSet<string>(scope.Keys??new string[0],StringComparer.OrdinalIgnoreCase);return catalog.Members.Where(x=>LabelBrowse.LayerMatches(x.Layer,scope.LayerPrefix,scope.LayerSuffix)&&(scope.All||keys.Contains(Key(x.Text,scope.SystemMode)))).ToList();}
    }
    public sealed class LabelScope
    {
        public bool All,SystemMode;public string[] Keys;public string Fingerprint,LayerPrefix="",LayerSuffix="";
    }
    public sealed class LabelEdit
    {
        public bool ForceR,Recenter=true,ChangeColor,ByLayer,ChangeScale,Multiply,ChangeWidth;public int Aci=7;public double Scale=1,Width;public BatchEdit Pattern=new BatchEdit();
        public bool ChangesType{get{return ForceR||Pattern.ChangesPattern;}}
        public void Validate()
        {
            if(!(ForceR||ChangeColor||ChangeScale||ChangeWidth||Pattern.ChangesPattern))throw new ArgumentException("Chọn thao tác muốn áp cho cả cụm.");if(ChangeColor&&!ByLayer&&(Aci<1||Aci>255))throw new ArgumentException("Màu ACI phải từ 1 đến 255.");
            if(ChangeScale&&(!Profile.Finite(Scale)||Scale<=0||Scale>1e6))throw new ArgumentException("Scale/hệ số nhân phải >0 và ≤1000000.");if(ChangeWidth&&(!Profile.Finite(Width)||Width<0||Width>1e9))throw new ArgumentException("Width phải từ 0 đến 1000000000.");
            if(Pattern.Renames||Pattern.ExactType||Pattern.ChangeLinetype||Pattern.ChangeColor||Pattern.ColorByLayer)throw new ArgumentException("Sửa theo nhãn chỉ thay nét/chữ, không đổi thuộc tính Layer.");if(Pattern.ChangesPattern)Pattern.Validate(1);
        }
        public string Signature{get{return ForceR+"|"+Recenter+"|"+ChangeColor+"|"+ByLayer+"|"+Aci+"|"+ChangeScale+"|"+Multiply+"|"+Profile.N(Scale)+"|"+ChangeWidth+"|"+Profile.N(Width)+"|"+Pattern.Signature;}}
    }
}
