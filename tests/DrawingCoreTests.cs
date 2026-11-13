using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MedGas2024.Core;
public static class DrawingCoreTests
{
    private static readonly List<string> log=new List<string>();
    static void Check(bool value,string message){if(!value)throw new Exception(message);log.Add("PASS: "+message);Console.WriteLine(log.Last());}
    static bool Reject(Action action){try{action();return false;}catch(ArgumentException){return true;}}
    public static int Main(string[] args)
    {
        try
        {
            int nativeReads=0;Func<string> native=()=>{nativeReads++;return "D15.Oxy";};
            Check(LinetypeSegments.ReadText(false,0,native)==""&&nativeReads==0,"Plain dash never calls native TextAt");
            Check(LinetypeSegments.ReadText(false,0,()=>{throw new InvalidOperationException("eNotApplicable on gap");})=="","Plain gap safely avoids a throwing native text accessor");
            Check(LinetypeSegments.ReadText(true,132,native)==""&&nativeReads==0,"Numbered SHX shape never calls native TextAt despite having a style");
            Check(LinetypeSegments.ReadText(true,0,native)=="D15.Oxy"&&nativeReads==1,"Text segment preserves its exact native label and is read once");
            Check(LinetypeSegments.ReadText(true,0,()=>null)=="","Missing text safely becomes an empty label");
            var mixed=new[]{LinetypeSegments.ReadText(false,0,native),LinetypeSegments.ReadText(true,0,native),LinetypeSegments.ReadText(false,0,native)};
            Check(mixed.SequenceEqual(new[]{"","D15.Oxy",""})&&nativeReads==2,"Three-part text pattern never requests text for its dash or trailing gap");
            bool propagated=false;try{LinetypeSegments.ReadText(true,0,()=>{throw new InvalidOperationException("unexpected native failure");});}catch(InvalidOperationException){propagated=true;}Check(propagated,"Unexpected text-read errors remain visible rather than silently losing labels");
            var rows=new List<DrawingLayerInfo>{new DrawingLayerInfo{LayerName="Mr.T_OXY_D15_RAW",LinetypeName="CustomOxy",Text="D15.Oxy",Description="Ống ôxy hiện hành",Managed=true,UsedCount=5},new DrawingLayerInfo{LayerName="Mr.T_OXY_D22_EST",Text="D22.Oxy",Managed=true},new DrawingLayerInfo{LayerName="OTHER_HDPE_110",Text="D110.HDPE",Description="Ống nước",UsedCount=2},new DrawingLayerInfo{LayerName="Mr.T_PV_abc_OXY",Text="D15.Oxy",Managed=true,UsedCount=1}};
            Func<string,string,string,bool,bool,List<DrawingLayerInfo>> find=(q,pre,s,m,u)=>DrawingSearch.Filter(rows,q,pre,s,m,u);
            Check(find("oxy 15","","",true,false).Single()==rows[0],"Search combines system and size independently of punctuation");
            Check(find("oxy15","","",true,false).Single()==rows[0],"Compact system-first search resolves diameter-first labels");
            Check(find("D15.Oxy","","",true,false).Single()==rows[0],"Exact on-line label can be searched");
            Check(find("HDPE110","","",false,false).Single()==rows[2],"Custom non-Mr.T layer is searchable when scope is widened");
            Check(find("ong oxy hien hanh","","",true,false).Single()==rows[0],"Vietnamese descriptions accept unaccented input");
            Check(find("","mr.t_","_raw",true,false).Single()==rows[0],"Literal prefix and suffix are case-insensitive and combined");
            Check(find("","","",true,true).Count==1,"Used-only excludes unused definitions and private previews");
            Check(find("","","",false,false).Count==3,"Preview layer remains excluded even with managed-only off");
            Check(find("missing","","",false,false).Count==0,"Unmatched search produces an empty list");
            Check(rows[0].UsedCount==5&&rows[0].Text=="D15.Oxy","Filtering never changes original drawing data");
            var edit=new DefinitionEdit{Text="D110.HDPE",Description="Ống mới",Dash=3,Before=1,After=1,Height=.35,X=-1,Y=-.1};edit.Validate(true);Check(true,"Valid actual-definition edit accepts negative offsets and Vietnamese description");
            edit.Height=Double.NaN;Check(Reject(()=>edit.Validate(true)),"Nonfinite height rejected before DWG access");edit.Height=.35;edit.Text="bad,text";Check(Reject(()=>edit.Validate(true)),"LIN control characters cannot enter label");edit.Text="D15.Oxy";edit.ChangeColor=true;edit.Aci=256;Check(Reject(()=>edit.Validate(true)),"ByLayer cannot accidentally become a Layer ACI");
            var p=Profile.Load(Path.Combine(args[0],"profiles","mrt-default.json"));string original=p.StyleName;p.SessionStyleName="MrT_TX_PRIVATE_TEST";Check(p.StyleName=="MrT_TX_PRIVATE_TEST","Create-only batch can use a private text style");
            string path=Path.Combine(args[0],"evidence","transient-profile.json");p.Save(path);var restored=Profile.Load(path);Check(restored.SessionStyleName==null&&restored.StyleName==original&&!File.ReadAllText(path).Contains("PRIVATE_TEST"),"Session style never leaks into saved configuration");
            log.Add("Completed "+log.Count+" drawing core assertions.");return 0;
        }
        catch(Exception ex){log.Add("FAIL: "+ex);Console.WriteLine(ex);return 1;}
        finally{File.WriteAllLines(Path.Combine(args[0],"evidence","drawing-core-tests.txt"),log);}
    }
}
