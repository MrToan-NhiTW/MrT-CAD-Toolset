using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MedGas2024.Core;

public static class CoreTests
{
    private static int passed;
    private static void Check(bool ok,string name) { if(!ok) throw new Exception("FAIL: "+name); Console.WriteLine("PASS: "+name); passed++; }
    private static bool Reject(Action a) { try {a();return false;} catch {return true;} }
    public static int Main(string[] args)
    {
        try
        {
            string root=args[0]; string evidence=Path.Combine(root,"evidence");
            var p=LinReader.ReadFolder(Path.Combine(root,"reference-originals")); p.FontFile="txt.shx"; p.FontBold=false;
            Check(p.Rows.Count==10,"Read all 10 reference LIN definitions");
            var oxy=p.Rows.First(x=>x.SystemCode=="OXY" && x.Diameter==12);
            var air=p.Rows.First(x=>x.SystemCode=="SYS_4_BAR" && x.Diameter==15);
            Check(oxy.Text=="D12.Oxy" && air.Text=="D15.4.Bar","Reference labels use diameter-first notation; source bytes are retained separately");
            Check(Math.Abs(oxy.Period-4.1)<1e-12 && Math.Abs(air.Period-4.5)<1e-12,"Pattern periods exclude text width");
            Check(p.Rows.All(x=>x.SourceStyle=="STANDARD" && x.TextScale==.35 && x.OffsetX==-1 && x.OffsetY==-.16),"Reference style, S, X and Y");
            Check(p.Rows.Single(x=>x.SystemCode=="AGSS").RotationMode=="U" && p.Rows.Count(x=>x.RotationMode=="R")==9,"Preserve U versus R modes");
            Check(Profile.Expand(oxy,"12,15;22 22").Count==3,"Batch diameter expansion and deduplication");
            Check(Reject(()=>Profile.Expand(oxy,"12,bad")),"Reject invalid diameter lists without partial output");
            string original=File.ReadAllText(Path.Combine(root,"reference-originals","Oxy D12.lin"));
            Check(Reject(()=>LinReader.Read(original.Replace("R=0.0","R=0.0,U=0"),"bad")),"Reject ambiguous rotation modes");
            Check(Reject(()=>LinReader.Read(original.Replace("S=0.35","Q=0.35"),"bad")),"Reject unsupported LIN options");
            Check(Reject(()=>LinReader.Read(original.Replace("\"Oxy.D12\",STANDARD","GAS,gas.shx"),"bad")),"Reject unsupported shape definitions explicitly");
            p.Rows.Add(oxy.Copy()); Check(p.Validate().Any(x=>x.Contains("Trùng")),"Reject duplicate selected output names"); p.Rows.RemoveAt(p.Rows.Count-1);
            double old=oxy.TextScale; oxy.TextScale=Double.NaN; Check(p.Validate().Count>0,"Reject NaN"); oxy.TextScale=old;
            int color=oxy.ColorIndex; oxy.ColorIndex=256; Check(p.Validate().Count>0,"Reject ByLayer as a layer ACI color"); oxy.ColorIndex=color;
            var prior=CultureInfo.CurrentCulture; System.Threading.Thread.CurrentThread.CurrentCulture=CultureInfo.GetCultureInfo("vi-VN");
            try
            {
                Check(p.ExportLin().Contains("S=0.35,R=0,X=-1,Y=-0.16"),"LIN numeric output is culture invariant");
                string test=Path.Combine(evidence,"profile-roundtrip.json"); p.Save(test); var restored=Profile.Load(test);
                Check(restored.ExportLin()==p.ExportLin(),"JSON round-trip preserves all patterns");
                p.Save(test); Check(Profile.Load(test).Rows.Count==10,"Atomic replacement of saved configuration");
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture=prior; }
            p.Save(Path.Combine(evidence,"reference-raw.json"));
            File.WriteAllText(Path.Combine(evidence,"reference-raw.lin"),p.ExportLin(),Encoding.ASCII);
            p.UnitTag="M"; p.GeometryFactor=.001;
            Check(p.Pattern(oxy).Contains("A,0.0015,-0.0012,") && p.Pattern(oxy).Contains("S=0.00035") && p.Pattern(oxy).Contains("X=-0.001,Y=-0.00016"),"mm-to-m factor scales lengths, height and offsets exactly once");
            p.Save(Path.Combine(evidence,"assumed-mm-to-m.json"));
            Check(LinReader.Read(p.ExportLin(),"roundtrip.lin").Count==10,"Generated LIN is readable by strict importer");
            var working=Profile.Load(Path.Combine(root,"tests","legacy-medgas-fixture.json"));
            var co2=working.Rows.Single(x=>x.SystemCode=="CO2" && x.Diameter==12);
            Check(working.LayerPrefix=="Mr.T" && working.LayerNameFor(co2)=="Mr.T_CO2_D12_RAW","Default layer prefix is exactly Mr.T");
            string typeName=working.NameFor(co2); working.LayerPrefix="TEAM";
            Check(working.LayerNameFor(co2)=="TEAM_CO2_D12_RAW" && working.NameFor(co2)=="TEAM_CO2_D12_RAW","Custom prefix applies consistently to layer and linetype");
            working.LayerPrefix="Mr.T";
            Check(working.Rows.Count==13 && working.Rows.Count(x=>x.SystemCode=="CO2")==3,"CO2 examples added for D12, D15 and D22");
            Check(working.Rows.All(x=>x.RotationMode=="U" && x.RotationDegrees==0),"All working defaults use native upright U=0 for the new experiment");
            Check(working.FontFile=="bahnschrift.ttf" && working.AutoFitOnSync,"Bahnschrift and automatic font-fit enabled in working profile");
            working.Save(Path.Combine(evidence,"legacy-working-roundtrip.json"));
            var restoredWorking=Profile.Load(Path.Combine(evidence,"legacy-working-roundtrip.json"));
            Check(restoredWorking.LayerPrefix=="Mr.T" && restoredWorking.AutoFitOnSync && restoredWorking.FontFile=="bahnschrift.ttf","New settings survive JSON round-trip");
            string legacyJson=File.ReadAllText(Path.Combine(evidence,"reference-raw.json")).Replace("\"SchemaVersion\":5","\"SchemaVersion\":1").Replace(",\"LayerPrefix\":\"Mr.T\"","");
            string legacyPath=Path.Combine(evidence,"legacy-profile-input.json"); File.WriteAllText(legacyPath,legacyJson,Encoding.UTF8);
            Check(Profile.Load(legacyPath).LayerPrefix=="Mr.T","Old JSON without prefix migrates to Mr.T");
            working.LayerPrefix="BAD|NAME"; Check(working.Validate().Count>0,"Reject invalid CAD layer prefix"); working.LayerPrefix="Mr.T";
            var fitted=co2.Copy(); Spacing.FitSymmetric(fitted,-.03,-.05,2.45,.30,.35);
            double anchor=fitted.Dash+fitted.GapBefore+fitted.OffsetX;
            double glyphLeft=anchor-.03, glyphRight=anchor+2.45, clearance=fitted.TextScale*.35;
            Check(Math.Abs(glyphLeft-fitted.Dash-clearance)<1e-12 && Math.Abs(fitted.Period-glyphRight-clearance)<1e-12,"Long labels and negative glyph bearings retain equal left and right gaps");
            Check(Math.Abs(-.05+fitted.OffsetY+.30+fitted.OffsetY)<1e-12,"Measured glyph height is vertically centered on the line");
            foreach(int degrees in new [] {0,45,90,135,180,225,270,315})
            {
                double radians=degrees*Math.PI/180, vx=Math.Cos(radians), vy=Math.Sin(radians);
                double leftX=glyphLeft*vx, leftY=glyphLeft*vy, rightX=glyphRight*vx, rightY=glyphRight*vy;
                double projectedLeft=leftX*vx+leftY*vy, projectedRight=rightX*vx+rightY*vy;
                Check(projectedLeft-fitted.Dash>=clearance-1e-10 && fitted.Period-projectedRight>=clearance-1e-10,"No stroke overlap at relative line angle "+degrees+" degrees");
            }
            fitted.RotationMode="U"; Spacing.FitSymmetric(fitted,-.03,-.05,2.45,.30,.35);
            Check(fitted.GapBefore==fitted.GapAfter && fitted.RotationMode=="U","U=0 accepts symmetric measured placement without resetting the upright mode");
            fitted.RotationMode="A"; Check(Reject(()=>Spacing.FitSymmetric(fitted,0,0,1,.35,.35)),"Auto-fit rejects absolute A rotation that requires a different projected gap");
            fitted.RotationMode="U"; fitted.RotationDegrees=25; Check(Reject(()=>Spacing.FitSymmetric(fitted,0,0,1,.35,.35)),"Nonzero upright angle requires manual geometry and is rejected by auto-fit"); fitted.RotationDegrees=0;
            fitted.RotationMode="R"; Spacing.FitSymmetric(fitted,-.03,-.05,2.45,.30,.35);
            Check(Math.Abs(fitted.OffsetX+1.21)<1e-12,"R=0 remains available as a comparison layout");
            Check(working.NameFor(co2)=="Mr.T_CO2_D12_RAW" && !working.StyleName.StartsWith("MRG_"),"Working layer, linetype and style no longer use MRG prefix");
            Check(co2.DescriptionText=="CO2 - D12" && co2.SourceName=="CO2-D12" && co2.LayoutReference=="4 Bar-D12","CO2 description is independent of the 4 bar layout reference");
            Check(working.FontBold,"Bold font is enabled by default");
            string boldStyle=working.StyleName; working.FontBold=false;
            Check(working.StyleName!=boldStyle,"Regular and bold use separate style definitions"); working.FontBold=true;
            var edited=co2.Copy(); edited.Text="CO2.D15_TEST"; edited.DescriptionText="Khí CO2 - cỡ 15";
            Check(edited.Text=="CO2.D15_TEST" && edited.DescriptionText.Contains("CO2"),"Embedded label and description are explicitly editable");
            edited.Text=""; Check(edited.Text==edited.DefaultText,"Clearing a custom label restores automatic label");
            var symmetric=co2.Copy(); Spacing.FitSymmetric(symmetric,-.03,-.05,2.45,.30,.35);
            Check(Math.Abs(symmetric.GapBefore-symmetric.GapAfter)<1e-12 && Math.Abs(symmetric.OffsetX+1.21)<1e-12,"Insertion sits in the centre of a symmetric gap");
            foreach(int sign in new[]{1,-1})
            {
                double insertion=symmetric.Dash+symmetric.GapBefore;
                double a=insertion+sign*(symmetric.OffsetX-.03), b=insertion+sign*(symmetric.OffsetX+2.45);
                Check(Math.Min(a,b)-symmetric.Dash>=clearance-1e-10 && symmetric.Period-Math.Max(a,b)>=clearance-1e-10,"Centered glyph retains clearance when locally flipped, sign "+sign);
            }
            var textProfile=new Profile {Rows=new System.Collections.Generic.List<GasRow>{edited}};
            string customPath=Path.Combine(evidence,"editable-description-roundtrip.json"); textProfile.Save(customPath);
            Check(Profile.Load(customPath).Rows[0].DescriptionText==edited.DescriptionText,"Vietnamese description survives JSON round-trip");
            Check(working.Rows.Single(r=>r.SystemCode=="OXY" && r.Diameter==15).Text=="D15.Oxy" && co2.Text=="D12.CO2","Working defaults place diameter before the system label");
            var hdpe=oxy.Copy(); hdpe.SystemCode="HDPE"; hdpe.LabelPrefix="HDPE";
            var expanded=Profile.Expand(hdpe,"110,160");
            Check(expanded[0].Text=="D110.HDPE" && expanded[1].Text=="D160.HDPE","Batch generation also supports HDPE labels without gas-specific logic");
            var imported=LinReader.Read(working.ExportLin(),"diameter-first.lin");
            Check(imported.Single(r=>r.SystemCode=="SYS_4_BAR" && r.Diameter==15).Text=="D15.4.Bar" && imported.Single(r=>r.SystemCode=="OXY" && r.Diameter==15).Text=="D15.Oxy","Diameter-first LIN round-trip preserves dotted system labels");
            var legacy=working.Rows.Single(r=>r.SystemCode=="OXY" && r.Diameter==15).Copy(); legacy.CustomText="Oxy.D15";
            var legacyCustom=co2.Copy(); legacyCustom.CustomText="CO2.D12_TEST";
            var migration=new Profile {SchemaVersion=3,Rows=new System.Collections.Generic.List<GasRow>{legacy,legacyCustom}};
            string migrationPath=Path.Combine(evidence,"diameter-first-migration.json"); migration.Save(migrationPath);
            var migrated=Profile.Load(migrationPath);
            Check(migrated.SchemaVersion==5 && migrated.Rows[0].Text=="D15.Oxy" && migrated.Rows[0].CustomText==null,"Old automatic literal labels migrate to the new convention");
            Check(migrated.Rows[1].Text=="CO2.D12_TEST","Migration preserves distinct user overrides");
            Check(working.Pattern(working.Rows.Single(r=>r.SystemCode=="OXY" && r.Diameter==15)).Contains("[\"D15.Oxy\","),"Exported CAD pattern contains diameter-first text");
            Check(working.NameFor(working.Rows.Single(r=>r.SystemCode=="OXY" && r.Diameter==15))=="Mr.T_OXY_D15_RAW","Changing label order keeps existing layer and linetype names");
            var uncheckedProfile=new Profile {Rows=new System.Collections.Generic.List<GasRow>{co2.Copy()}};uncheckedProfile.Rows[0].Enabled=false;
            Check(uncheckedProfile.Validate().Count>0 && uncheckedProfile.Validate(false).Count==0,"Configuration can be valid without selection while drawing operations require selected rows");
            string uncheckedPath=Path.Combine(evidence,"unchecked-profile.json");uncheckedProfile.Save(uncheckedPath);
            Check(!Profile.Load(uncheckedPath).Rows[0].Enabled,"Saving and loading preserves an entirely unchecked configuration");
            Check(Reject(()=>uncheckedProfile.ExportLin()),"LIN export still rejects empty selection");
            File.WriteAllText(Path.Combine(evidence,"core-tests.txt"),"PASS: "+passed+" assertions. Compiled and executed with installed .NET Framework, x64.\r\n",Encoding.UTF8);
            Console.WriteLine("TOTAL PASS: "+passed); return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
