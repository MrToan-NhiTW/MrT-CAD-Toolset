using System;
using System.Linq;
using System.Collections.Generic;
using MedGas2024.Core;
public static class RoomRulesTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    static RoomNameMatch Shared(string text,string place){return new RoomNameMatch{Text=text,Place=place,NeedsLabel=true,SingleLine=true};}
    static void BlockLabels()
    {
        var catalog=RoomCatalog.Build(new[]{new RoomEntry{Id="510",OldName="510",NewName="610",ExcelRow=4},new RoomEntry{Id="520",OldName="520",NewName="620",ExcelRow=5},
            new RoomEntry{Id="512",OldName="512",NewName="Hành chính",ExcelRow=7},new RoomEntry{Id="512",OldName="512",NewName="Kho",ExcelRow=8},
            new RoomEntry{Id="530",OldName="530",NewName="Dòng\nhai",ExcelRow=9}});
        Func<RoomMatchOptions,RoomNameMatch[],System.Collections.Generic.List<RoomNameMatch>> run=(o,h)=>RoomNameMatcher.Match(h,catalog,true,o);
        var off=new RoomMatchOptions();var on=new RoomMatchOptions{CreateLabels=true};var all=new RoomMatchOptions{CreateLabels=true,AllowRepeats=true};
        var a=Shared("510","1A/2B/3C");
        Check(!run(off,new[]{a})[0].Ready&&a.Status.Contains("Tạo nhãn riêng")&&a.Entry.NewName=="610","Shared block text shows its Excel target and waits for the label option");
        Check(run(on,new[]{a})[0].Ready&&a.Action==RoomNameMatcher.ActionLabel,"With the option a unique shared text is ready to get its own label");
        var multi=new[]{new RoomNameMatch{Text="530",SingleLine=false,Place="5"}};
        Check(run(on,multi)[0].Ready&&run(on,new[]{Shared("530","1/9")})[0].Ready,"A label (MTEXT) or MTEXT may show a two-line name");
        var c1=Shared("512","1/7");run(on,new[]{c1});
        Check(c1.Ready&&c1.Splits.Count==2&&c1.Status.Contains("tách 2")&&c1.ExcelTargets=="Hành chính | Kho","A split old room is ready and lists its new rooms");
        var clash=RoomCatalog.Build(new[]{new RoomEntry{Id="X",OldName="540",NewName="A"},new RoomEntry{Id="X",OldName="541",NewName="B"}});
        var c2=Shared("540","1/9");RoomNameMatcher.Match(new[]{c2},clash,true,on);
        Check(!c2.Ready&&c2.Status.StartsWith("Xung đột"),"A real ID conflict is named on the drawing row");
        var direct=new RoomNameMatch{Text="510",Place="7",Direct=true};RoomNameMatcher.Match(new[]{direct},catalog,true,new RoomMatchOptions{Blocks=true,ReplaceOld=true});
        Check(direct.Ready&&direct.Action==RoomNameMatcher.ActionReplace,"With room blocks a direct old text is replaced");
        var place=Shared("520","30/1");var oldLabel=new RoomNameMatch{Text="620",Tag="520",Linked="30/1",LegacyLabel=true,Blocked="Đã gắn ID"};
        RoomNameMatcher.Match(new[]{place,oldLabel},catalog,true,new RoomMatchOptions{Blocks=true});
        Check(place.Ready&&place.Supersedes==oldLabel&&place.Action==RoomNameMatcher.ActionUpgrade,"A 2.0.2.4 MTEXT label is upgraded to a block instead of blocking its room");
        RoomNameMatcher.Match(new[]{place,oldLabel},catalog,true,on);
        Check(!place.Ready&&place.Status.StartsWith("Đã có nhãn"),"Without room blocks the old label still counts");
        var blocks=new RoomMatchOptions{Blocks=true,CreateLabels=true};
        var right=new RoomNameMatch{Text="512",Place="2",X=900,Y=0,NeedsLabel=true};var left=new RoomNameMatch{Text="512",Place="1",X=100,Y=0,NeedsLabel=true};
        RoomNameMatcher.Match(new[]{right,left},catalog,true,blocks);
        Check(left.Ready&&right.Ready&&left.Entry.NewName=="Hành chính"&&right.Entry.NewName=="Kho"&&left.Splits.Count==1&&left.Review&&right.Note.Contains("2/2"),"Split 512 found at two places: each place gets one new room, left to right, flagged for review");
        var third=new RoomNameMatch{Text="512",Place="3",X=500,Y=0,NeedsLabel=true};
        RoomNameMatcher.Match(new[]{right,left,third},catalog,true,blocks);
        Check(!left.Ready&&left.Status.Contains("3 vị trí nhưng 2 tên mới"),"More places than split names asks for manual placement");
        var single=Shared("512","5/5");RoomNameMatcher.Match(new[]{single},catalog,true,blocks);
        Check(single.Ready&&single.Splits.Count==2&&single.Review,"A split at one place is stacked and flagged for review");
        var r1=Shared("520","8/1");var r2=Shared("520","9/1");RoomNameMatcher.Match(new[]{r1,r2},catalog,true,new RoomMatchOptions{Blocks=true,AllowRepeats=true});
        Check(r1.Ready&&r2.Ready&&r1.Review&&r1.Note.Contains("2 vị trí"),"A repeated code applied everywhere is flagged for review");
        var plain=Shared("510","1/1");RoomNameMatcher.Match(new[]{plain},catalog,true,blocks);Check(plain.Ready&&!plain.Review,"A normal room is not flagged");
        var none=Shared("999","1/8");run(on,new[]{none});Check(none.Status.StartsWith("Thiếu ánh xạ"),"Text without an Excel row is reported as a missing mapping");
        var p1=Shared("520","10/1");var p2=Shared("520","20/1");
        run(on,new[]{p1,p2});Check(!p1.Ready&&!p2.Ready&&p1.Status.StartsWith("2 vị trí"),"Two places with one code need an explicit choice");
        run(all,new[]{p1,p2});Check(p1.Ready&&p2.Ready&&p1.Status.Contains("2 vị trí"),"Apply-all labels every place of a repeated code");
        var label=new RoomNameMatch{Text="620",Tag="520",Linked="10/1",Blocked="Đã gắn ID"};
        var again=run(all,new[]{p1,p2,label});
        Check(!p1.Ready&&p1.Status.StartsWith("Đã có nhãn")&&p2.Ready,"A rerun skips the place that already has a label and completes the other");
        run(on,new[]{p1,p2,label});Check(!p2.Ready&&p2.Status.Contains("ID đã gắn"),"Without apply-all a rerun never adds another label for an attached ID");
        var both=new RoomNameMatch{Text="620",Tag="520",Linked="20/1",Blocked="Đã gắn ID"};
        Check(!run(all,new[]{p1,p2,label,both}).Any(h=>h.Ready),"A third run with every place labelled creates nothing");
        var hidden=Shared("510","1/5");hidden.Blocked="Chữ hoặc block bị ẩn, khóa hay đóng băng";
        run(on,new[]{hidden});Check(!hidden.Ready&&hidden.Status==hidden.Blocked,"Hidden or locked text keeps its drawing reason");
        Check(run(on,new[]{hidden}).Count==run(on,new[]{hidden}).Count,"Matching the same scan twice gives the same rows");
    }
    static void Search()
    {
        Check(RoomSearch.Fold("  Phòng  ĐIỀU   Trị ")=="phong dieu tri","Search folds accents, đ and case");
        Check(RoomSearch.Matches("dieu tri -kho","PHÒNG ĐIỀU TRỊ","A1")&&!RoomSearch.Matches("dieu -tri","PHÒNG ĐIỀU TRỊ")&&RoomSearch.Matches("a1 phong","PHÒNG ĐIỀU TRỊ","A1"),"Every word must match across fields; -word excludes");
        var sorted=new[]{"P10","p2","P1","Kho","007","7A"}.OrderBy(x=>x,Comparer<string>.Create(RoomSearch.NaturalCompare)).ToArray();
        Check(sorted.SequenceEqual(new[]{"007","7A","Kho","P1","p2","P10"}),"Natural sort puts P2 before P10");
    }
    public static int Main(){try{
        var entries=new[]{new RoomEntry{Id="PHÒNG KHÁM",OldName="PHÒNG KHÁM",NewName="PHÒNG KHÁM MỚI",ExcelRow=2},new RoomEntry{Id="PHÒNG XÉT NGHIỆM",OldName="PHÒNG XÉT NGHIỆM",NewName="A",ExcelRow=3},new RoomEntry{Id="PHÒNG XÉT NGHIỆM",OldName="PHÒNG XÉT NGHIỆM",NewName="B",ExcelRow=4}};
        var c=RoomCatalog.Build(entries);Check(c.Unique.Count==3&&c.Find("PHÒNG XÉT NGHIỆM-1").NewName=="A"&&c.Find("PHÒNG XÉT NGHIỆM-2").Status==RoomCatalog.StatusSplit&&c.Ambiguous.Count==0,"One old room with two new names is a split with IDs -1/-2");
        var clash=RoomCatalog.Build(new[]{new RoomEntry{Id="X1",OldName="Kho A",NewName="K1"},new RoomEntry{Id="X1",OldName="Kho B",NewName="K2"}});Check(clash.Unique.Count==0&&clash.Ambiguous.Contains("X1"),"One ID with two different old names stays a conflict");
        var merge=RoomCatalog.Build(new[]{new RoomEntry{Id="501",OldName="501",NewName="Hội trường"},new RoomEntry{Id="502",OldName="502",NewName="Hội trường"},new RoomEntry{Id="503",OldName="503",NewName="503"},new RoomEntry{Id="504",OldName="504",NewName="Kho"}});
        Check(merge.Find("501").Status==RoomCatalog.StatusMerge&&merge.Find("502").Status==RoomCatalog.StatusMerge&&merge.Find("503").Status==RoomCatalog.StatusKeep&&merge.Find("504").Status==RoomCatalog.StatusRename,"Merge, keep and rename statuses come from Excel");
        Check(RoomRules.Display(merge.Find("501"),true,true)=="HỘI TRƯỜNG","Upper case keeps Vietnamese marks");Check(c.Find("PHONG KHAM")==null&&c.Find("phòng khám")==null,"Matching does not guess case or Vietnamese accents");
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
        BlockLabels();Search();
        return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
