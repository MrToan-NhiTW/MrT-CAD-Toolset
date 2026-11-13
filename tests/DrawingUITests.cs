using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class DrawingUITests
{
    static readonly List<string> log=new List<string>();static string evidence;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);log.Add("PASS: "+message);Console.WriteLine(log.Last());}
    static IEnumerable<Control> All(Control p){foreach(Control c in p.Controls){yield return c;foreach(var child in All(c))yield return child;}}
    static T Named<T>(Control p,string name)where T:Control{return All(p).OfType<T>().Single(c=>c.Name==name);}
    static void Capture(Form f,string name){Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(evidence,name+".png"));}}
    static void Fit(Control c){var p=c.Parent;Check(p.ClientRectangle.Contains(c.Bounds),c.Name+" lies inside its visible parent");}
    static List<DrawingLayerInfo> Rows(){return new List<DrawingLayerInfo>{new DrawingLayerInfo{LayerName="Mr.T_OXY_D15_RAW",LinetypeName="Mr.T_OXY_D15_RAW",Text="D15.Oxy",Description="Ống oxy theo DWG đã sửa",ColorText="ACI 3",Aci=3,Font="MrT_TX_current • bahnschrift.ttf • Bold=True",State="Sẵn sàng",Managed=true,Writable=true,Editable=true,UsedCount=25,OverrideCount=3,SharedLayers=1,AffectedCount=22,Dash=45,Before=11,After=11,Height=3,X=-8,Y=-1,Fingerprint="actual-1"},new DrawingLayerInfo{LayerName="Mr.T_VAC_D22_RAW",LinetypeName="Mr.T_VAC_D22_RAW",Text="D22.VAC",State="Layer khóa",ColorText="ACI 1",Managed=true,Writable=false,UsedCount=4},new DrawingLayerInfo{LayerName="OldPipe_HDPE110_EST",LinetypeName="CustomPipe",Text="D110.HDPE",Description="Dự kiến",ColorText="RGB 10,20,30",State="Sẵn sàng",Writable=true,UsedCount=2},new DrawingLayerInfo{LayerName="Mr.T_PV_private",Text="D15.Oxy",Managed=true,Writable=true,UsedCount=4}};}
    [STAThread]public static int Main(string[] args)
    {
        evidence=Path.Combine(args[0],"evidence");Application.EnableVisualStyles();
        try
        {
            var rows=Rows();int reads=0,writes=0;
            using(var f=new DrawingManagerForm(rows,()=>{reads++;return rows;},(r,e)=>{writes++;}))
            {
                f.Show();Application.DoEvents();var grid=Named<DataGridView>(f,"DrawingGrid");
                Check(grid.Rows.Count==2&&!grid.ReadOnly&&grid.Columns["Text"].ReadOnly,"Main manager starts from actual managed DWG rows with readonly cells");
                Check(grid.Rows[0].Cells["Text"].Value.ToString()=="D15.Oxy"&&rows[0].Dash==45,"Actual labels and measurements replace template defaults");
                Named<TextBox>(f,"DrawingQuery").Text="oxy15";Check(grid.Rows.Count==1,"Immediate keyboard search finds compact pipe label");
                Named<TextBox>(f,"DrawingQuery").Clear();Named<CheckBox>(f,"ManagedOnly").Checked=false;Check(grid.Rows.Count==3,"Widening layer scope still excludes all private samples");
                Named<TextBox>(f,"DrawingPrefix").Text="old";Named<TextBox>(f,"DrawingSuffix").Text="_EST";Check(grid.Rows.Count==1&&grid.Rows[0].Cells["Text"].Value.ToString()=="D110.HDPE","Prefix and suffix load a custom current pipe group");
                Named<TextBox>(f,"DrawingPrefix").Clear();Named<TextBox>(f,"DrawingSuffix").Clear();Named<CheckBox>(f,"ManagedOnly").Checked=true;
                Named<Button>(f,"ReloadDrawing").PerformClick();Check(reads==1&&writes==0,"Reload reads DWG without invoking any write callback");Capture(f,"ui-drawing-manager");
                f.Size=f.MinimumSize;Application.DoEvents();foreach(string n in new[]{"ReloadDrawing","EditCurrent","DeleteSelected","QuickAssign","SyncScale","FixDirection","OpenLibrary"})Fit(Named<Button>(f,n));var groups=All(f).OfType<RibbonGroup>().ToList();Capture(f,"ui-drawing-manager-minimum");Check(groups.Count==3&&All(f).OfType<Label>().Where(c=>c.Name.EndsWith("Caption")).All(c=>groups.Any(g=>g.RectangleToScreen(g.ClientRectangle).Contains(c.RectangleToScreen(c.ClientRectangle)))),"All group captions remain visible at minimum width");Capture(f,"ui-drawing-manager-minimum");
                Named<Button>(f,"QuickAssign").PerformClick();Check(f.DialogResult==DialogResult.Retry&&writes==0,"Quick assign leaves definition editing without a write");
            }
            using(var f=new DrawingManagerForm(rows,()=>rows,(r,e)=>writes++)){f.Show();Named<Button>(f,"OpenLibrary").PerformClick();Check(f.DialogResult==DialogResult.Abort&&writes==0,"Library creation is a separate explicit route");}
            using(var f=new PipePickerForm(rows,0))
            {
                f.Show();Application.DoEvents();Check(!f.ColorByLayer&&f.KeepScale&&!Named<NumericUpDown>(f,"AssignEntityScale").Enabled,"Catalog assignment preserves entity color and scale unless explicitly changed");
                Check(!All(f).Any(c=>c.Name=="UsePickedSource"||c.Name=="PickPipeSource")&&Named<Button>(f,"AssignPipe").Enabled,"Catalog assignment does not require an old Dung checkbox or picked source");
                Named<TextBox>(f,"DrawingQuery").Text="vac";Check(!Named<Button>(f,"AssignPipe").Enabled,"Locked target cannot be assigned");
                Named<TextBox>(f,"DrawingQuery").Text="nothing";Check(!Named<Button>(f,"AssignPipe").Enabled,"Empty search cannot assign a stale selection");
                Named<TextBox>(f,"DrawingQuery").Text="oxy 15";Named<CheckBox>(f,"KeepEntityScale").Checked=false;Named<NumericUpDown>(f,"AssignEntityScale").Value=2;Check(f.EntityScale==2&&Named<NumericUpDown>(f,"AssignEntityScale").Enabled,"Entity scale changes require an explicit option");
                Named<CheckBox>(f,"KeepEntityScale").Checked=true;Capture(f,"ui-pipe-picker");f.Size=f.MinimumSize;Application.DoEvents();Fit(Named<Button>(f,"AssignPipe"));Capture(f,"ui-pipe-picker-minimum");
                Named<Button>(f,"AssignPipe").PerformClick();Check(f.DialogResult==DialogResult.OK&&f.Selected==rows[0],"Search assignment returns the chosen current definition");
            }
            using(var f=new PipePickerForm(rows,12)){f.Show();Check(Named<Button>(f,"AssignPipe").Text.Contains("12"),"Preselected target count remains visible without a source-pipe mode");Named<Button>(f,"CancelAssignment").PerformClick();Check(f.DialogResult==DialogResult.Cancel,"Cancel returns without assignment");}
            using(var f=new CurrentDefinitionForm(rows[0]))
            {
                f.Show();Application.DoEvents();Check(Named<TextBox>(f,"ActualText").Text==rows[0].Text&&Named<TextBox>(f,"ActualDescription").Text==rows[0].Description,"Definition editor starts with actual DWG text and description");
                Capture(f,"ui-current-definition");Named<TextBox>(f,"ActualText").Text="D15.Oxy_EDIT";f.Close();Check(rows[0].Text=="D15.Oxy"&&writes==0,"Closing editor cancels local edits and never touches source rows");
            }
            using(var f=new CurrentDefinitionForm(rows[0])){f.Show();Named<Button>(f,"ReviewDefinition").PerformClick();Check(f.DialogResult==DialogResult.OK&&f.Value.Dash==45&&!f.Value.AutoFit&&!f.Value.ChangeColor,"Review returns actual settings with explicit color preservation");}
            log.Add("Standalone WinForms verification only. No AutoCAD entities or native prompts exercised.");return 0;
        }
        catch(Exception ex){log.Add("FAIL: "+ex);Console.WriteLine(ex);return 1;}
        finally{File.WriteAllLines(Path.Combine(evidence,"drawing-ui-tests.txt"),log);}
    }
}
