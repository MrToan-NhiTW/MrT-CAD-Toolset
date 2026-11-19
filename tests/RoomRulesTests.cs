using System;
using System.Linq;
using MedGas2024.Core;
public static class RoomRulesTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    public static int Main(){try{
        var entries=new[]{new RoomEntry{Id="PHÒNG KHÁM",OldName="PHÒNG KHÁM",NewName="PHÒNG KHÁM MỚI",ExcelRow=2},new RoomEntry{Id="PHÒNG XÉT NGHIỆM",OldName="PHÒNG XÉT NGHIỆM",NewName="A",ExcelRow=3},new RoomEntry{Id="PHÒNG XÉT NGHIỆM",OldName="PHÒNG XÉT NGHIỆM",NewName="B",ExcelRow=4}};
        var c=RoomCatalog.Build(entries);Check(c.Unique.Count==1&&c.Ambiguous.Contains("PHÒNG XÉT NGHIỆM"),"Conflicting repeated IDs are blocked");Check(c.Find("PHONG KHAM")==null&&c.Find("phòng khám")==null,"Matching does not guess case or Vietnamese accents");
        var tag=new RoomTag{Id="PHÒNG KHÁM",Original="PHÒNG KHÁM",OldName="PHÒNG KHÁM",NewName="PHÒNG KHÁM MỚI",LastText="PHÒNG KHÁM"};var round=RoomTag.Parse(tag.Serialize());string reason;Check(round.Id==tag.Id&&RoomRules.CanUpdate(round,tag.LastText,c.Find(tag.Id),true,out reason),"Vietnamese original ID survives serialization and allows a reviewed switch");Check(!RoomRules.CanUpdate(round,"ĐÃ SỬA",c.Find(tag.Id),true,out reason)&&reason.Contains("thủ công"),"Manual edits are flagged instead of overwritten");
        Check(!RoomRules.CanUpdate(round,tag.LastText,c.Find("PHÒNG XÉT NGHIỆM"),true,out reason),"Ambiguous ID cannot be updated");var rows=RoomCatalog.Build(new[]{
            new RoomEntry{Id="a",OldName="Phòng khám",NewName="Khám 2"},new RoomEntry{Id="b",OldName="Kho",NewName="K1"},
            new RoomEntry{Id="c",OldName="Kho",NewName="K2"},new RoomEntry{Id="d",OldName="Trực",NewName="T"},
            new RoomEntry{Id="e",OldName="Đã gắn",NewName="X"},new RoomEntry{Id="f",OldName="Mất vị trí",NewName="Y"}});
        var hits=new[]{new RoomNameMatch{Text="  PHÒNG  KHÁM "},new RoomNameMatch{Text="Kho"},
            new RoomNameMatch{Text="Trực"},new RoomNameMatch{Text="trực"},new RoomNameMatch{Text="Đã gắn"},
            new RoomNameMatch{Text="X",Tag="e"},new RoomNameMatch{Text="Phong kham"}};
        var match=RoomNameMatcher.Match(hits,rows,true);
        Check(match.Count(x=>x.Ready)==1&&match[0].Entry.Id=="a","Only unique unlinked matches qualify; accents remain significant");
        Check(match.Last().Entry.Id=="f"&&match.Last().Status.Contains("Chưa tìm thấy"),"Unplaced Excel rooms remain visible in the review");
        Check(!RoomNameMatcher.SingleLineSafe("A\nB")&&!RoomNameMatcher.SingleLineSafe("%%d"),"Single line labels reject paragraph and CAD control syntax");
        return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
