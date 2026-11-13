using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class VisibilityTests
{
    static List<string> log=new List<string>();static string evidence;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);log.Add("PASS: "+text);Console.WriteLine(log.Last());}
    static IEnumerable<Control> All(Control p){foreach(Control c in p.Controls){yield return c;foreach(var d in All(c))yield return d;}}
    static T Find<T>(Control p,string name)where T:Control{return All(p).OfType<T>().Single(c=>c.Name==name);}
    static void Capture(Form f,string name){Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(evidence,name+".png"));}}
    static void Header(DataGridView g,string col){typeof(DataGridView).GetMethod("OnColumnHeaderMouseClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,new object[]{new DataGridViewCellMouseEventArgs(g.Columns[col].Index,-1,0,0,new MouseEventArgs(MouseButtons.Left,1,0,0,0))});}
    [STAThread]public static int Main(string[] args){evidence=Path.Combine(args[0],"evidence");Application.EnableVisualStyles();try{
        var rows=new List<DrawingLayerInfo>{new DrawingLayerInfo{LayerName="Mr.T_WATER_D2_BASE",LinetypeName="Pipe2",Text="D2.Water",Managed=true,UsedCount=12,OverrideCount=2,Aci=2,ColorText="ACI 2",State="Layer tắt",Assignable=true},new DrawingLayerInfo{LayerName="Mr.T_WATER_D110_BASE",LinetypeName="Pipe110",Text="D110.Water",Managed=true,UsedCount=2,OverrideCount=10,Aci=10,ColorText="ACI 10",State="Đóng băng",Assignable=true},new DrawingLayerInfo{LayerName="Mr.T_LOCKED",LinetypeName="Locked",Text="D12.Locked",Managed=true,UsedCount=100,OverrideCount=0,Aci=1,ColorText="ACI 1",State="Layer khóa"}};
        Check(DrawingSearch.Sort(rows,"UsedCount",true).Select(r=>r.UsedCount).SequenceEqual(new[]{2,12,100}),"Advanced count sorting is numeric, not lexical");
        Check(DrawingSearch.Sort(rows,"Text",true).Select(r=>r.Text).SequenceEqual(new[]{"D2.Water","D12.Locked","D110.Water"}),"Advanced label sorting compares diameter numbers");
        Check(DrawingSearch.Sort(rows,"ColorText",true).Select(r=>r.Aci).SequenceEqual(new[]{1,2,10}),"Advanced color sorting compares numbers within color names");
        using(var f=new DrawingManagerForm(rows,()=>rows,(r,e)=>{})){
            f.Show();var g=Find<DataGridView>(f,"DrawingGrid");g.Rows[0].Cells["Marked"].Value=true;string marked=((DrawingLayerInfo)g.Rows[0].DataBoundItem).LayerName;Header(g,"UsedCount");Check(((DrawingLayerInfo)g.Rows[0].DataBoundItem).UsedCount==2,"Advanced header click sorts ascending");Header(g,"UsedCount");Check(((DrawingLayerInfo)g.Rows[0].DataBoundItem).UsedCount==100&&g.Rows.Cast<DataGridViewRow>().Single(r=>((DrawingLayerInfo)r.DataBoundItem).LayerName==marked).Cells["Marked"].Value.Equals(true),"Reverse header sort retains checks on the same Layer");Find<TextBox>(f,"DrawingQuery").Text="Water";Check(g.Rows.Count==2&&((DrawingLayerInfo)g.Rows[0].DataBoundItem).UsedCount==12,"Filter retains the chosen descending order");Check(All(f).OfType<Label>().Any(l=>l.Text=="Tiền tố Layer:")&&All(f).OfType<Label>().Any(l=>l.Text=="Hậu tố Layer:"),"Advanced filter names match main Layer prefix/suffix terminology");Capture(f,"ui-advanced-sort");f.Size=f.MinimumSize;Capture(f,"ui-advanced-sort-minimum");f.Close();}
        using(var f=new PipePickerForm(rows,3)){
            f.Show();var g=Find<DataGridView>(f,"DrawingGrid");g.CurrentCell=g.Rows.Cast<DataGridViewRow>().Single(r=>((DrawingLayerInfo)r.DataBoundItem).State=="Layer tắt").Cells[0];Check(Find<Button>(f,"AssignPipe").Enabled&&f.IsolateAfterAssign,"Off target layer is assignable and whole-system isolation is selected by default");g.CurrentCell=g.Rows.Cast<DataGridViewRow>().Single(r=>((DrawingLayerInfo)r.DataBoundItem).State=="Đóng băng").Cells[0];Check(Find<Button>(f,"AssignPipe").Enabled,"Frozen target remains assignable without enabling definition edits");g.CurrentCell=g.Rows.Cast<DataGridViewRow>().Single(r=>((DrawingLayerInfo)r.DataBoundItem).State=="Layer khóa").Cells[0];Check(!Find<Button>(f,"AssignPipe").Enabled,"Locked target remains blocked");g.CurrentCell=g.Rows.Cast<DataGridViewRow>().Single(r=>((DrawingLayerInfo)r.DataBoundItem).State=="Layer tắt").Cells[0];Capture(f,"ui-assign-isolate");f.Size=f.MinimumSize;Capture(f,"ui-assign-isolate-minimum");Find<CheckBox>(f,"IsolateAfterAssign").Checked=false;Check(!f.IsolateAfterAssign,"User can opt out of automatic visibility changes");f.Close();}
        var cat=new LabelCatalog{Fingerprint="fixture"};cat.Members.Add(new LabelMember{Handle="1",Layer="Foreign",Type="Pipe",Text="D110.HDPE",Scale=1,Color="ACI 3"});using(var f=new LabelManagerForm(cat,()=>cat,(s,e)=>false,n=>3,new ScaleContext())){f.Show();Check(All(f).OfType<Label>().Any(l=>l.Text=="Tiền tố Layer:")&&All(f).OfType<Label>().Any(l=>l.Text=="Hậu tố Layer:"),"Main filter uses consistent Layer prefix/suffix captions");var g=Find<DataGridView>(f,"LabelGroupsGrid");g.Rows[0].Cells["Marked"].Value=true;Capture(f,"ui-checked-isolate");f.Size=f.MinimumSize;Capture(f,"ui-checked-isolate-minimum");Find<Button>(f,"IsolateCheckedLabels").PerformClick();Check(f.IsolateRequested&&f.RequestedPickScope.Keys.Length==1&&f.DialogResult==DialogResult.OK,"Checked isolate returns an explicit label scope and isolation intent");}
        log.Add("RESULT: PASS; standalone Core/WinForms. Native isolate/restore commands require live CAD checks.");return 0;
    }catch(Exception ex){log.Add("FAIL: "+ex);Console.WriteLine(ex);return 1;}finally{File.WriteAllLines(Path.Combine(evidence,"visibility-tests.txt"),log);}}
}
