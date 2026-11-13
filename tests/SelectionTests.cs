using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class SelectionTests
{
    static List<string> log=new List<string>();static string evidence;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);log.Add("PASS: "+message);Console.WriteLine(log.Last());}
    static IEnumerable<Control> All(Control p){foreach(Control c in p.Controls){yield return c;foreach(var d in All(c))yield return d;}}
    static T Find<T>(Control p,string name)where T:Control{return All(p).OfType<T>().Single(c=>c.Name==name);}
    static void Capture(Form f,string name){Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(evidence,name+".png"));}}
    static void ClickHeader(DataGridView g,string col){typeof(DataGridView).GetMethod("OnColumnHeaderMouseClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,new object[]{new DataGridViewCellMouseEventArgs(g.Columns[col].Index,-1,0,0,new MouseEventArgs(MouseButtons.Left,1,0,0,0))});}
    static LabelCatalog Catalog(){var c=new LabelCatalog{Fingerprint="selection-fixture"};c.Members.AddRange(new[]{
        new LabelMember{Handle="1",Layer="Mr.T_A_RAW",Type="a",Text="D2.Oxy",Scale=10},
        new LabelMember{Handle="2",Layer="Mr.T_B_RAW",Type="b",Text="D12.Oxy",Scale=2},
        new LabelMember{Handle="3",Layer="OTHER-OLD",Type="c",Text="Oxy-%%C12",Scale=.2},
        new LabelMember{Handle="4",Layer="Mr.T_C_RAW",Type="d",Text="D110.HDPE",Scale=1},
        new LabelMember{Handle="5",Layer="foreign drainage",Type="e",Text="DN110_PVC",Scale=.1}});foreach(var m in c.Members){m.Color="ACI 140";m.NativeHeight=2.5;}return c;}
    [STAThread]public static int Main(string[] args){evidence=Path.Combine(args[0],"evidence");Application.EnableVisualStyles();try{
        var c=Catalog();Check(LabelRules.Key("Oxy-%%C12",false)==LabelRules.Key("D12.Oxy",false)&&LabelRules.Canonical("CO2-φ15")=="D15.CO2"&&LabelRules.Canonical("DN110_PVC")=="D110.PVC","Foreign diameter notation is recognized without requiring layer naming rules");
        Check(LabelRules.Key("D15.O2",true)!=LabelRules.Key("D15.Oxy",true),"Different system words are not guessed to be aliases");
        Check(LabelBrowse.Rows(c,false,"","","","Label",true).Select(x=>x.Label).SequenceEqual(new[]{"D2.Oxy","D12.Oxy","D110.HDPE","D110.PVC"}),"Label sorting compares diameters numerically");
        Check(LabelBrowse.Rows(c,false,"","","","Scales",true).First().Label=="D110.PVC"&&LabelBrowse.Rows(c,false,"","","","Scales",false).First().Label=="D2.Oxy","Scale column sorts by numeric minimum rather than formatted text");
        Check(LabelBrowse.Rows(c,false,"","","","Lines",false).First().Lines==2,"Count column sorts numerically");
        var rows=LabelBrowse.Rows(c,false,""," mr.t_ ","_raw","Label",true);Check(rows.Count==3&&rows.Single(x=>x.Label=="D12.Oxy").Lines==1,"Literal case-insensitive layer filters narrow members before grouping");
        Check(LabelRules.Select(c,new LabelScope{SystemMode=true,Keys=new[]{LabelRules.Key("Oxy",true)},LayerPrefix="Mr.T_",LayerSuffix="_RAW"}).Count==2,"Selection scope intersects system identity and actual layer filters");
        using(var f=new LabelManagerForm(c,()=>c,(s,e)=>false,n=>7,new ScaleContext(),null,false,()=>new ManagerForm(new Profile{UnitTag="BASE"},"Fixture",p=>"Plan",p=>"Created",null))){
            f.Show();Capture(f,"ui-label-manager");var g=Find<DataGridView>(f,"LabelGroupsGrid");g.Rows[0].Cells["Marked"].Value=true;Find<TextBox>(f,"LabelSearch").Text="HDPE";ClickHeader(g,"Lines");Check(g.Rows.Count==1&&!Convert.ToBoolean(g.Rows[0].Cells["Marked"].Value),"Searching and sorting hide but do not retarget a checked group");
            typeof(CheckBox).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(Find<CheckBox>(f,"LabelSelectAll"),new object[]{EventArgs.Empty});Find<TextBox>(f,"LabelSearch").Clear();Check(g.Rows.Cast<DataGridViewRow>().Count(r=>Convert.ToBoolean(r.Cells["Marked"].Value))==2,"Select-visible adds visible groups while retaining hidden checks");
            Capture(f,"ui-selection-main");f.Size=f.MinimumSize;Capture(f,"ui-selection-main-minimum");
            Find<TextBox>(f,"LabelPrefixFilter").Text="Mr.T_";Check(g.Rows.Count==3&&g.Rows.Cast<DataGridViewRow>().All(r=>!Convert.ToBoolean(r.Cells["Marked"].Value)),"Changing layer scope clears checks before applying a different member set");
            g.Rows[0].Cells["Marked"].Value=true;Find<ComboBox>(f,"LabelGrouping").SelectedIndex=1;Check(g.Rows.Count==2&&g.Rows.Cast<DataGridViewRow>().All(r=>!Convert.ToBoolean(r.Cells["Marked"].Value)),"Changing exact/system grouping clears prior check keys");
            g.Rows[0].Cells["Marked"].Value=true;Find<TextBox>(f,"LabelSearch").Text="ZZZ";Find<Button>(f,"LabelClearTicks").PerformClick();Check(!Find<Button>(f,"PickCheckedLabels").Enabled,"Clear-all removes hidden checks too");f.Close();
        }
        using(var f=new LabelManagerForm(c,()=>c,(s,e)=>false,n=>7,new ScaleContext())){f.Show();var g=Find<DataGridView>(f,"LabelGroupsGrid");g.Rows[0].Cells["Marked"].Value=true;Find<TextBox>(f,"LabelSearch").Text="HDPE";g.Rows[0].Cells["Marked"].Value=true;Find<Button>(f,"PickCheckedLabels").PerformClick();Check(f.DialogResult==DialogResult.OK&&f.RequestedPickScope.Keys.Length==2&&LabelRules.Select(c,f.RequestedPickScope).Count==2,"Pick-checked returns the union including checked groups hidden by search");}
        using(var f=new LabelManagerForm(c,()=>c,(s,e)=>false,n=>7,new ScaleContext())){f.Show();Find<Button>(f,"AddFamilySizes").PerformClick();Check(f.DialogResult==DialogResult.Ignore&&f.RequestedPickScope==null,"Add-family action returns to the editor to choose a source without changing drawing attributes");}
        using(var existing=new ManagerForm(new Profile{Rows=new List<GasRow>{new GasRow{SystemCode="TEST",LabelPrefix="Test",Diameter=15,RotationMode="U"}}},"fixture",p=>"",p=>"",null)){existing.PrepareEmbedded();Check(existing.SnapshotDraft().Rows[0].RotationMode=="U","Embedding an existing draft preserves its chosen native text orientation");}
        int inspect=0,writes=0;using(var f=new LabelManagerForm(new LabelCatalog(),()=>new LabelCatalog(),(s,e)=>false,n=>7,new ScaleContext(),null,false,()=>new ManagerForm(new Profile{UnitTag="BASE"},"UI fixture only",p=>{inspect++;return "Tạo mới: "+p.Rows.Count;},p=>{writes++;return "Created";},null))){
            f.Show();using(var timer=new Timer{Interval=100}){timer.Tick+=delegate{var n=Application.OpenForms.Cast<Form>().OfType<NewSystemForm>().FirstOrDefault();if(n==null)return;timer.Stop();Find<TextBox>(n,"SystemCode").Text="HDPE";Find<TextBox>(n,"LabelPrefix").Text="HDPE";Find<CheckBox>(n,"CreateMany").Checked=true;Find<TextBox>(n,"Diameters").Text="90,110";Find<Button>(n,"CreateSystem").PerformClick();};timer.Start();Find<Button>(f,"LabelLibrary").PerformClick();}
            var child=All(f).OfType<ManagerForm>().Single();Check(!child.TopLevel&&Find<TabControl>(f,"MainWorkspaceTabs").TabPages.Count==2&&f.CreationDraft.Rows.Count==2&&f.CreationDraft.Rows.All(r=>r.RotationMode=="R"),"Main manager hosts an empty-library creation workspace with batch sizes and native R0 defaults");
            Check(writes==0&&!Find<Button>(f,"CreateMain").Enabled,"New rows are drafts until inspection and explicit create");Find<Button>(f,"InspectMain").PerformClick();Check(inspect==1&&Find<Button>(f,"CreateMain").Enabled,"Main inspection enables the main create command");Find<NumericUpDown>(f,"GeometryFactor").Value=2;Check(!Find<Button>(f,"CreateMain").Enabled,"Editing the draft invalidates reviewed creation state");Find<Button>(f,"InspectMain").PerformClick();Find<Button>(f,"CreateMain").PerformClick();Check(writes==1&&!Find<Button>(f,"CreateMain").Enabled,"Main create uses the reviewed draft exactly once and resets readiness");
            Capture(f,"ui-embedded-creation");f.Size=f.MinimumSize;Capture(f,"ui-embedded-creation-minimum");var grid=Find<DataGridView>(f,"MainGrid");Check(grid.GetCellDisplayRectangle(grid.Columns["DeleteRow"].Index,0,true).Width>=55,"Embedded creation keeps row edit/delete actions visible at minimum width");Check(f.CreationDraft.GeometryFactor==2&&f.CreationDraft.Rows.Count==2,"Draft snapshot retains current form values without writing profile files");f.Close();}
        log.Add("RESULT: PASS; core and standalone WinForms only. Native selection remains a separate CAD test.");return 0;
    }catch(Exception ex){log.Add("FAIL: "+ex);Console.WriteLine(ex);return 1;}finally{File.WriteAllLines(Path.Combine(evidence,"selection-tests.txt"),log);}}
}
