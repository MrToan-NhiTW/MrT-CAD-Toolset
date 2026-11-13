using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MedGas2024.Core
{
    public static class LinReader
    {
        // This importer intentionally accepts only the one-text, three-dash LIN grammar.
        // Other valid LIN grammars are rejected explicitly rather than silently approximated.
        private const string Num = @"[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?";
        public static List<GasRow> Read(string text, string source)
        {
            var lines = text.Split(new [] {'\r','\n'}, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith(";")).ToArray();
            var rows = new List<GasRow>();
            if (lines.Length == 0 || lines.Length % 2 != 0) throw new InvalidDataException(source + ": LIN phải có cặp tên/mẫu.");
            for (int i = 0; i < lines.Length; i += 2)
            {
                var h = Regex.Match(lines[i], @"^\*([^,]+),(.*)$");
                var m = Regex.Match(lines[i+1], @"^A\s*,\s*(?<dash>"+Num+@")\s*,\s*(?<pre>"+Num+@")\s*,\s*\[\s*""(?<label>[^""\r\n]+)""\s*,\s*(?<style>[^,\]]+)\s*(?<opts>(?:,[^\]]+)?)\]\s*,\s*(?<post>"+Num+@")\s*$", RegexOptions.IgnoreCase);
                if (!h.Success || !m.Success) throw new InvalidDataException(source + ": chỉ hỗ trợ A,nét,khoảng,[\"chữ\",style,S=,R/U/A=,X=,Y=],khoảng. Không hỗ trợ shape SHX hoặc mẫu nhiều chữ.");
                var label = Regex.Match(m.Groups["label"].Value, @"^D(?<d>\d+)\.(?<p>.+)$", RegexOptions.IgnoreCase);
                if (!label.Success) label = Regex.Match(m.Groups["label"].Value, @"^(?<p>.+)\.D(?<d>\d+)$", RegexOptions.IgnoreCase);
                if (!label.Success) throw new InvalidDataException(source + ": nhãn dùng D<đường kính>.<nhãn hệ>, ví dụ D110.HDPE; cũng nhận mẫu nhãn.D110.");
                string prefix = label.Groups["p"].Value;
                var row = new GasRow { Enabled=true, LabelPrefix=prefix, Diameter=Int32.Parse(label.Groups["d"].Value, CultureInfo.InvariantCulture),
                    SystemCode=Code(prefix),
                    Dash=Parse(m.Groups["dash"].Value), GapBefore=-Parse(m.Groups["pre"].Value), GapAfter=-Parse(m.Groups["post"].Value),
                    TextScale=1, OffsetX=0, OffsetY=0, RotationMode="R", RotationDegrees=0, ColorIndex=7,
                    SourceFile=source, SourceName=h.Groups[1].Value.Trim(), Description=h.Groups[2].Value.Trim(), SourceStyle=m.Groups["style"].Value.Trim() };
                var seen = new HashSet<string>(); bool rotation = false;
                foreach (var opt in m.Groups["opts"].Value.Split(new [] {','}, StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = Regex.Match(opt.Trim(), @"^(?<k>[SRUAXY])\s*=\s*(?<v>"+Num+@")(?<unit>[drg]?)$", RegexOptions.IgnoreCase);
                    if (!kv.Success) throw new InvalidDataException(source + ": tham số không hỗ trợ: " + opt);
                    string key=kv.Groups["k"].Value.ToUpperInvariant();
                    if (!seen.Add(key)) throw new InvalidDataException(source + ": lặp tham số " + key);
                    double v=Parse(kv.Groups["v"].Value); string unit=kv.Groups["unit"].Value.ToLowerInvariant();
                    if (key=="R" || key=="U" || key=="A")
                    {
                        if (rotation) throw new InvalidDataException(source + ": có nhiều kiểu góc.");
                        rotation=true; row.RotationMode=key;
                        row.RotationDegrees=unit=="r" ? v*180/Math.PI : unit=="g" ? v*0.9 : v;
                    }
                    else
                    {
                        if (unit!="") throw new InvalidDataException(source + ": hậu tố góc chỉ dùng cho R/U/A.");
                        switch(key) { case "S": row.TextScale=v; break; case "X": row.OffsetX=v; break; case "Y": row.OffsetY=v; break; }
                    }
                }
                rows.Add(row);
            }
            var p = new Profile { Rows=rows }; p.RequireValid(); return rows;
        }
        private static string Code(string prefix){string code=Regex.Replace(prefix.ToUpperInvariant(),@"[^A-Z0-9_]","_");return code.Length>0&&Char.IsLetter(code[0])?code:"SYS_"+code;}
        private static double Parse(string s) { return Double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture); }
        public static Profile ReadFolder(string folder)
        {
            var p=new Profile();
            foreach(var f in Directory.GetFiles(folder, "*.lin").OrderBy(x=>x, StringComparer.OrdinalIgnoreCase))
            {
                var bytes=File.ReadAllBytes(f);
                // Existing labels are ASCII; decode UTF-8 BOM or local ANSI for import metadata.
                string content=bytes.Length>=3 && bytes[0]==239 && bytes[1]==187 && bytes[2]==191 ? Encoding.UTF8.GetString(bytes,3,bytes.Length-3) : Encoding.Default.GetString(bytes);
                p.Rows.AddRange(Read(content,Path.GetFileName(f)));
            }
            p.RequireValid(); return p;
        }
    }
}
