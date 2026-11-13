using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;

public static class UIPreview
{
    private static int passed;
    private static IEnumerable<Control> All(Control parent){foreach(Control c in parent.Controls){yield return c;foreach(var child in All(c))yield return child;}}
    private static T Named<T>(Control parent,string name)where T:Control{return All(parent).OfType<T>().Single(x=>x.Name==name);}
    private static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS: "+name);}
    private static void Click(CheckBox checkbox){typeof(CheckBox).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(checkbox,new object[]{EventArgs.Empty});Application.DoEvents();}
    private static void RowAction(DataGridView grid,int row,string name){grid.GetType().GetMethod("OnCellContentClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(grid,new object[]{new DataGridViewCellEventArgs(grid.Columns[name].Index,row)});}
    private static void Capture(Form f,string path){Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(path);}}
    private static RibbonGroup Group(Control c){for(var parent=c.Parent;parent!=null;parent=parent.Parent)if(parent is RibbonGroup)return (RibbonGroup)parent;return null;}
    private static void Dialog<T>(Action open,Action<T> use)where T:Form
    {
        bool seen=false;Exception error=null;using(var timer=new Timer{Interval=50}){timer.Tick+=delegate{var f=Application.OpenForms.OfType<T>().FirstOrDefault();if(f==null)return;seen=true;timer.Stop();try{use(f);}catch(Exception ex){error=ex;f.Close();}};timer.Start();open();timer.Stop();}if(error!=null)throw error;Check(seen,"Dialog opened: "+typeof(T).Name);
    }
    [STAThread]
    public static int Main(string[] args)
    {
        try{return Run(args);}catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
    private static int Run(string[] args)
    {
        Application.EnableVisualStyles();string root=args[0],evidence=Path.Combine(root,"evidence","legacy-ui");Directory.CreateDirectory(evidence);var p=Profile.Load(Path.Combine(root,"tests","legacy-medgas-fixture.json"));int inspected=0,synced=0,colorCalls=0,unexpected=0,samples=0;
        using(var f=new ManagerForm(p,"Giao diện thử độc lập — chưa chạy trong AutoCAD",q=>{inspected++;return "Kế hoạch hợp lệ: "+q.Selected.Count+" dòng";},q=>{synced++;return "Đã áp dụng thử.";},q=>{samples=q.Selected.Count;},q=>"Đã gọi đo font thử.",i=>{colorCalls++;return 6;}))
        {
            f.ErrorHandler=ex=>{unexpected++;throw new Exception("Unexpected UI error",ex);};f.Show();Application.DoEvents();var grid=Named<DataGridView>(f,"MainGrid");var useAll=Named<CheckBox>(f,"UseAll");var inspect=Named<Button>(f,"Inspect");var apply=Named<Button>(f,"ApplyDrawing");var report=Named<TextBox>(f,"Report");
            Check(!All(f).OfType<MenuStrip>().Any(),"Main screen has no import/edit menus");
            var groups=All(f).OfType<RibbonGroup>().ToList();Check(groups.Count==3&&groups.Any(g=>g.Name=="ImportExportGroup")&&groups.Any(g=>g.Name=="EditGroup")&&groups.Any(g=>g.Name=="ApplyGroup"),"Three framed Ribbon groups are always visible");
            Check(new[]{"OpenProfile","SaveProfile","ImportLin","ExportLin"}.All(n=>Group(Named<Button>(f,n)).Name=="ImportExportGroup"),"All file operations are in the import/export group");
            Check(new[]{"AddSystem","GroupColor","FitText","UndoDelete"}.All(n=>Group(Named<Button>(f,n)).Name=="EditGroup")&&new[]{"Inspect","ApplyDrawing","DrawSamples"}.All(n=>Group(Named<Button>(f,n)).Name=="ApplyGroup"),"Editing and drawing commands are placed in their own groups");
            var ribbonButtons=All(f).OfType<RibbonButton>().ToList();Check(ribbonButtons.Count==11&&ribbonButtons.All(b=>!String.IsNullOrEmpty(b.IconKey)&&!String.IsNullOrWhiteSpace(b.Text)),"Every Ribbon action has both a code-drawn icon and a text label");
            var ribbonBounds=Named<TableLayoutPanel>(f,"MainRibbon").RectangleToScreen(Named<TableLayoutPanel>(f,"MainRibbon").ClientRectangle);var captions=All(f).OfType<Label>().Where(c=>c.Name.EndsWith("Caption")).ToList();
            Check(captions.Count==3&&captions.All(c=>c.Height>=20&&ribbonBounds.Contains(c.RectangleToScreen(c.ClientRectangle))),"All three group titles are visible inside the Ribbon viewport");
            Check(ribbonButtons.All(b=>b.Height>=65&&ribbonBounds.Contains(b.RectangleToScreen(b.ClientRectangle))),"Icon buttons stay inside their group without clipping the bottom labels");
            var hints=(ToolTip)typeof(ManagerForm).GetField("tips",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f);
            Check(hints.InitialDelay<=600&&hints.ShowAlways&&ribbonButtons.All(b=>!String.IsNullOrWhiteSpace(hints.GetToolTip(b))),"All Ribbon actions have hover instructions with a short delay");
            Check(hints.GetToolTip(apply.Parent).Contains("Đang khóa")&&hints.GetToolTip(Named<Button>(f,"DrawSamples")).Contains("Có ghi"),"Disabled Apply host explains prerequisites; sample hint discloses DWG changes");
            Check(new[]{"Units","GeometryFactor","UnitTag","LayerPrefix","DrawingFont","PaddingRatio","FontBold","AutoFit","TextDirection"}.All(n=>All(f).Where(c=>c.Name==n).Any(c=>!String.IsNullOrWhiteSpace(hints.GetToolTip(c)))),"Settings have explanations for their units and effects");
            Check(new[]{"OpenProfile","SaveProfile","ImportLin","ExportLin","AddSystem","GroupColor","FitText"}.All(n=>All(f).OfType<Button>().Any(x=>x.Name==n)),"File, creation, color and fitting actions are directly on the main screen");
            Check(useAll.CheckState==CheckState.Checked,"Header checkbox reflects all enabled defaults");
            Check(grid.Columns.Contains("EditRow")&&grid.Columns.Contains("DeleteRow")&&grid.Columns.Contains("AciColor"),"Every main row has Edit, Delete and color actions");
            Check(grid.Columns["Text"].ReadOnly&&grid.Columns["DescriptionText"].ReadOnly,"Main text columns are read-only; editing uses the row dialog");
            Check(!apply.Enabled&&apply.BackColor.R>180&&apply.BackColor.B>180,"Unavailable Apply button is visibly grey");
            Click(useAll);Check(p.Rows.All(r=>!r.Enabled)&&useAll.CheckState==CheckState.Unchecked,"Header click clears all Dung checks without deleting rows");
            Check(hints.GetToolTip(apply.Parent).Contains("chưa tích dòng Dùng"),"Hover explanation updates when selection is empty");
            inspect.PerformClick();Check(inspected==0&&!apply.Enabled&&report.Text.Contains("Tích")&&!report.Text.Contains("System.")&&unexpected==0,"Empty selection produces plain guidance, not an exception trace or native call");
            Click(useAll);Check(p.Rows.All(r=>r.Enabled),"Second header click selects every row");
            grid.Rows[0].Cells["Enabled"].Value=false;grid.EndEdit();Application.DoEvents();Check(useAll.CheckState==CheckState.Indeterminate,"Header represents a partial selection");Click(useAll);Check(p.Rows.All(r=>r.Enabled),"Clicking a partial header selects all");
            inspect.PerformClick();Check(apply.Enabled&&apply.BackColor.B>apply.BackColor.R,"Valid inspection makes Apply enabled and blue");
            Check(hints.GetToolTip(apply).Contains("kế hoạch vừa kiểm tra")&&!hints.GetToolTip(apply).Contains("Đang khóa"),"Ready Apply hint describes the actual write operation");
            apply.PerformClick();Check(synced==1&&!apply.Enabled&&apply.BackColor.R>180,"Apply runs once and returns to grey");
            inspect.PerformClick();Named<TextBox>(f,"LayerPrefix").Text="TEAM";Check(!apply.Enabled&&grid.Rows[0].Cells["LayerName"].Value.ToString().StartsWith("TEAM_"),"Editing settings invalidates the plan and refreshes names");Named<TextBox>(f,"LayerPrefix").Text="Mr.T";
            var orientation=Named<ComboBox>(f,"TextDirection");orientation.SelectedIndex=1;Check(p.Rows.All(r=>r.RotationMode=="R"),"R comparison remains available");orientation.SelectedIndex=0;Check(p.Rows.All(r=>r.RotationMode=="U"),"U default can be restored");
            int oxy=p.Rows.FindIndex(r=>r.SystemCode=="OXY"&&r.Diameter==15);
            Dialog<RowEditorForm>(()=>RowAction(grid,oxy,"EditRow"),d=>{Named<TextBox>(d,"Description").Text="Ống Oxy D15 — đã sửa";Named<TextBox>(d,"Text").Text="D15.Oxy_TEST";Capture(d,Path.Combine(evidence,"ui-row-editor.png"));Named<Button>(d,"Save").PerformClick();});
            Check(p.Rows[oxy].DescriptionText=="Ống Oxy D15 — đã sửa"&&p.Rows[oxy].Text=="D15.Oxy_TEST","Row Edit commits the clicked row, with custom text and description");
            var deleted=p.Rows[oxy];int before=p.Rows.Count;RowAction(grid,oxy,"DeleteRow");Check(p.Rows.Count==before-1&&!p.Rows.Contains(deleted)&&Named<Button>(f,"UndoDelete").Enabled,"Row Delete removes exactly the clicked row from the table");Named<Button>(f,"UndoDelete").PerformClick();Check(p.Rows.Count==before&&Object.ReferenceEquals(p.Rows[oxy],deleted),"Undo restores the deleted row and original position");
            Click(useAll);Check(p.Rows.All(r=>!r.Enabled),"Color operations can start when no rows are marked Dung");grid.CurrentCell=grid.Rows[oxy].Cells["SystemCode"];
            Dialog<ColorGroupForm>(()=>Named<Button>(f,"GroupColor").PerformClick(),d=>
            {
                Check(Named<ComboBox>(d,"ColorScope").SelectedIndex==1&&d.Targets.Count==3&&d.Targets.All(r=>r.SystemCode=="OXY"),"Color dialog defaults to all sizes of the current system");
                Named<Button>(d,"NativeColor").PerformClick();Check(colorCalls==1&&d.SelectedColor==6,"Native palette callback supplies an ACI selection");
                All(d).OfType<AciPicker>().Single().Value=3;Check(p.Rows.Where(r=>r.SystemCode=="OXY").All(r=>r.ColorIndex==7),"Choosing a bulk color does not mutate rows before confirmation");Capture(d,Path.Combine(evidence,"ui-group-color.png"));Named<Button>(d,"ApplyColor").PerformClick();
            });
            Check(p.Rows.Where(r=>r.SystemCode=="OXY").All(r=>r.ColorIndex==3)&&p.Rows.Where(r=>r.SystemCode!="OXY").All(r=>r.ColorIndex==7)&&p.Rows.All(r=>!r.Enabled),"Bulk color changes only the target system and preserves Dung flags");
            Dialog<ColorGroupForm>(()=>RowAction(grid,oxy,"AciColor"),d=>{Named<ComboBox>(d,"ColorScope").SelectedIndex=3;All(d).OfType<AciPicker>().Single().Value=2;d.Close();});Check(p.Rows.Where(r=>r.SystemCode=="OXY").All(r=>r.ColorIndex==3)&&p.Rows.Where(r=>r.SystemCode!="OXY").All(r=>r.ColorIndex==7),"Cancelling bulk color leaves every row unchanged");
            before=p.Rows.Count;
            Dialog<NewSystemForm>(()=>Named<Button>(f,"AddSystem").PerformClick(),d=>
            {
                Check(!Named<CheckBox>(d,"CreateMany").Checked&&d.NewRows.Count==0,"New system requires user-defined labels in its own dialog");
                Named<TextBox>(d,"SystemCode").Text="HDPE";Named<TextBox>(d,"LabelPrefix").Text="HDPE";Named<NumericUpDown>(d,"Diameter").Value=110;
                Check(d.NewRows.Single().Text=="D110.HDPE","Single-size preview uses diameter-first text");
                Named<CheckBox>(d,"CreateMany").Checked=true;Named<TextBox>(d,"Diameters").Text="90, 110, 160, 160";Named<TextBox>(d,"DescriptionPattern").Text="Ống {He} D{D}";All(d).OfType<AciPicker>().Single().Value=4;
                Check(d.NewRows.Count==3&&d.NewRows.Single(r=>r.Diameter==160).DescriptionText=="Ống HDPE D160","Integrated batch deduplicates sizes and expands description placeholders");
                Check(p.Rows.Count==before,"Live new-system preview does not alter the manager");Capture(d,Path.Combine(evidence,"ui-new-system.png"));Named<Button>(d,"CreateSystem").PerformClick();
            });
            Check(p.Rows.Count==before+3&&p.Rows.Where(r=>r.SystemCode=="HDPE").All(r=>r.Enabled&&r.ColorIndex==4&&r.RotationMode=="U"),"Confirmation adds the generated rows, color and orientation together");
            Check(useAll.CheckState==CheckState.Indeterminate,"Header updates after adding enabled rows to an unchecked table");
            before=p.Rows.Count;
            Dialog<NewSystemForm>(()=>Named<Button>(f,"AddSystem").PerformClick(),d=>{Named<TextBox>(d,"SystemCode").Text="CANCELLED";d.Close();});Check(p.Rows.Count==before&&!p.Rows.Any(r=>r.SystemCode=="CANCELLED"),"Cancelling a new system creates no row");
            Dialog<NewSystemForm>(()=>Named<Button>(f,"AddSystem").PerformClick(),d=>
            {
                Named<TextBox>(d,"SystemCode").Text="HDPE";Named<TextBox>(d,"LabelPrefix").Text="HDPE";Named<CheckBox>(d,"CreateMany").Checked=true;Named<TextBox>(d,"Diameters").Text="110,160";
                Check(d.NewRows.Count==0&&!Named<Button>(d,"CreateSystem").Enabled,"Existing sizes are shown but cannot be added a second time");
                Named<TextBox>(d,"Diameters").Text="200,bad";Check(d.NewRows.Count==0&&!Named<Button>(d,"CreateSystem").Enabled,"Invalid batch input prevents partial creation");
                Named<TextBox>(d,"Diameters").Text="110,160,200";Check(d.NewRows.Count==1&&d.NewRows[0].Diameter==200,"Adding sizes to an existing code skips current rows");
                Named<TextBox>(d,"SystemCode").Text="BAD|CODE";Check(!Named<Button>(d,"CreateSystem").Enabled,"Invalid system names disable creation");
                Named<TextBox>(d,"SystemCode").Text="HDPE";Named<TextBox>(d,"DescriptionPattern").Text="Ống {He} D{D}";Named<Button>(d,"CreateSystem").PerformClick();
            });
            Check(p.Rows.Count==before+1&&p.Rows.Count(r=>r.SystemCode=="HDPE"&&r.Diameter==110)==1,"Only a missing size is appended");
            using(var d=new ColorGroupForm(p.Rows,p.Rows.First()))
            {
                d.Show();var scope=Named<ComboBox>(d,"ColorScope");scope.SelectedIndex=0;Check(d.Targets.Count==1,"Bulk color also supports a single row");scope.SelectedIndex=2;Check(d.Targets.Count==p.Selected.Count,"Dung color scope targets exactly marked rows");scope.SelectedIndex=3;Check(d.Targets.Count==p.Rows.Count,"All-table color scope includes disabled rows");d.Close();
            }
            Named<Button>(f,"DrawSamples").PerformClick();Check(samples==p.Selected.Count&&samples==4,"Draw samples uses Dung selection rather than the focused row");
            inspect.PerformClick();Named<ComboBox>(f,"DrawingFont").Text="arial.ttf";Check(!apply.Enabled,"Font changes invalidate inspection");Named<ComboBox>(f,"DrawingFont").Text="bahnschrift.ttf";
            f.Size=f.MinimumSize;grid.CurrentCell=grid.Rows[0].Cells["SystemCode"];grid.FirstDisplayedScrollingRowIndex=0;grid.HorizontalScrollingOffset=0;Application.DoEvents();var editRect=grid.GetCellDisplayRectangle(grid.Columns["EditRow"].Index,0,true);var deleteRect=grid.GetCellDisplayRectangle(grid.Columns["DeleteRow"].Index,0,true);
            Check(editRect.Width>0&&deleteRect.Width>0&&deleteRect.Right<=grid.ClientSize.Width,"Row action buttons remain visible at the minimum supported window width");
            ribbonBounds=Named<TableLayoutPanel>(f,"MainRibbon").RectangleToScreen(Named<TableLayoutPanel>(f,"MainRibbon").ClientRectangle);Check(captions.All(c=>ribbonBounds.Contains(c.RectangleToScreen(c.ClientRectangle)))&&ribbonButtons.All(b=>b.Width>=60&&b.Height>=65),"Framed group titles and command sizes remain usable at minimum width");Capture(f,Path.Combine(evidence,"ui-minimum.png"));f.Close();
        }
        using(var atlas=new Bitmap(624,96))using(var g=Graphics.FromImage(atlas))
        {
            g.Clear(Color.White);string[] icons={"open","save","import","export","new","color","fit","undo","inspect","apply","samples","edit","delete"};
            using(var labelFont=new Font("Segoe UI",8F))using(var ink=new SolidBrush(Color.FromArgb(45,65,80)))using(var align=new StringFormat{Alignment=StringAlignment.Center})for(int i=0;i<icons.Length;i++){RibbonIcons.Draw(g,icons[i],new Rectangle(i*48+8,10,32,32),Color.FromArgb(38,89,131));g.DrawString(icons[i],labelFont,ink,new RectangleF(i*48,55,48,25),align);}
            atlas.Save(Path.Combine(evidence,"ribbon-icons.png"));
        }
        var fresh=Profile.Load(Path.Combine(root,"tests","legacy-medgas-fixture.json"));
        using(var f=new ManagerForm(fresh,"Xem trước giao diện — chưa xác minh trong AutoCAD",q=>"Kế hoạch hợp lệ",q=>"Đã áp dụng thử",null))
        {
            f.Show();Capture(f,Path.Combine(evidence,"ui-preview.png"));var ribbon=Named<TableLayoutPanel>(f,"MainRibbon");using(var strip=new Bitmap(ribbon.Width,ribbon.Height)){ribbon.DrawToBitmap(strip,new Rectangle(Point.Empty,strip.Size));strip.Save(Path.Combine(evidence,"ui-ribbon.png"));}Named<Button>(f,"Inspect").PerformClick();Capture(f,Path.Combine(evidence,"ui-ready.png"));All(f).OfType<TabControl>().Single().SelectedIndex=1;Capture(f,Path.Combine(evidence,"ui-parameters.png"));f.Close();
        }
        File.WriteAllText(Path.Combine(evidence,"ui-tests.txt"),"PASS: "+passed+" UI assertions. Standalone WinForms host; native AutoCAD palette and drawing runtime not exercised.\r\n");Console.WriteLine("UI regression and visual renders complete.");return 0;
    }
}
