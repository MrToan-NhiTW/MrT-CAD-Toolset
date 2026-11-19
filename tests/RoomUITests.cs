using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class RoomUITests
{
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
    static object Field(object obj,string name){return obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);}
    static IEnumerable<Button> Buttons(Control parent)
    {
        foreach(Control child in parent.Controls)
        {
            if(child is Button)yield return (Button)child;
            foreach(var button in Buttons(child))yield return button;
        }
    }
    static IEnumerable<T> Controls<T>(Control parent) where T:Control
    {
        foreach(Control child in parent.Controls)
        {
            if(child is T)yield return (T)child;
            foreach(var inner in Controls<T>(child))yield return inner;
        }
    }
    [STAThread]public static int Main(string[] args){try{
        var book=RoomWorkbook.Read(args[0]);var catalog=RoomWorkbook.Catalog(book.Sheets[0],1,1,2,0);
        using(var f=new RoomForm((r,h,m)=>true,(c,m)=>{})){
            f.GetType().GetField("book",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(f,book);var sheet=(ComboBox)Field(f,"sheet");foreach(var s in book.Sheets)sheet.Items.Add(s.Name);sheet.SelectedIndex=0;
            Check(((ComboBox)Field(f,"oldCol")).SelectedIndex==0&&((ComboBox)Field(f,"newCol")).SelectedIndex==1,"Column selectors detect Vietnamese headers");
            Check(((ComboBox)Field(f,"oldCol")).Text.Contains("Cột A")&&((ComboBox)Field(f,"oldCol")).Text.Contains("PHÒNG KHÁM"),"Column choices show a real Excel example");
            Check(((SearchCombo)Field(f,"targetLayer")).Text==RoomCatalog.NewLayer&&((SearchCombo)Field(f,"targetStyle")).Text==RoomCatalog.DefaultStyle,"Default layer and style are explicit");
            f.GetType().GetField("catalog",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(f,catalog);f.GetType().GetMethod("RefreshRows",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(f,null);var grid=(DataGridView)Field(f,"grid");
            Check(((ICollection)grid.DataSource).Count==4&&TypeDescriptor.GetProperties(typeof(RoomEntry))["OldName"]!=null,"Catalog binds rows through accessible properties");
            var search=(TextBox)Field(f,"search");search.Text="001";Check(((ICollection)grid.DataSource).Count==1,"Quick search filters room IDs");
            search.Text="phong dieu";Check(((ICollection)grid.DataSource).Count==1,"Excel search ignores accents and case");
            search.Text="";var list=(IBindingList)grid.DataSource;
            Check(list.SupportsSorting,"Excel grid columns can be sorted");
            list.ApplySort(TypeDescriptor.GetProperties(typeof(RoomEntry))["Id"],ListSortDirection.Descending);
            Check(((RoomEntry)list[0]).Id=="PHÒNG XÉT NGHIỆM-2"&&((RoomEntry)list[list.Count-1]).Id=="001","Sorting by ID reorders rows");
        }
        using(var f=new RoomForm((r,m,s)=>true,(c,m)=>{},new[]{"0","A-WALL","Tên phòng cũ","Mr.T Tên phòng"},new[]{"Standard"},null,null))
        {
            var source=(SearchCombo)Field(f,"sourceLayer");
            Check(source.Text==RoomForm.AllLayers&&source.Choices.Count==5,"Source layer list starts with all layers");
            source.Text="ten phong cu";Check(source.Value=="Tên phòng cũ","Typing part of a layer name without accents picks that layer");
            source.Text="khong co layer nay";Check(source.Value=="Tên phòng cũ","An unknown layer name falls back to the last valid choice");
            var target=(SearchCombo)Field(f,"targetLayer");Check(target.Value==RoomCatalog.NewLayer&&target.Choices.Count(n=>n==RoomCatalog.NewLayer)==1,"New-name layer is the default target and is listed once");
            target.Text="Mr.T Phòng tầng 4";typeof(Control).GetMethod("OnLeave",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,new object[]{EventArgs.Empty});
            Check(target.Value=="Mr.T Phòng tầng 4","A target layer that does not exist yet is kept as typed (created on apply)");
            target.Choose(RoomCatalog.DefaultLayer);
            source.Text="mr.t ten phong";typeof(Control).GetMethod("OnLeave",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(source,new object[]{EventArgs.Empty});
            Check(source.Value==RoomCatalog.DefaultLayer&&target.Value==RoomCatalog.NewLayer,"Old names on the default layer move the target to a separate new-name layer");
        }
        var remembered=new RoomSettings{ExcelPath=args[0],Sheet="Tầng 2",HeaderRow=3,OldColumn=1,NewColumn=2,IdColumn=0,SourceLayer="Tên phòng cũ",ShowNew=true,
            Label=new RoomLabelStyle{Layer="Mr.T Phòng tầng 4",Style="Standard",Height=400,Width=3000,Upper=false,Frame=RoomLabelStyle.FrameSlot,Pad=.3,Radius=1,ReplaceOld=false,MaxLines=2,WidthFactor=.8,LayoutDecided=true}};
        RoomSettings saved=null;
        using(var f=new RoomForm((r,m,s)=>true,(c,m)=>{},new[]{"0","Tên phòng cũ"},new[]{"Standard"},null,null,remembered,s=>saved=s))
        {
            var grid=(DataGridView)Field(f,"grid");
            Check(((ComboBox)Field(f,"sheet")).Text=="Tầng 2"&&grid.DataSource!=null&&((ICollection)grid.DataSource).Count==1,"The last Excel file, sheet, header row and columns are reopened and loaded");
            var look=f.LabelStyle;
            Check(look.Height==400&&look.Width==3000&&look.MaxLines==2&&Math.Abs(look.WidthFactor-.8)<1e-9&&!look.Upper&&look.Frame==RoomLabelStyle.FrameSlot&&look.Layer=="Mr.T Phòng tầng 4"&&!look.ReplaceOld&&((CheckBox)Field(f,"showNew")).Checked,"Label height, width, case, frame, new layer and options are restored");
            Check(((SearchCombo)Field(f,"sourceLayer")).Value=="Tên phòng cũ","The old-name layer is restored");
            ((NumericUpDown)Field(f,"height")).Value=350;f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Left=-20000;f.Show();Application.DoEvents();f.Close();
            Check(saved!=null&&saved.Label.Height==350&&saved.Sheet=="Tầng 2"&&saved.OldColumn==1,"Choices are saved when the form closes");
        }
        var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"MrT-room-settings-"+Guid.NewGuid().ToString("N")+".json");
        try{remembered.Save(path);var back=RoomSettings.Load(path);Check(back.Label.Height==350&&back.Label.Frame==RoomLabelStyle.FrameSlot&&back.ExcelPath==args[0],"Settings survive a save and reload");}
        finally{System.IO.File.Delete(path);}
        Check(RoomSettings.Load(path).Label.Height==250,"Missing settings fall back to defaults");
        var placed=new List<RoomBlockInfo>{new RoomBlockInfo{Handle="1A",Space="Model",Id="410-1",Name="PHÒNG DUYỆT THUỐC",OldName="410",NewName="Phòng duyệt thuốc",Status=RoomCatalog.StatusSplit,Note="Tách 410: cần kiểm tra",Layer="Mr.T Tên phòng mới",Height=250,Width=2400,Color=1,Frame=RoomLabelStyle.FrameRounded,Pad=.4,Radius=.6,Mask=true,Checked=false,MaxLines=3,WidthFactor=1},
            new RoomBlockInfo{Handle="1B",Space="Model",Id="401",Name="KHO CẤP PHÁT LẺ",OldName="401",NewName="Kho cấp phát lẻ",Status=RoomCatalog.StatusRename,Note="",Layer="Mr.T Tên phòng mới",Height=250,Width=2400,Color=158,Frame=RoomLabelStyle.FrameRounded,Pad=.4,Radius=.6,Mask=true,Checked=true,MaxLines=3,WidthFactor=1}};
        using(var manager=new RoomManagerForm(placed.Select(r=>r.Copy()).ToList(),placed,"Thử"))
        {
            manager.ShowInTaskbar=false;manager.StartPosition=FormStartPosition.Manual;manager.Left=-20000;manager.Show();Application.DoEvents();
            var grid=Controls<DataGridView>(manager).Single();
            Check(grid.Rows.Count==2&&grid.Columns.Cast<DataGridViewColumn>().Any(c=>c.HeaderText=="Padding (× Text height)")&&grid.Columns.Cast<DataGridViewColumn>().All(c=>c.SortMode==DataGridViewColumnSortMode.Automatic),"Manager lists placed rooms with sortable columns in Text height terms");
            var filter=Controls<ComboBox>(manager).First(c=>c.Items.Contains("Chưa Checked"));filter.SelectedItem="Chưa Checked";Check(grid.Rows.Count==1,"Manager filter shows rooms not yet Checked");filter.SelectedIndex=0;
            grid.ClearSelection();foreach(DataGridViewRow r in grid.Rows)r.Selected=true;
            ((NumericUpDown)Field(manager,"height")).Value=300;((ComboBox)Field(manager,"frame")).SelectedItem=RoomLabelStyle.FrameSlot;((ComboBox)Field(manager,"mask")).SelectedItem="No";
            ((NumericUpDown)Field(manager,"lines")).Value=2;((NumericUpDown)Field(manager,"factor")).Value=.85M;((ComboBox)Field(manager,"check")).SelectedItem="Yes";
            Buttons(manager).Single(b=>b.Text.StartsWith("Áp cho dòng")).PerformClick();
            Check(manager.Rows.All(r=>r.Changed&&r.Height==300&&r.Frame==RoomLabelStyle.FrameSlot&&!r.Mask&&r.Width==2400&&r.MaxLines==2&&Math.Abs(r.WidthFactor-.85)<1e-9&&r.Checked),"Bulk apply sets Text height, Max lines, Width factor, Frame, Wipeout and Checked; Fixed width kept");
            Check(grid.Columns.Cast<DataGridViewColumn>().Any(c=>c.HeaderText=="Checked")&&!grid.Columns.Cast<DataGridViewColumn>().Any(c=>c.HeaderText=="Màu"),"Checked replaces the raw colour column");
            var xlsx=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"MrT-room-manager-"+Guid.NewGuid().ToString("N")+".xlsx");
            try
            {
                RoomManagerFile.Export(xlsx,placed);
                var sheet=RoomWorkbook.Read(xlsx).Sheets[0];
                Check(sheet.Cell(1,1).Text=="Handle"&&sheet.Cell(2,4).Text=="PHÒNG DUYỆT THUỐC"&&sheet.Rows.Count==3,"Export writes a readable current-state sheet");
                var back=placed.Select(r=>r.Copy()).ToList();back[1].Name="X";
                var problems=new List<string>();
                Check(RoomManagerFile.Import(xlsx,back,problems)==1&&back[1].Name=="KHO CẤP PHÁT LẺ"&&back[1].Changed&&problems.Count==0,"Import matches rows by handle and marks what differs");
            }
            finally{System.IO.File.Delete(xlsx);}
            manager.DialogResult=DialogResult.Cancel;
        }
        var choices=new RoomReviewChoices();int rebuilt=0;
        var firstRows=new List<RoomReviewRow>{new RoomReviewRow{Handle="1/2",Id="510",Current="510",NewName="610",Status="Chữ trong block dùng chung — bật «Tạo nhãn riêng» để áp dụng"},
            new RoomReviewRow{Handle="3",Id="511",Current="511",Status="Thiếu ánh xạ"}};
        Func<RoomReviewChoices,RoomReviewPage> rebuild=c=>{rebuilt++;return new RoomReviewPage{Summary="Lần "+rebuilt,Rows=new List<RoomReviewRow>{
            new RoomReviewRow{Handle="1/2",Id="510",Current="510",NewName="610",After=c.AllowRepeats?"610":"",Ready=c.AllowRepeats,Action=c.AllowRepeats?RoomNameMatcher.ActionLabel:"—",Status=c.AllowRepeats?"Sẵn sàng":"Chờ"},
            new RoomReviewRow{Handle="3",Id="511",Current="511",Status="Thiếu ánh xạ"}}};};
        choices.AllowRepeats=false;
        using(var review=new RoomReviewForm(firstRows,"Rà soát",choices,rebuild))
        {
            review.ShowInTaskbar=false;review.StartPosition=FormStartPosition.Manual;review.Left=-20000;review.Show();Application.DoEvents();
            var ok=(Button)review.AcceptButton;Check(!ok.Enabled,"Nothing applies while the block label option is off");
            var create=Controls<CheckBox>(review).Single(c=>c.Text.StartsWith("Áp dụng mọi vị trí"));create.Checked=true;Application.DoEvents();
            Check(rebuilt==1&&ok.Enabled&&ok.Text.Contains("1 dòng")&&review.Choices.AllowRepeats,"Turning on apply-all re-matches and enables the apply button with a count");
            var hide=Controls<CheckBox>(review).Single(c=>c.Text.StartsWith("Tắt layer"));Check(!hide.Enabled,"Turning the old layer off is only offered when it is safe");
            var grid=Controls<DataGridView>(review).Single();var query=Controls<TextBox>(review).First();
            query.Text="tao nhan";Check(grid.Rows.Count==1,"Review search matches the action column without accents");
            query.Text="";var filter=Controls<ComboBox>(review).Single();filter.SelectedItem="Cần xử lý";Check(grid.Rows.Count==1&&((RoomReviewRow)grid.Rows[0].DataBoundItem).Id=="511","Review filter shows rows that still need work");
            Check(grid.Columns.Cast<DataGridViewColumn>().Any(c=>c.HeaderText=="Hành động")&&grid.Columns.Cast<DataGridViewColumn>().All(c=>c.SortMode==DataGridViewColumnSortMode.Automatic),"Review shows the planned action and every column sorts");
        }
        using(var review=new RoomReviewForm(new System.Collections.Generic.List<RoomReviewRow>{new RoomReviewRow{Status="Sẵn sàng"}},"Kiểm tra")){Check(review.AcceptButton!=null,"Preview exposes apply action");}
        var missing=new RoomReviewRow{Id="001",OldName="Phòng A",CanPlace=true,Status="Chưa tìm thấy"};
        using(var review=new RoomReviewForm(new List<RoomReviewRow>{missing},"Rà soát",true))
        {
            review.ShowInTaskbar=false;review.StartPosition=FormStartPosition.Manual;review.Left=-20000;review.Show();Application.DoEvents();
            Check(!((Button)review.AcceptButton).Enabled,"Batch is disabled for unmatched rooms");
            var point=Buttons(review).Single(b=>b.Text.StartsWith("Chọn điểm"));Check(point.Enabled,"Manual placement remains available without a drawing match");
            point.PerformClick();Check(review.SelectedRoom==missing&&review.DialogResult==DialogResult.Retry,"Manual placement returns the chosen Excel ID");
        }
        return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
