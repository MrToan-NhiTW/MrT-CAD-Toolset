using System;
using MedGas2024.Core;
public static class RoomWorkbookTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    public static int Main(string[] args){try{
        var b=RoomWorkbook.Read(args[0]);Check(b.Sheets.Count==2&&b.Sheets[0].Name=="Tên phòng"&&b.Sheets[1].Name=="Tầng 2","Loads sheet names and relationships");
        Check(b.Sheets[0].Cell(2,1).Text=="PHÒNG KHÁM"&&b.Sheets[0].Cell(5,1).Text=="001","Unicode and identifier leading zeros are preserved");
        var c=RoomWorkbook.Catalog(b.Sheets[0],1,1,2,0);Check(c.Find("PHÒNG KHÁM").NewName=="PHÒNG ĐIỀU TRỊ"&&c.Ambiguous.Contains("PHÒNG XÉT NGHIỆM"),"Maps old-to-new and detects duplicate conflicting original IDs");
        var d=RoomWorkbook.Catalog(b.Sheets[1],3,2,3,0);Check(d.Find("PHÒNG HỒI SỨC").NewName=="PHÒNG ĐIỀU TRỊ TÍCH CỰC","Custom sheet, row and columns work");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
