using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using MedGas2024.Core;
public static class RoomWorkbookTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    static void Entry(ZipArchive zip,string path,string xml){using(var w=new StreamWriter(zip.CreateEntry(path).Open(),new UTF8Encoding(false)))w.Write(xml);}
    // Fake workbook shaped like the floor sample: title rows, header on row 3 (Tên phòng cũ / TÊN PHÒNG),
    // codes stored as Excel numbers with General, 000 and "P"000 formats, a date, a decimal and a formula.
    static string NumericWorkbook()
    {
        var path=Path.Combine(Path.GetTempPath(),"MrT-room-numeric-"+Guid.NewGuid().ToString("N")+".xlsx");
        const string ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Func<string,string> s=t=>"<c r=\"{0}\" t=\"inlineStr\"><is><t>"+System.Security.SecurityElement.Escape(t)+"</t></is></c>";
        Func<string,string,string,string> row=(r,a,b)=>"<row r=\""+r+"\">"+a+(b??"")+"</row>";
        Func<string,string,string> text=(cell,t)=>String.Format(s(t),cell);
        Func<string,string,int,string> num=(cell,v,style)=>"<c r=\""+cell+"\" s=\""+style+"\"><v>"+v+"</v></c>";
        var rows=new StringBuilder();
        rows.Append(row("1",text("A1","DANH SÁCH PHÒNG TẦNG 4"),null));
        rows.Append(row("3",text("A3","Tên phòng cũ"),text("B3","TÊN PHÒNG")));
        rows.Append(row("4",num("A4","510",0),num("B4","610",0)));
        rows.Append(row("5",num("A5","7",1),text("B5","Phòng 007")));
        rows.Append(row("6",text("A6","007A"),text("B6","Kho 7A")));
        rows.Append(row("7",num("A7","512",0),text("B7","Hành chính")));
        rows.Append(row("8",num("A8","512",0),text("B8","Kho")));
        rows.Append(row("9",num("A9","45000",2),text("B9","Ngày")));
        rows.Append(row("10",text("A10","Phòng 9"),num("B10","3.5",3)));
        rows.Append(row("11",text("A11","001"),text("B11","Trực")));
        rows.Append(row("12",num("A12","12",4),text("B12","Phòng P12")));
        rows.Append(row("13","<c r=\"A13\"><f>500+13</f><v>513</v></c>",text("B13","Họp")));
        rows.Append(row("14",num("A14","514",5),num("B14","1234",6)));
        using(var file=new FileStream(path,FileMode.CreateNew))using(var zip=new ZipArchive(file,ZipArchiveMode.Create))
        {
            Entry(zip,"[Content_Types].xml","<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>");
            Entry(zip,"xl/workbook.xml","<?xml version=\"1.0\"?><workbook xmlns=\""+ns+"\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Tầng 4\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Entry(zip,"xl/_rels/workbook.xml.rels","<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Entry(zip,"xl/styles.xml","<?xml version=\"1.0\"?><styleSheet xmlns=\""+ns+"\"><numFmts count=\"2\"><numFmt numFmtId=\"164\" formatCode=\"000\"/><numFmt numFmtId=\"165\" formatCode=\"&quot;P&quot;000\"/></numFmts>"+
                "<cellXfs count=\"7\"><xf numFmtId=\"0\"/><xf numFmtId=\"164\"/><xf numFmtId=\"14\"/><xf numFmtId=\"2\"/><xf numFmtId=\"165\"/><xf numFmtId=\"49\"/><xf numFmtId=\"3\"/></cellXfs></styleSheet>");
            Entry(zip,"xl/worksheets/sheet1.xml","<?xml version=\"1.0\"?><worksheet xmlns=\""+ns+"\"><sheetData>"+rows+"</sheetData></worksheet>");
        }
        return path;
    }
    public static int Main(string[] args){try{
        var b=RoomWorkbook.Read(args[0]);Check(b.Sheets.Count==2&&b.Sheets[0].Name=="Tên phòng"&&b.Sheets[1].Name=="Tầng 2","Loads sheet names and relationships");
        Check(b.Sheets[0].Cell(2,1).Text=="PHÒNG KHÁM"&&b.Sheets[0].Cell(5,1).Text=="001","Unicode and identifier leading zeros are preserved");
        var c=RoomWorkbook.Catalog(b.Sheets[0],1,1,2,0);Check(c.Find("PHÒNG KHÁM").NewName=="PHÒNG ĐIỀU TRỊ"&&c.Ambiguous.Contains("PHÒNG XÉT NGHIỆM"),"Maps old-to-new and detects duplicate conflicting original IDs");
        Check(c.ConflictTargets["PHÒNG XÉT NGHIỆM"].Count==2,"A conflicting old name lists both Excel targets");
        var d=RoomWorkbook.Catalog(b.Sheets[1],3,2,3,0);Check(d.Find("PHÒNG HỒI SỨC").NewName=="PHÒNG ĐIỀU TRỊ TÍCH CỰC","Custom sheet, row and columns work");var test=new RoomSheet();test.Columns=2;
            test.Rows[4]=new System.Collections.Generic.Dictionary<int,RoomCell>{{1,new RoomCell{Text="Tên phòng cũ"}},{2,new RoomCell{Text="Tên phòng mới"}}};
            test.Rows[5]=new System.Collections.Generic.Dictionary<int,RoomCell>{{1,new RoomCell{Text="001"}},{2,new RoomCell{Text="Phòng trực"}}};
            Check(RoomWorkbook.HeaderRow(test)==4,"Room headers below title rows are detected");
            var fallback=RoomWorkbook.Catalog(test,1,1,2,0);
            Check(fallback.Unique.Count==1&&fallback.Find("001")!=null,"A repeated header never becomes a room and numeric-looking text keeps its zeros");
        var path=NumericWorkbook();
        try
        {
            var n=RoomWorkbook.Read(path).Sheets[0];
            Check(RoomWorkbook.HeaderRow(n)==3,"Header Tên phòng cũ / TÊN PHÒNG on row 3 is detected");
            Check(n.Cell(4,1).Text=="510"&&n.Cell(4,2).Text=="610"&&n.Cell(4,1).Error=="","General whole numbers read as codes 510 and 610");
            Check(n.Cell(5,1).Text=="007"&&n.Cell(6,1).Text=="007A","Number 7 with format 000 reads 007; text 007A is untouched");
            Check(n.Cell(12,1).Text=="P012"&&n.Cell(13,1).Text=="513"&&n.Cell(14,1).Text=="514","Prefix format \"P\"000, formula result and @ format read as shown in Excel");
            Check(n.Cell(9,1).Error.Contains("ngày")&&n.Cell(10,2).Error.Contains("thập phân")&&n.Cell(14,2).Error.Contains("định dạng"),"Dates, decimals and thousand-separator numbers stay explicit errors");
            var cat=RoomWorkbook.Catalog(n,3,1,2,0);
            Check(cat.Find("510").NewName=="610"&&cat.Find("007").NewName=="Phòng 007"&&cat.Find("007A").NewName=="Kho 7A"&&cat.Find("001")!=null&&cat.Find("P012")!=null,"Numeric and text codes all enter the catalog");
            Check(cat.Ambiguous.Contains("512")&&cat.ConflictTargets["512"].SequenceEqual(new[]{"Hành chính","Kho"}),"Two rows for 512 with different targets are a reported conflict");
            Check(cat.Problems.Count(p=>p.Contains("Dòng 9")||p.Contains("Dòng 10")||p.Contains("Dòng 14"))==3&&cat.ErrorNames.Contains("Phòng 9"),"Only the genuinely unreadable rows are warnings");
        }
        finally{File.Delete(path);}
        string text2,error;
        Check(RoomWorkbook.NumericCode("7","\"Phòng \"00",out text2,out error)&&text2=="Phòng 07","Quoted prefix with padding");
        Check(!RoomWorkbook.NumericCode("7","hh:mm",out text2,out error)&&!RoomWorkbook.NumericCode("7","[$-409]d/m/yy",out text2,out error),"Time and locale date formats are rejected");
        return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
