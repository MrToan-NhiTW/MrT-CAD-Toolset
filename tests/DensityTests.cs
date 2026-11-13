using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class DensityTests
{
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS: "+name);}
    static IEnumerable<Control> All(Control c){foreach(Control x in c.Controls){yield return x;foreach(var y in All(x))yield return y;}}
    static T Named<T>(Control c,string name)where T:Control{return All(c).OfType<T>().Single(x=>x.Name==name);}
    static bool Reject(Action a){try{a();return false;}catch(ArgumentException){return true;}}
    [STAThread]public static int Main(){try{
        Check(DensityMath.IsMrT("Mr.T_VAC_D15_RAW")&&DensityMath.IsMrT("mr.t_R0_123")&&!DensityMath.IsMrT("Mr.Test")&&!DensityMath.IsMrT("Foreign_Mr.T_VAC"),"Only explicit Mr.T prefix is included");
        var o=new DensityOptions{Multiplier=2};Check(DensityMath.DashFor(o,10,4,.5,2)==16,"Multiplying full pattern period preserves the text gap and doubles spacing");
        o.Multiplier=.2;Check(Reject(()=>DensityMath.DashFor(o,10,4,1,1)),"Too-small target is blocked instead of removing the text gap");
        o.PaperMode=true;o.PaperMm=100;o.Denominator=100;var dash=DensityMath.DashFor(o,10,4,.5,2);Check(Math.Abs(DensityMath.EstimatePaper(dash+4,.5,2,100)-100)<1e-9,"Paper estimate accounts for entity scale, global scale and plot ratio");
        o.PaperMm=25;o.Denominator=200;var co2=DensityMath.DashFor(o,20,2.1,.25,1000);var vac=DensityMath.DashFor(o,7.25142,2.12,.25,1000);Check(Math.Abs(DensityMath.EstimatePaper(co2+2.1,.25,1000,200)-25)<1e-8&&Math.Abs(DensityMath.EstimatePaper(vac+2.12,.25,1000,200)-25)<1e-8,"CO2 and VAC with differing source patterns reach the same 25 mm paper estimate");
        var members=new[]{new DensityMember{Handle="A",Scale=.25},new DensityMember{Handle="B",Scale=.25},new DensityMember{Handle="C",Scale=.15}};var groups=DensityMath.ScaleGroups(members);Check(groups.Count==2&&groups[0].Scale==.25&&groups[0].Handles.Count==2&&groups[1].Handles.Single()=="C","Mixed entity scales retain their values; the most common scale is the layer pattern");
        foreach(var group in groups){var groupDash=DensityMath.DashFor(o,7.25142,2.12,group.Scale,1000);Check(Math.Abs(DensityMath.EstimatePaper(groupDash+2.12,group.Scale,1000,200)-25)<1e-8,"Every mixed-scale group reaches 25 mm at 1:200 in the model estimate");}
        Check(DensityMath.ReadableStem("Mr.T_CO2_D22_RAW","Mr.T_CO2_D22_RAW_D_da0e1468_D_dc978fd4_D_ea5bda14_D_2b96beb7_D_d6f8eacd","D22.CO2",false)=="Mr.T_CO2_D22_RAW","Repeated legacy density hash suffixes are removed using the layer family");
        Check(DensityMath.ReadableStem("Mr.T_OXY_D12_RAW","Mr.T_R0_7ad24ed123456789","D12.Oxy",true)=="Mr.T_D12.Oxy","Legacy hashed R override derives a readable name from its text");
        Check(DensityMath.ReadableStem("Mr.T_Bơm thải_D160_RAW","old","D160.Bơm thải",false)=="Mr.T_Bom_thai_D160_RAW","Readable density names retain the family without Vietnamese accents or spaces");
        Check(DensityMath.SameScale(.15,.15)&&!DensityMath.SameScale(.15,.16)&&!DensityMath.SameScale(1e-12,1e-9),"Paper mode can distinguish mixed scales, including small values");
        o.Multiplier=Double.PositiveInfinity;Check(Reject(()=>o.Validate()),"Non-finite controls are rejected");
        Application.EnableVisualStyles();var input=new List<DensityRow>{new DensityRow{Selected=true,Key="V|BASE",Layer="Mr.T_VAC_D15_RAW",Type="Mr.T_VAC_D15_RAW",Text="D15.VAC",Direct=3,State="Có thể xem trước"},new DensityRow{Key="S|BASE",Layer="Mr.T_SA7_D15_RAW",Type="Other",State="Bỏ: linetype không mang tiền tố Mr.T"}};int previews=0;
        using(var f=new DensityForm(input,(opt,names)=>{previews++;return new List<DensityRow>{new DensityRow{Key=names[0],Before="10",After="20",Result="giữ chữ"}};},(opt,names)=>1)){
            f.ShowInTaskbar=false;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();Check(!Named<Button>(f,"DensityApply").Enabled,"Apply is disabled before preview");
            Named<Button>(f,"DensityPreview").PerformClick();Application.DoEvents();Check(previews==1&&Named<Button>(f,"DensityApply").Enabled,"Preview enables apply for valid selected rows");
            Named<NumericUpDown>(f,"DensityMultiplier").Value=3;Check(!Named<Button>(f,"DensityApply").Enabled,"Changing an option invalidates preview");
            f.Size=f.MinimumSize;Application.DoEvents();foreach(var name in new[]{"DensityMode","DensityMultiplier","DensityPaper","DensityDenominator","DensityRows","DensityPreview","DensityApply"}){var c=All(f).Single(x=>x.Name==name);Check(c.Parent.ClientRectangle.Contains(c.Bounds),name+" fits at minimum size");}
            f.Size=new Size(1350,800);using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","evidence","ui-density-2009.png"));}f.Close();}
        Console.WriteLine("RESULT: PASS; pure calculations and standalone UI, native DWG unverified.");return 0;
    }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
