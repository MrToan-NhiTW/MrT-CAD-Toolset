using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MedGas2024.Core
{
    [DataContract]
    public sealed class GasRow
    {
        [DataMember] public bool Enabled { get; set; }
        [DataMember] public string SystemCode { get; set; }
        [DataMember] public int Diameter { get; set; }
        [DataMember] public string LabelPrefix { get; set; }
        [DataMember] public double Dash { get; set; }
        [DataMember] public double GapBefore { get; set; }
        [DataMember] public double GapAfter { get; set; }
        [DataMember] public double TextScale { get; set; }
        [DataMember] public double OffsetX { get; set; }
        [DataMember] public double OffsetY { get; set; }
        [DataMember] public string RotationMode { get; set; }
        [DataMember] public double RotationDegrees { get; set; }
        [DataMember] public int ColorIndex { get; set; }
        [DataMember] public string SourceFile { get; set; }
        [DataMember] public string SourceName { get; set; }
        [DataMember] public string SourceStyle { get; set; }
        [DataMember] public string CustomText { get; set; }
        [DataMember] public string Description { get; set; }
        [DataMember] public string LayoutReference { get; set; }
        // Null inherits profile defaults; an empty string means explicitly no prefix/suffix.
        [DataMember(EmitDefaultValue=false)] public string PrefixOverride { get; set; }
        [DataMember(EmitDefaultValue=false)] public string SuffixOverride { get; set; }
        [DataMember(EmitDefaultValue=false)] public string LinetypeNameOverride { get; set; }
        public static string FormatText(string label, int diameter) { return "D" + diameter.ToString(CultureInfo.InvariantCulture) + "." + label; }
        public string DefaultText { get { return FormatText(LabelPrefix, Diameter); } }
        public string Text { get { return String.IsNullOrEmpty(CustomText)?DefaultText:CustomText; } set { CustomText=String.IsNullOrWhiteSpace(value) || value.Trim()==DefaultText ? null : value.Trim(); } }
        public string DescriptionText { get { return String.IsNullOrWhiteSpace(Description)?SystemCode+" - D"+Diameter:Description; } set { Description=value; } }
        public string Stem { get { return "MRG_" + SystemCode + "_D" + Diameter; } }
        public string LayerName { get; set; }
        public string LinetypeName { get; set; }
        public double Period { get { return Dash + GapBefore + GapAfter; } }
        public GasRow Copy() { return (GasRow)MemberwiseClone(); }
    }

    [DataContract]
    public sealed class Profile
    {
        [DataMember] public int SchemaVersion { get; set; }
        [DataMember] public string UnitTag { get; set; }
        [DataMember] public double GeometryFactor { get; set; }
        [DataMember] public string FontFile { get; set; }
        [DataMember] public bool FontBold { get; set; }
        [DataMember] public double PaddingRatio { get; set; }
        [DataMember] public string LayerPrefix { get; set; }
        [DataMember] public bool AutoFitOnSync { get; set; }
        [DataMember] public List<GasRow> Rows { get; set; }
        public Profile()
        {
            SchemaVersion = 5; UnitTag = "RAW"; GeometryFactor = 1;
            FontFile = "bahnschrift.ttf"; FontBold=true; PaddingRatio = 0.35; LayerPrefix="Mr.T"; AutoFitOnSync=false; Rows = new List<GasRow>();
        }
        [OnDeserializing]
        private void BeforeRead(StreamingContext context) { LayerPrefix="Mr.T"; PaddingRatio=.35; }
        public string PrefixFor(GasRow r){return r.PrefixOverride??LayerPrefix??"";}
        public string SuffixFor(GasRow r){return r.SuffixOverride??UnitTag??"";}
        public static string ComposeName(string prefix,string code,int diameter,string suffix){return String.Join("_",new[]{prefix,code,"D"+diameter.ToString(CultureInfo.InvariantCulture),suffix}.Where(s=>!String.IsNullOrEmpty(s)));}
        public string LegacyNameFor(GasRow r) { return ComposeName("MRG",r.SystemCode,r.Diameter,SuffixFor(r)); }
        public string NameFor(GasRow r) { return r.LinetypeNameOverride??ComposeName(PrefixFor(r),LinetypeCode(r.SystemCode),r.Diameter,SuffixFor(r)); }
        public string LayerNameFor(GasRow r) { return ComposeName(PrefixFor(r),r.SystemCode,r.Diameter,SuffixFor(r)); }
        // LIN names remain readable; callers must reject transliteration collisions.
        public static string LinetypeCode(string code)
        {
            code=(code??"").Normalize(NormalizationForm.FormC);
            if(Regex.IsMatch(code,@"^[A-Z][A-Z0-9_]{0,23}$"))return code;
            string plain=code.Replace('đ','d').Replace('Đ','D').Normalize(NormalizationForm.FormD);
            plain=new string(plain.Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant();
            plain=Regex.Replace(plain,@"[^A-Z0-9_]+","_").Trim('_');
            if(plain.Length==0)plain="HE";if(!Char.IsLetter(plain[0]))plain="HE_"+plain;
            return plain;
        }
        public void RefreshNames() { foreach(var r in Rows) { r.LayerName=LayerNameFor(r); r.LinetypeName=NameFor(r); } }
        // Only scratch/sample/new-library operations use this transient name. Never serialized.
        public string SessionStyleName {get;set;}
        public string StyleName { get { return SessionStyleName ?? ("MrT_TX_" + Hash(FontFile.ToLowerInvariant()+"|bold="+FontBold).Substring(0, 12).ToUpperInvariant()); } }
        public List<GasRow> Selected { get { return Rows.Where(x => x.Enabled).ToList(); } }
        public static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
        public static string N(double n) { return n.ToString("0.###############", CultureInfo.InvariantCulture); }
        public List<string> Validate(bool requireSelection=true)
        {
            var e = new List<string>();
            if (SchemaVersion < 1 || SchemaVersion > 5) e.Add("Phiên bản cấu hình không được hỗ trợ.");
            if (!String.IsNullOrEmpty(LayerPrefix)&&!Regex.IsMatch(LayerPrefix, @"^[A-Za-z][A-Za-z0-9_.-]{0,31}$")) e.Add("Tiền tố Layer để trống hoặc dùng chữ không dấu, số, dấu chấm, _ hoặc -; bắt đầu bằng chữ.");
            if (!String.IsNullOrEmpty(UnitTag)&&!Regex.IsMatch(UnitTag, @"^[A-Z][A-Z0-9_]{0,15}$")) e.Add("Hậu tố để trống hoặc dùng chữ A-Z, số hoặc dấu _.");
            if (!Finite(GeometryFactor) || GeometryFactor < 0.000001 || GeometryFactor > 1000000) e.Add("Hệ số phải trong khoảng 0.000001–1000000.");
            if (!Finite(PaddingRatio) || PaddingRatio < 0.05 || PaddingRatio > 5) e.Add("Tỷ lệ hở chữ / cao chữ phải từ 0.05 đến 5.");
            if (String.IsNullOrWhiteSpace(FontFile) || FontFile.IndexOfAny(new [] {'"','\r','\n',',','[',']'}) >= 0 ||
                !(FontFile.EndsWith(".shx", StringComparison.OrdinalIgnoreCase) || FontFile.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))) e.Add("Font phải là file SHX/TTF hợp lệ.");
            if(FontBold && (FontFile??"").EndsWith(".shx",StringComparison.OrdinalIgnoreCase)) e.Add("Chữ đậm cần font TTF. Chọn Bahnschrift/Arial hoặc bỏ chọn Chữ đậm khi dùng SHX.");
            if (Rows == null || Rows.Any(r => r == null)) { e.Add("Danh sách dòng không hợp lệ."); return e; }
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in Selected)
            {
                string at = (r.SystemCode ?? "?") + " D" + r.Diameter + ": ";
                if(!String.IsNullOrEmpty(PrefixFor(r))&&!Regex.IsMatch(PrefixFor(r),@"^[A-Za-z][A-Za-z0-9_.-]{0,31}$"))e.Add(at+"Tiền tố không hợp lệ.");
                if(!String.IsNullOrEmpty(SuffixFor(r))&&!Regex.IsMatch(SuffixFor(r),@"^[A-Z][A-Z0-9_]{0,15}$"))e.Add(at+"Hậu tố không hợp lệ.");
                if (String.IsNullOrWhiteSpace(r.SystemCode) || r.SystemCode.Length>40 || !Regex.IsMatch(r.SystemCode,@"^[\p{L}\p{M}\p{N} _.-]+$") || r.SystemCode!=r.SystemCode.Trim()) e.Add(at + "Mã hệ tối đa 40 ký tự: chữ, số, dấu cách, _, . hoặc -.");
                if (r.Diameter < 1 || r.Diameter > 10000) e.Add(at + "Đường kính phải từ 1 đến 10000.");
                if (String.IsNullOrWhiteSpace(r.LabelPrefix) || r.LabelPrefix.Length>40 || !Regex.IsMatch(r.LabelPrefix,@"^[\p{L}\p{M}\p{N} ._+/-]+$")) e.Add(at + "Nhãn tối đa 40 ký tự: chữ, số, dấu cách và . _ + / -.");
                if (String.IsNullOrWhiteSpace(r.Text) || r.Text.Length>80 || !Regex.IsMatch(r.Text,@"^[\p{L}\p{M}\p{N} ._+/-]+$")) e.Add(at+"Chữ trên nét tối đa 80 ký tự: chữ, số, dấu cách và . _ + / -.");
                if((r.Description??"").Length>200 || (r.Description??"").Any(Char.IsControl)) e.Add(at+"Mô tả tối đa 200 ký tự, một dòng.");
                if (r.ColorIndex < 1 || r.ColorIndex > 255) e.Add(at + "Màu ACI phải từ 1 đến 255.");
                if (!Finite(r.Dash) || r.Dash <= 0 || !Finite(r.GapBefore) || r.GapBefore <= 0 || !Finite(r.GapAfter) || r.GapAfter <= 0 || !Finite(r.TextScale) || r.TextScale <= 0) e.Add(at + "Đoạn nét, khoảng trống và S phải dương, hữu hạn.");
                if (!Finite(r.OffsetX) || !Finite(r.OffsetY) || !Finite(r.RotationDegrees) || !Finite(r.Period)) e.Add(at + "Offset/góc/chu kỳ không hợp lệ.");
                foreach(var v in new [] {r.Dash,r.GapBefore,r.GapAfter,r.TextScale,r.OffsetX,r.OffsetY,r.Period})
                    if (!Finite(v*GeometryFactor) || Math.Abs(v*GeometryFactor)>1000000000 || (v!=0 && N(v*GeometryFactor)=="0")) { e.Add(at + "Kích thước sau nhân hệ số quá nhỏ hoặc quá lớn."); break; }
                if (r.RotationMode != "R" && r.RotationMode != "U" && r.RotationMode != "A") e.Add(at + "Góc dùng R, U hoặc A.");
                if (!used.Add(NameFor(r))) e.Add(at + "Trùng tên Linetype '"+NameFor(r)+"' sau chuyển không dấu; đổi mã hệ hoặc tiền tố/hậu tố.");
                if (!usedLayers.Add(LayerNameFor(r))) e.Add(at + "Trùng tên layer.");
            }
            if (requireSelection && Selected.Count == 0) e.Add("Chọn ít nhất một dòng.");
            return e;
        }
        public static bool Finite(double n) { return !Double.IsNaN(n) && !Double.IsInfinity(n); }
        public void RequireValid(bool requireSelection=true)
        {
            var errors = Validate(requireSelection);
            if (errors.Count != 0) throw new InvalidDataException(String.Join(Environment.NewLine, errors));
        }
        public string Pattern(GasRow r)
        {
            double k = GeometryFactor;
            return "A," + N(r.Dash*k) + ",-" + N(r.GapBefore*k) + ",[\"" + r.Text + "\"," + StyleName + ",S=" + N(r.TextScale*k) + "," + r.RotationMode + "=" + N(r.RotationDegrees) + ",X=" + N(r.OffsetX*k) + ",Y=" + N(r.OffsetY*k) + "],-" + N(r.GapAfter*k);
        }
        public string ExportLin()
        {
            RequireValid();
            if(Selected.Any(r=>NameFor(r).Any(c=>c>127)||r.Text.Any(c=>c>127)))throw new InvalidDataException("Xuất LIN ASCII chưa hỗ trợ nhãn tiếng Việt có dấu. Tạo trực tiếp vào DWG để giữ nguyên chữ; không tự đổi chữ thành dấu hỏi.");
            var s = new StringBuilder("; Mr.T: create the named text style with height 0, width 1 before loading.\r\n");
            s.Append("; Font: ").Append(FontFile).Append("; unit tag: ").Append(UnitTag).Append("; factor: ").Append(N(GeometryFactor)).Append("\r\n");
            foreach (var r in Selected) s.Append("*").Append(NameFor(r)).Append(",").Append(AsciiDescription(r.DescriptionText)).Append("\r\n").Append(Pattern(r)).Append("\r\n");
            return s.ToString();
        }
        private static string AsciiDescription(string value)
        {
            var chars=value.Replace('đ','d').Replace('Đ','D').Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark && c>=32 && c<=126).ToArray();
            return new string(chars);
        }
        public string Signature(GasRow r) { return Hash(NameFor(r) + "|" + Pattern(r) + "|" + FontFile.ToLowerInvariant()+"|"+FontBold+"|"+r.DescriptionText); }
        public void Save(string path)
        {
            RequireValid(false);
            using (var ms = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(Profile)).WriteObject(ms, this);
                var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(tmp, ms.ToArray());
                try { if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path); }
                finally { if (File.Exists(tmp)) File.Delete(tmp); }
            }
        }
        public static Profile Load(string path)
        {
            Profile p;
            using (var f = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(path,Encoding.UTF8))))
                p = (Profile)new DataContractJsonSerializer(typeof(Profile)).ReadObject(f);
            if (p == null) throw new InvalidDataException("Cấu hình rỗng.");
            if(p.SchemaVersion>=1 && p.SchemaVersion<4)
            {
                // A literal copy of the former automatic label is migrated; distinct overrides are retained.
                if(p.Rows!=null) foreach(var r in p.Rows.Where(x=>x!=null))
                    if(r.CustomText==r.LabelPrefix+".D"+r.Diameter.ToString(CultureInfo.InvariantCulture)) r.CustomText=null;
                p.SchemaVersion=4;
            }
            if(p.SchemaVersion==4)
            {
                // 1.9.5.4 used hashed names for non-ASCII/noncanonical codes. Preserve
                // those names when opening an existing profile; new rows use plain names.
                if(p.Rows!=null)foreach(var r in p.Rows.Where(x=>x!=null&&x.LinetypeNameOverride==null))
                {
                    string code=(r.SystemCode??"").Normalize(NormalizationForm.FormC);
                    if(!Regex.IsMatch(code,@"^[A-Z][A-Z0-9_]{0,23}$"))
                    {
                        string legacy=LinetypeCode(code);if(legacy.Length>28)legacy=legacy.Substring(0,28);
                        legacy+="_"+Hash(code.ToUpperInvariant()).Substring(0,10).ToUpperInvariant();
                        r.LinetypeNameOverride=ComposeName(p.PrefixFor(r),legacy,r.Diameter,p.SuffixFor(r));
                    }
                }
                p.SchemaVersion=5;
            }
            p.RequireValid(false); p.RefreshNames(); return p;
        }
        public static List<GasRow> Expand(GasRow template, string diameters)
        {
            var output = new List<GasRow>(); var seen = new HashSet<int>();
            foreach (var part in Regex.Split(diameters.Trim(), @"[,;\s]+"))
            {
                int d;
                if (!Int32.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out d) || d < 1 || d > 10000) throw new InvalidDataException("Đường kính không hợp lệ: " + part);
                if (seen.Add(d)) { var row = template.Copy(); row.Diameter = d; row.CustomText=null; row.Description=null; row.SourceFile = String.IsNullOrWhiteSpace(template.SourceName)?null:"Generated from " + template.SourceName; row.SourceName = ""; output.Add(row); }
            }
            return output;
        }
    }
}
