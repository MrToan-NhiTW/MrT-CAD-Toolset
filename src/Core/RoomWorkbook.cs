using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace MedGas2024.Core
{
    public sealed class RoomCell{public string Text="",Error="";public double? Number;}
    public sealed class RoomSheet
    {
        public string Name;public readonly SortedDictionary<int,Dictionary<int,RoomCell>> Rows=new SortedDictionary<int,Dictionary<int,RoomCell>>();
        public int Columns;
        public RoomCell Cell(int row,int col){Dictionary<int,RoomCell> d;RoomCell c;return Rows.TryGetValue(row,out d)&&d.TryGetValue(col,out c)?c:new RoomCell();}
    }
    public sealed class RoomWorkbook
    {
        static readonly XNamespace X="http://schemas.openxmlformats.org/spreadsheetml/2006/main",R="http://schemas.openxmlformats.org/officeDocument/2006/relationships",P="http://schemas.openxmlformats.org/package/2006/relationships";
        public readonly List<RoomSheet> Sheets=new List<RoomSheet>();
        static XDocument Xml(ZipArchive zip,string path,bool required=true)
        {
            var e=zip.GetEntry(path);if(e==null){if(required)throw new InvalidDataException("Thiếu thành phần Excel: "+path);return null;}if(e.Length>30000000)throw new InvalidDataException("Dữ liệu Excel quá lớn: "+path);using(var stream=e.Open())return XDocument.Load(stream,LoadOptions.None);
        }
        static int Col(string address)
        {
            int col=0,i=0;while(i<address.Length&&Char.IsLetter(address[i])){char c=Char.ToUpperInvariant(address[i++]);if(c<'A'||c>'Z')return 0;col=col*26+c-'A'+1;if(col>256)return 0;}return col;
        }
        public static string ColumnName(int n){string s="";while(n>0){n--;s=(char)('A'+n%26)+s;n/=26;}return s;}
        static string Text(XElement parent){return parent==null?"":String.Concat(parent.Descendants(X+"t").Select(t=>(string)t));}
        public static RoomWorkbook Read(string path)
        {
            if(!String.Equals(Path.GetExtension(path),".xlsx",StringComparison.OrdinalIgnoreCase)&&!String.Equals(Path.GetExtension(path),".xlsm",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Hãy chọn Excel .xlsx hoặc .xlsm; .xls cần lưu thành .xlsx trước.");
            var result=new RoomWorkbook();using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read))
            {
                var book=Xml(zip,"xl/workbook.xml");var rels=Xml(zip,"xl/_rels/workbook.xml.rels");var rel=rels.Root.Elements(P+"Relationship").Where(e=>(string)e.Attribute("TargetMode")!="External").ToDictionary(e=>(string)e.Attribute("Id"),e=>(string)e.Attribute("Target"));
                var shared=Xml(zip,"xl/sharedStrings.xml",false);var strings=shared==null?new string[0]:shared.Root.Elements(X+"si").Select(Text).ToArray();
                var formats=NumberFormats(Xml(zip,"xl/styles.xml",false));
                foreach(var sh in book.Descendants(X+"sheet")){
                    if(result.Sheets.Count>=100)throw new InvalidDataException("Tệp có quá 100 sheet.");var rid=(string)sh.Attribute(R+"id");string target;if(rid==null||!rel.TryGetValue(rid,out target))continue;
                    target=target.Replace('\\','/');if(target.StartsWith("/"))target=target.TrimStart('/');else target="xl/"+target;
                    if(target.Contains("../")||!target.StartsWith("xl/",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Đường dẫn sheet không hợp lệ.");
                    var doc=Xml(zip,target);var sheet=new RoomSheet{Name=(string)sh.Attribute("name")??"Sheet"};int rowCount=0;
                    foreach(var row in doc.Descendants(X+"sheetData").Elements(X+"row")){
                        if(++rowCount>100000)throw new InvalidDataException("Sheet quá 100.000 dòng; hãy tách nhỏ trước.");int rowNo;if(!Int32.TryParse((string)row.Attribute("r"),out rowNo)||rowNo<=0)continue;var cells=new Dictionary<int,RoomCell>();
                        foreach(var cell in row.Elements(X+"c")){int col=Col((string)cell.Attribute("r")??"");if(col==0)continue;var type=(string)cell.Attribute("t")??"n";var raw=(string)cell.Element(X+"v");var formula=cell.Element(X+"f");var item=new RoomCell();
                            if(type=="s"){int idx;if(!Int32.TryParse(raw,NumberStyles.Integer,CultureInfo.InvariantCulture,out idx)||idx<0||idx>=strings.Length)item.Error="Chỉ số chuỗi Excel không hợp lệ.";else item.Text=strings[idx];}
                            else if(type=="inlineStr")item.Text=Text(cell.Element(X+"is"));
                            else if(type=="str")item.Text=raw??"";
                            else if(type=="e")item.Error="Ô Excel đang có lỗi công thức.";
                            else if(type=="b")item.Error="Ô kiểu đúng/sai, không phải mã hoặc tên phòng.";
                            else if(!String.IsNullOrEmpty(raw)){double number;if(Double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out number))item.Number=number;int style;Int32.TryParse((string)cell.Attribute("s"),NumberStyles.Integer,CultureInfo.InvariantCulture,out style);
                                string text,error;if(NumericCode(raw,style>=0&&style<formats.Count?formats[style]:null,out text,out error))item.Text=text;else item.Error=error;}
                            if(formula!=null&&raw==null)item.Error="Công thức chưa có kết quả lưu; mở Excel, tính lại và lưu.";
                            cells[col]=item;sheet.Columns=Math.Max(sheet.Columns,col);
                        }
                        if(cells.Count>0)sheet.Rows[rowNo]=cells;
                    }result.Sheets.Add(sheet);
                }
            }
            if(result.Sheets.Count==0)throw new InvalidDataException("Không đọc được sheet nào trong tệp Excel.");return result;
        }
        // Number format code for each cell style (cellXfs index); null = General.
        static List<string> NumberFormats(XDocument styles)
        {
            var result=new List<string>();if(styles==null)return result;
            var custom=new Dictionary<int,string>();
            foreach(var e in styles.Root.Elements(X+"numFmts").Elements(X+"numFmt")){int id;if(Int32.TryParse((string)e.Attribute("numFmtId"),NumberStyles.Integer,CultureInfo.InvariantCulture,out id))custom[id]=(string)e.Attribute("formatCode")??"";}
            foreach(var xf in styles.Root.Elements(X+"cellXfs").Elements(X+"xf"))
            {
                int id;Int32.TryParse((string)xf.Attribute("numFmtId"),NumberStyles.Integer,CultureInfo.InvariantCulture,out id);string code;
                result.Add(custom.TryGetValue(id,out code)?code:BuiltInFormat(id));
            }
            return result;
        }
        static string BuiltInFormat(int id)
        {
            switch(id)
            {
                case 0:return null;case 1:return "0";case 49:return "@";
                case 2:return "0.00";case 3:return "#,##0";case 4:return "#,##0.00";case 9:return "0%";case 10:return "0.00%";
                case 11:case 48:return "0.00E+00";case 12:return "# ?/?";case 13:return "# ??/??";
            }
            if((id>=14&&id<=22)||(id>=27&&id<=36)||(id>=45&&id<=47)||(id>=50&&id<=58))return "d/m/yyyy";
            return "#,##0.00";
        }
        // Excel stores a number without its display text. Rebuild the code the user
        // sees only when it is certain: a whole number shown with General, 0, @ or a
        // zero-padding format such as 000 or "P"000. Text cells are never touched.
        public static bool NumericCode(string raw,string format,out string text,out string error)
        {
            text="";error=null;double value;
            if(!Double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||Double.IsNaN(value)||Double.IsInfinity(value))
            {error="Ô số không đọc được.";return false;}
            string section=FirstSection(format);
            if(section!=null&&IsDateFormat(section)){error="Ô là ngày/giờ trong Excel, không phải mã phòng; hãy nhập lại dạng văn bản.";return false;}
            if(value<0||value>=1e15||value!=Math.Floor(value)){error="Ô là số thập phân, số âm hoặc quá lớn; hãy lưu mã phòng dạng văn bản.";return false;}
            string digits=value.ToString("0",CultureInfo.InvariantCulture);
            if(section==null||section.Trim().Length==0||String.Equals(section,"General",StringComparison.OrdinalIgnoreCase)||section=="0"||section=="@"){text=digits;return true;}
            string prefix,suffix;int zeros;
            if(!PaddingFormat(section,out prefix,out zeros,out suffix)){error="Ô số có định dạng «"+format+"» không chuyển chắc chắn thành mã; hãy lưu dạng văn bản.";return false;}
            text=prefix+digits.PadLeft(zeros,'0')+suffix;return true;
        }
        static string FirstSection(string format)
        {
            if(format==null)return null;bool quoted=false;
            for(int i=0;i<format.Length;i++){char c=format[i];if(c=='"')quoted=!quoted;else if(c=='\\'&&!quoted)i++;else if(c==';'&&!quoted)return format.Substring(0,i);}
            return format;
        }
        static bool IsDateFormat(string section)
        {
            bool quoted=false;
            for(int i=0;i<section.Length;i++)
            {
                char c=section[i];
                if(c=='"'){quoted=!quoted;continue;}if(quoted)continue;
                if(c=='\\'){i++;continue;}
                if(c=='['){int end=section.IndexOf(']',i);if(end<0)return false;string tag=section.Substring(i+1,end-i-1).ToLowerInvariant();if(tag.Length>0&&"hms".IndexOf(tag[0])>=0)return true;i=end;continue;}
                if("dmyhsDMYHS".IndexOf(c)>=0&&!section.Equals("General",StringComparison.OrdinalIgnoreCase))return true;
            }
            return false;
        }
        static bool PaddingFormat(string section,out string prefix,out int zeros,out string suffix)
        {
            prefix="";suffix="";zeros=0;var before=new System.Text.StringBuilder();var after=new System.Text.StringBuilder();int state=0;
            for(int i=0;i<section.Length;i++)
            {
                char c=section[i];string literal=null;
                if(c=='"'){int end=section.IndexOf('"',i+1);if(end<0)return false;literal=section.Substring(i+1,end-i-1);i=end;}
                else if(c=='\\'){if(i+1>=section.Length)return false;literal=section[++i].ToString();}
                else if(c=='['){int end=section.IndexOf(']',i);if(end<0)return false;i=end;continue;}
                else if(c=='0'||c=='#'){if(state==2)return false;state=1;if(c=='0')zeros++;continue;}
                else if("-/() ".IndexOf(c)>=0)literal=c.ToString();
                else return false;
                if(state==0)before.Append(literal);else{state=2;after.Append(literal);}
            }
            if(state==0)return false;prefix=before.ToString();suffix=after.ToString();return true;
        }
        static string HeaderKey(string text)
        {
            string folded=RoomSearch.Fold(System.Text.RegularExpressions.Regex.Replace(text??"",@"\([^)]*\)"," "));
            return String.Join(" ",new string(folded.Select(c=>Char.IsLetterOrDigit(c)?c:' ').ToArray()).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries));
        }
        static readonly string[] OldHeaders={"ten cu","ten phong cu","phong cu","ma phong cu","ma cu","ten phong hien trang","ten hien trang"};
        static readonly string[] NewHeaders={"ten moi","ten phong moi","phong moi","ma phong moi","ma moi","ten phong","ten phong de xuat","ten de xuat"};
        public static bool IsOldHeader(string text){return OldHeaders.Contains(HeaderKey(text));}
        public static bool IsNewHeader(string text){return NewHeaders.Contains(HeaderKey(text));}
        static bool Header(string text,bool old){return old?IsOldHeader(text):IsNewHeader(text);}
        public static int HeaderRow(RoomSheet sheet)
        {
            foreach(var row in sheet.Rows.Take(50))
                if(row.Value.Values.Any(c=>Header(c.Text,true))&&row.Value.Values.Any(c=>Header(c.Text,false)))return row.Key;
            return 1;
        }
        public static RoomCatalog Catalog(RoomSheet sheet,int headerRow,int oldCol,int newCol,int idCol)
        {
            if(oldCol<1||newCol<1)throw new ArgumentException("Chọn cột tên cũ và tên mới.");if(oldCol==newCol)throw new ArgumentException("Tên cũ và tên mới phải chọn hai cột khác nhau.");var rows=new List<RoomEntry>();var problems=new List<string>();var invalidNames=new List<string>();
            foreach(var pair in sheet.Rows.Where(x=>x.Key>headerRow)){
                var a=sheet.Cell(pair.Key,oldCol);var b=sheet.Cell(pair.Key,newCol);var id=idCol>0?sheet.Cell(pair.Key,idCol):a;
                if(Header(a.Text,true)&&Header(b.Text,false)){
                    problems.Add("Dòng "+pair.Key+" là tiêu đề cột, đã bỏ qua.");continue;
                }
                string error=String.Join(" ",new[]{a.Error,b.Error,id.Error}.Where(s=>!String.IsNullOrEmpty(s)).Distinct());
                if(error.Length>0){invalidNames.Add(RoomCatalog.MatchName(a.Text));problems.Add("Dòng "+pair.Key+": "+error);continue;}
                if(String.IsNullOrWhiteSpace(a.Text)&&String.IsNullOrWhiteSpace(b.Text)&&String.IsNullOrWhiteSpace(id.Text))continue;
                rows.Add(new RoomEntry{Id=id.Text,OldName=a.Text,NewName=b.Text,ExcelRow=pair.Key});
            }
            var catalog=RoomCatalog.Build(rows);catalog.BlockedNames.UnionWith(invalidNames);catalog.ErrorNames.UnionWith(invalidNames.Where(n=>n.Length>0));catalog.Problems.AddRange(problems);return catalog;
        }
    }
}
