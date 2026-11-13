using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
class VietnameseSystemTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    static IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(var x in All(child))yield return x;}
    [STAThread] static void Main()
    {
        var row=new GasRow{Enabled=true,SystemCode="ỐNG KHÍ NÉN",LabelPrefix="Khí nén",Diameter=22,Dash=10,GapBefore=8,GapAfter=8,TextScale=2.5,OffsetX=0,OffsetY=0,RotationMode="U",ColorIndex=7};
        var p=new Profile{Rows=new List<GasRow>{row}};p.RequireValid();p.RefreshNames();
        Check(row.Text=="D22.Khí nén"&&row.LayerName=="Mr.T_ỐNG KHÍ NÉN_D22_RAW","Vietnamese code, label, spaces and Layer preserved");
        Check(row.LinetypeName.All(c=>c<128&&!Char.IsWhiteSpace(c))&&row.LinetypeName!=row.LayerName,"safe ASCII linetype name is separate and visible");
        Check(Profile.LinetypeCode("ỐNG KHÍ NÉN")=="ONG_KHI_NEN","Vietnamese name transliterates without a hash");
        var collision=row.Copy();collision.SystemCode="ONG KHI NEN";var duplicate=new Profile{Rows=new List<GasRow>{row,collision}};
        Check(duplicate.Validate().Any(x=>x.Contains("Trùng tên Linetype")),"colliding transliterations are explicitly rejected");
        Check(Profile.LinetypeCode("HDPE")=="HDPE","existing ASCII names remain unchanged");
        bool rejected=false;try{p.ExportLin();}catch(System.IO.InvalidDataException){rejected=true;}Check(rejected,"Unicode LIN export is explicitly rejected rather than corrupted");
        var fam=new FamilyRequest{Prefix="Mr.T",Code="ỐNG KHÍ NÉN",Suffix="RAW",Diameters="28"}.Rows(new FamilySource{Label="Khí nén"}).Single();
        Check(fam.Name=="Mr.T_ỐNG KHÍ NÉN_D28_RAW"&&fam.TypeName=="Mr.T_"+Profile.LinetypeCode("ỐNG KHÍ NÉN")+"_D28_RAW"&&fam.Text=="D28.Khí nén","adding family sizes preserves Vietnamese and chooses safe linetype name");
        new DefinitionEdit{Text="D22.Khí nén",Dash=10,Before=8,After=8,Height=2.5}.Validate(true);Check(true,"editing an existing label accepts Vietnamese");
        using(var form=new NewSystemForm(new Profile(),new List<GasRow>(),row))
        {
            form.StartPosition=FormStartPosition.Manual;form.Left=-2000;form.ShowInTaskbar=false;form.Show();
            All(form).OfType<TextBox>().Single(x=>x.Name=="SystemCode").Text="Ống khí nén";
            All(form).OfType<TextBox>().Single(x=>x.Name=="LabelPrefix").Text="Khí nén";
            Check(form.NewRows.Count==1&&form.NewRows[0].SystemCode=="ỐNG KHÍ NÉN"&&form.NewRows[0].Text=="D22.Khí nén","creation form keeps Vietnamese label and displays normalized code");
            var grid=All(form).OfType<DataGridView>().Single(x=>x.Name=="NewRowsPreview");
            Check(grid.Columns.Cast<DataGridViewColumn>().Any(x=>x.DataPropertyName=="TypeName"),"preview includes the separate linetype name");form.Close();
        }
        using(var form=new NewSystemForm(new Profile(),new List<GasRow>(),row))
        {
            form.StartPosition=FormStartPosition.Manual;form.Left=-2000;form.ShowInTaskbar=false;form.Show();
            All(form).OfType<TextBox>().Single(x=>x.Name=="SystemPrefix").Text="";
            All(form).OfType<ComboBox>().Single(x=>x.Name=="SystemSuffix").Text="";
            Check(form.NewRows.Count==1&&form.NewRows[0].PrefixOverride==""&&form.NewRows[0].SuffixOverride=="","prefix and suffix can be explicitly blank");
            var clean=form.NewRows[0];var profile=new Profile();Check(profile.LayerNameFor(clean)=="ỐNG KHÍ NÉN_D22"&&profile.NameFor(clean)=="ONG_KHI_NEN_D22","blank components omit separators and hashes");
            profile.LayerPrefix="OTHER";profile.UnitTag="BASE";Check(profile.LayerNameFor(clean)=="ỐNG KHÍ NÉN_D22","per-system blanks survive later changes to shared defaults");
            All(form).OfType<TextBox>().Single(x=>x.Name=="SystemPrefix").Text="Mr.T";
            All(form).OfType<ComboBox>().Single(x=>x.Name=="SystemSuffix").Text="BASE";
            Check(profile.LayerNameFor(form.NewRows[0])=="Mr.T_ỐNG KHÍ NÉN_D22_BASE","prefix and BASE apply per system");form.Close();
        }
        var first=row.Copy();first.PrefixOverride="Mr.T";first.SuffixOverride="RAW";var second=row.Copy();second.PrefixOverride="Mr.T";second.SuffixOverride="BASE";
        var pairs=new Profile{Rows=new List<GasRow>{first,second}};Check(pairs.Validate().Count==0&&pairs.NameFor(first)!=pairs.NameFor(second),"same code and size can coexist under RAW and BASE");
        string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"mrt-naming-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            row.PrefixOverride="";row.SuffixOverride="";p.Save(path);var saved=Profile.Load(path);Check(saved.PrefixFor(saved.Rows[0])==""&&saved.SuffixFor(saved.Rows[0])==""&&saved.NameFor(saved.Rows[0])=="ONG_KHI_NEN_D22","explicit blanks survive save and reload");
            row.PrefixOverride=null;row.SuffixOverride=null;p.SchemaVersion=4;p.Save(path);var old=Profile.Load(path);string expected="Mr.T_ONG_KHI_NEN_"+Profile.Hash(row.SystemCode).Substring(0,10).ToUpperInvariant()+"_D22_RAW";Check(old.NameFor(old.Rows[0])==expected,"existing 1.9.5.4 profile retains its hashed linetype name");
        }
        finally{if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
        p=new Profile{Rows=new List<GasRow>{row}};bool inspected=false,created=false;
        using(var manager=new ManagerForm(p,"test",q=>{inspected=true;return "OK";},q=>{created=true;return "OK";},null))
        {manager.StartPosition=FormStartPosition.Manual;manager.Left=-2000;manager.ShowInTaskbar=false;manager.Show();manager.MainAction("Inspect");Check(inspected&&manager.ReadyToCreate,"Vietnamese data reaches inspection without LIN export");manager.MainAction("Create");Check(created,"unchanged Vietnamese data reaches create callback");manager.Close();}
    }
}
