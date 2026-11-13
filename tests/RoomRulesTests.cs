using System;
using MedGas2024.Core;
public static class RoomRulesTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    public static int Main(){try{
        var entries=new[]{new RoomEntry{Id="PHÒNG KHÁM",OldName="PHÒNG KHÁM",NewName="PHÒNG KHÁM MỚI",ExcelRow=2},new RoomEntry{Id="PHÒNG XÉT NGHIỆM",OldName="PHÒNG XÉT NGHIỆM",NewName="A",ExcelRow=3},new RoomEntry{Id="PHÒNG XÉT NGHIỆM",OldName="PHÒNG XÉT NGHIỆM",NewName="B",ExcelRow=4}};
        var c=RoomCatalog.Build(entries);Check(c.Unique.Count==1&&c.Ambiguous.Contains("PHÒNG XÉT NGHIỆM"),"Conflicting repeated IDs are blocked");Check(c.Find("PHONG KHAM")==null&&c.Find("phòng khám")==null,"Matching does not guess case or Vietnamese accents");
        var tag=new RoomTag{Id="PHÒNG KHÁM",Original="PHÒNG KHÁM",OldName="PHÒNG KHÁM",NewName="PHÒNG KHÁM MỚI",LastText="PHÒNG KHÁM"};var round=RoomTag.Parse(tag.Serialize());string reason;Check(round.Id==tag.Id&&RoomRules.CanUpdate(round,tag.LastText,c.Find(tag.Id),true,out reason),"Vietnamese original ID survives serialization and allows a reviewed switch");Check(!RoomRules.CanUpdate(round,"ĐÃ SỬA",c.Find(tag.Id),true,out reason)&&reason.Contains("thủ công"),"Manual edits are flagged instead of overwritten");
        Check(!RoomRules.CanUpdate(round,tag.LastText,c.Find("PHÒNG XÉT NGHIỆM"),true,out reason),"Ambiguous ID cannot be updated");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
