using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace MedGas2024.Core
{
    public sealed class RoomCell{public string Text="",Error="";}
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
                            else if(!String.IsNullOrEmpty(raw))item.Error="Ô là số/ngày; hãy lưu mã và tên phòng dạng văn bản để giữ nguyên số 0 đầu.";
                            if(formula!=null&&raw==null)item.Error="Công thức chưa có kết quả lưu; mở Excel, tính lại và lưu.";
                            cells[col]=item;sheet.Columns=Math.Max(sheet.Columns,col);
                        }
                        if(cells.Count>0)sheet.Rows[rowNo]=cells;
                    }result.Sheets.Add(sheet);
                }
            }
            if(result.Sheets.Count==0)throw new InvalidDataException("Không đọc được sheet nào trong tệp Excel.");return result;
        }
        public static RoomCatalog Catalog(RoomSheet sheet,int headerRow,int oldCol,int newCol,int idCol)
        {
            if(oldCol<1||newCol<1)throw new ArgumentException("Chọn cột tên cũ và tên mới.");if(oldCol==newCol)throw new ArgumentException("Tên cũ và tên mới phải chọn hai cột khác nhau.");var rows=new List<RoomEntry>();var problems=new List<string>();
            foreach(var pair in sheet.Rows.Where(x=>x.Key>headerRow)){
                var a=sheet.Cell(pair.Key,oldCol);var b=sheet.Cell(pair.Key,newCol);var id=idCol>0?sheet.Cell(pair.Key,idCol):a;
                string error=String.Join(" ",new[]{a.Error,b.Error,id.Error}.Where(s=>!String.IsNullOrEmpty(s)).Distinct());
                if(error.Length>0){problems.Add("Dòng "+pair.Key+": "+error);continue;}
                if(String.IsNullOrWhiteSpace(a.Text)&&String.IsNullOrWhiteSpace(b.Text)&&String.IsNullOrWhiteSpace(id.Text))continue;
                rows.Add(new RoomEntry{Id=id.Text,OldName=a.Text,NewName=b.Text,ExcelRow=pair.Key});
            }
            var catalog=RoomCatalog.Build(rows);catalog.Problems.AddRange(problems);return catalog;
        }
    }
}
