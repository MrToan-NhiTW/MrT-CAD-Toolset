using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class BatchTests
{
    static List<string> log=new List<string>();static string evidence;static void Check(bool value,string text){if(!value)throw new Exception(text);log.Add("PASS: "+text);Console.WriteLine(log.Last());}
    static bool Reject(Action a){try{a();return false;}catch(ArgumentException){return true;}}
    static IEnumerable<Control> All(Control p){foreach(Control c in p.Controls){yield return c;foreach(var d in All(c))yield return d;}}
    static T Find<T>(Control p,string name)where T:Control{return All(p).OfType<T>().Single(c=>c.Name==name);}
    static void Capture(Form f,string name){Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(evidence,name+".png"));}}
    static void Cell(DataGridView grid,string column,int row){typeof(DataGridView).GetMethod("OnCellContentClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(grid,new object[]{new DataGridViewCellEventArgs(grid.Columns[column].Index,row)});}
    static void Click(CheckBox box){typeof(CheckBox).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(box,new object[]{EventArgs.Empty});}
    static List<DrawingLayerInfo> Rows(){return new List<DrawingLayerInfo>{new DrawingLayerInfo{LayerName="Mr.T_CN_D110_RAW",LinetypeName="Mr.T_CN_D110_RAW",Text="D110.CN",Description="Cấp nước",Aci=140,ColorText="ACI 140",Managed=true,Writable=true,Editable=true,Dash=40,Before=12,After=12,Height=3,State="Sẵn sàng",UsedCount=8},new DrawingLayerInfo{LayerName="Mr.T_TN_D160_RAW",LinetypeName="Mr.T_TN_D160_RAW",Text="D160.TN",Description="Thoát nước",Aci=30,ColorText="ACI 30",Managed=true,Writable=true,Editable=true,Dash=60,Before=14,After=14,Height=4,State="Sẵn sàng",UsedCount=5},new DrawingLayerInfo{LayerName="Mr.T_HDPE_D90_EST",LinetypeName="HDPE90",Text="D90.HDPE",Aci=7,Managed=true,Writable=true,State="Sẵn sàng"}};}
    [STAThread]public static int Main(string[] args)
    {
        evidence=Path.Combine(args[0],"evidence");Application.EnableVisualStyles();try
        {
            var rows=Rows();var change=new BatchEdit{ChangePrefix=true,Prefix="Mr.T",ChangeSuffix=true,Suffix="EST",ChangeSystem=true,System="HDPE",ChangeLabel=true,Label="HDPE"};change.Validate(2);
            Check(change.NewLayer(rows[0])=="Mr.T_HDPE_D110_EST"&&change.NewLayer(rows[1])=="Mr.T_HDPE_D160_EST","Batch renaming preserves each diameter and replaces only selected name components");
            Check(change.NewText(rows[0])=="D110.HDPE"&&change.NewText(rows[1])=="D160.HDPE","Changing the label retains the diameter of each selected row");
            Check(new BatchEdit{ChangeSuffix=true,Suffix=""}.NewLayer(rows[0])=="Mr.T_CN_D110","An explicitly empty suffix removes only the suffix");
            Check(new BatchEdit{ChangeDiameter=true,Diameter=125}.NewText(rows[0])=="D125.CN"&&new BatchEdit{ChangeDiameter=true,Diameter=50}.NewText(new DrawingLayerInfo{LayerName="MRG_CO2_D35_RAW",Text="CO2.D35"})=="D50.CO2","Diameter edits preserve the actual label and support legacy system-first text");
            var plain=new DrawingLayerInfo{LayerName="Existing custom layer",Text="D25.PPR"};Check(Reject(()=>change.NewLayer(plain))&&new BatchEdit{ExactLayer=true,LayerName="Mr.T_PPR_D25_BASE"}.NewLayer(plain)=="Mr.T_PPR_D25_BASE","Ambiguous names require explicit full-name editing instead of guessing");
            Check(LayerParts.Read(new DrawingLayerInfo{LayerName="Mr.T_CAP_NUOC_D110_BASE"}).System=="CAP_NUOC","System codes containing underscores are retained");
            var color=new BatchEdit{ChangeColor=true,Aci=201};color.Validate(3);Check(!color.ChangesPattern&&!color.Renames&&!color.ColorByLayer&&color.NewLayer(rows[0])==rows[0].LayerName&&color.NewText(rows[0])==rows[0].Text,"Color-only changes do not request naming, pattern, font or entity-color resets");
            Check(Reject(()=>new BatchEdit().Validate(1))&&Reject(()=>new BatchEdit{ChangeColor=true,Aci=256}.Validate(1))&&Reject(()=>new BatchEdit{ExactLayer=true,LayerName="New"}.Validate(2)),"Empty patches, invalid ACI and multirow exact names are rejected");
            Check(Reject(()=>BatchEdit.Name("Bad|Name"))&&Reject(()=>BatchEdit.Name("ByLayer")),"Invalid and reserved symbol names are rejected");string signature=color.Signature;color.ColorByLayer=true;Check(signature!=color.Signature,"Changing override intent invalidates the reviewed options signature");
            change.ChangeDescription=true;change.DescriptionTemplate="{System}: D{D}; {Text}";Check(change.Expand(change.DescriptionTemplate,rows[0])=="HDPE: D110; D110.CN","Description templates expand per-row values without importing sample metadata");
            int edits=0,deletes=0;using(var f=new Form{Size=new Size(1200,680)})using(var browser=new DrawingBrowser(rows,true))
            {
                f.Controls.Add(browser);f.Show();Application.DoEvents();var grid=Find<DataGridView>(f,"DrawingGrid");Check(!grid.ReadOnly&&grid.Columns["Text"].ReadOnly&&!grid.Columns["Marked"].ReadOnly,"The manager exposes editable checkboxes and explicit row actions while data edits remain reviewable");
                grid.Rows[0].Cells["Marked"].Value=true;grid.Rows[1].Cells["Marked"].Value=true;Check(browser.CheckedRows.Count==2,"Multiple marked rows form the edit/delete scope");Find<TextBox>(f,"DrawingQuery").Text="CN";Check(grid.Rows.Count==1&&browser.CheckedRows.Count==2,"Filtering keeps hidden checked rows in the explicit scope");
                Click(Find<CheckBox>(f,"DrawingSelectAll"));Check(browser.CheckedRows.Count==1&&browser.CheckedRows[0]==rows[1],"Clearing a fully checked visible filter leaves hidden checks intact");Click(Find<CheckBox>(f,"DrawingSelectAll"));Check(browser.CheckedRows.Count==2,"Selecting a visible filter adds rows without resetting hidden selections");
                Find<Button>(f,"DrawingClearSelection").PerformClick();Check(browser.CheckedRows.Count==0,"Clear-all removes both visible and hidden checks");Find<TextBox>(f,"DrawingQuery").Clear();grid.Rows[0].Cells["Marked"].Value=true;Check(Find<CheckBox>(f,"DrawingSelectAll").CheckState==CheckState.Indeterminate,"Header displays partial selection");Click(Find<CheckBox>(f,"DrawingSelectAll"));Check(browser.CheckedRows.Count==3,"Clicking a partial header selects all visible rows");
                browser.EditRequested+=row=>{if(row==rows[1])edits++;};browser.DeleteRequested+=row=>{if(row==rows[2])deletes++;};Cell(grid,"EditRow",1);Cell(grid,"DeleteRow",2);Check(edits==1&&deletes==1,"Row Edit/Delete dispatch the clicked row rather than the marked group");
                browser.Reload(rows.Take(2).ToList());Check(browser.CheckedRows.Count==2,"Reload retains surviving checks and removes checks for deleted rows");f.Close();
            }
            using(var f=new BatchEditForm(rows.Take(2).ToList(),rows,initial=>201))
            {
                f.Show();Application.DoEvents();Check(!Find<CheckBox>(f,"ChangeColor").Checked&&!Find<CheckBox>(f,"ChangeFont").Checked&&!Find<CheckBox>(f,"ChangeAutoFit").Checked&&!Find<CheckBox>(f,"BatchColorByLayer").Checked,"All destructive or appearance-changing options start unselected");Check(!Find<CheckBox>(f,"ChangeExactLayer").Enabled&&!Find<CheckBox>(f,"ChangeExactType").Enabled,"Exact symbol names are unavailable for multiple rows");
                Find<Button>(f,"BatchPalette").PerformClick();Check(Find<CheckBox>(f,"ChangeColor").Checked&&Find<NumericUpDown>(f,"BatchColor").Value==201,"Native palette callback selects bulk ACI without mutating source rows");Find<CheckBox>(f,"ChangeSuffix").Checked=true;Find<TextBox>(f,"BatchSuffix").Text="EST";Find<CheckBox>(f,"ChangeLabel").Checked=true;Find<TextBox>(f,"BatchLabel").Text="HDPE";
                var preview=Find<DataGridView>(f,"BatchNamePreview");Check(((BatchPreviewRow)preview.Rows[1].DataBoundItem).TextAfter=="D160.HDPE","Live preview shows row-specific label expansion");Check(rows[0].LayerName=="Mr.T_CN_D110_RAW"&&rows[0].Text=="D110.CN"&&rows[0].Aci==140,"Editing the dialog does not mutate DWG catalog objects");Capture(f,"ui-batch-editor");f.Size=f.MinimumSize;Capture(f,"ui-batch-editor-minimum");var scroller=Find<Panel>(f,"BatchFields");var force=Find<CheckBox>(f,"BatchForceR");scroller.ScrollControlIntoView(force);Application.DoEvents();Check(scroller.RectangleToScreen(scroller.ClientRectangle).Contains(force.RectangleToScreen(force.ClientRectangle)),"Font and R=0 section can be scrolled fully into view at minimum size");Capture(f,"ui-batch-font-options");
                Find<Button>(f,"ReviewBatch").PerformClick();Check(f.Value.ChangeColor&&f.Value.Aci==201&&f.Value.ChangeSuffix&&f.Value.ChangeLabel&&!f.Value.ChangeFont&&!f.Value.ChangeHeight&&!f.Value.ColorByLayer,"Review captures only the checked appearance/naming requests");
            }
            using(var f=new BatchReviewForm(new List<BatchPreviewRow>{new BatchPreviewRow{LayerBefore="UsedLayer",LayerAfter="[DELETE]",Blocked=true,Detail="Có đối tượng; giữ lại."}},"Không xóa hình học",false)){f.Show();Check(!Find<Button>(f,"ApplyBatch").Enabled,"Blocked deletion plans cannot be applied");Capture(f,"ui-batch-review-blocked");f.Close();}
            var sample=new List<BatchPreviewRow>{new BatchPreviewRow{LayerBefore=rows[0].LayerName,LayerAfter="Mr.T_HDPE_D110_EST",TypeBefore=rows[0].LinetypeName,TypeAfter="Mr.T_HDPE_D110_EST",TextBefore="D110.CN",TextAfter="D110.HDPE",ColorBefore="ACI 140",ColorAfter="ACI 201",BeforeState="S=3, font DWG",AfterState="S=3, giữ font",Detail="Sao nét riêng, không đổi Layer khác."}};
            using(var f=new BatchReviewForm(sample,"1 Layer, không có lỗi. Giữ hình học và Scale/Width.",true)){f.Show();Check(Find<Button>(f,"ApplyBatch").Enabled&&Find<TextBox>(f,"BatchReviewDetails").Text.Contains("ACI 140 → ACI 201"),"Review exposes before/after values and enables only a valid plan");Capture(f,"ui-batch-review");f.Close();}
            int calls=0;List<DrawingLayerInfo> scope=null;BatchEdit intent=null;
            using(var f=new DrawingManagerForm(rows,()=>rows,(row,patch)=>{},(selected,patch)=>{calls++;scope=selected;intent=patch;return false;}))
            {
                f.Show();var grid=Find<DataGridView>(f,"DrawingGrid");grid.Rows[0].Cells["Marked"].Value=true;grid.Rows[1].Cells["Marked"].Value=true;Capture(f,"ui-batch-manager");Find<Button>(f,"DeleteSelected").PerformClick();Check(calls==1&&scope.Count==2&&intent.Delete,"Toolbar delete sends exactly the marked scope to the review callback");Check(grid.Rows.Count==3&&Convert.ToBoolean(grid.Rows[0].Cells["Marked"].Value),"Cancelling the callback preserves rows and checkbox scope");Cell(grid,"DeleteRow",2);Check(calls==2&&scope.Count==1&&scope[0]==rows[2],"Quick row deletion does not expand to the other marked layers");int step=0;bool retained=false;using(var timer=new Timer{Interval=100}){timer.Tick+=delegate{var dialog=Application.OpenForms.Cast<Form>().OfType<BatchEditForm>().FirstOrDefault();if(dialog==null)return;if(step++==0){Find<CheckBox>(dialog,"ChangeColor").Checked=true;Find<NumericUpDown>(dialog,"BatchColor").Value=42;Find<Button>(dialog,"ReviewBatch").PerformClick();}else{retained=Find<CheckBox>(dialog,"ChangeColor").Checked&&Find<NumericUpDown>(dialog,"BatchColor").Value==42;timer.Stop();Find<Button>(dialog,"CancelBatch").PerformClick();}};timer.Start();Find<Button>(f,"EditCurrent").PerformClick();}Check(retained,"Returning from review retains the editor values instead of forcing re-entry");Check(calls==3&&scope.Count==2&&intent.ChangeColor&&intent.Aci==42,"Main bulk Edit roundtrip returns the checked scope and only the selected patch fields");
                f.Size=f.MinimumSize;Capture(f,"ui-batch-manager-minimum");foreach(string key in new[]{"ReloadDrawing","EditCurrent","DeleteSelected","QuickAssign","SyncScale","FixDirection","OpenLibrary"}){var button=Find<Button>(f,key);Check(button.Parent.ClientRectangle.Contains(button.Bounds),key+" is visible at the minimum manager size");}f.Close();
            }
            log.Add("RESULT: PASS; standalone core/WinForms checks only, no native DWG operations.");return 0;
        }
        catch(Exception ex){log.Add("FAIL: "+ex);Console.WriteLine(ex);return 1;}
        finally{File.WriteAllLines(Path.Combine(evidence,"batch-tests.txt"),log);}
    }
}
