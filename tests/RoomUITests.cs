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
            Check(((ComboBox)Field(f,"targetLayer")).Text==RoomCatalog.DefaultLayer&&((ComboBox)Field(f,"targetStyle")).Text==RoomCatalog.DefaultStyle,"Default layer and style are explicit");
            f.GetType().GetField("catalog",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(f,catalog);f.GetType().GetMethod("RefreshRows",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(f,null);var grid=(DataGridView)Field(f,"grid");
            Check(((ICollection)grid.DataSource).Count==2&&TypeDescriptor.GetProperties(typeof(RoomEntry))["OldName"]!=null,"Catalog binds rows through accessible properties");
            var search=(TextBox)Field(f,"search");search.Text="001";Check(((ICollection)grid.DataSource).Count==1,"Quick search filters room IDs");
            search.Text="phong dieu";Check(((ICollection)grid.DataSource).Count==1,"Excel search ignores accents and case");
            search.Text="";var list=(IBindingList)grid.DataSource;
            Check(list.SupportsSorting,"Excel grid columns can be sorted");
            list.ApplySort(TypeDescriptor.GetProperties(typeof(RoomEntry))["Id"],ListSortDirection.Descending);
            Check(((RoomEntry)list[0]).Id=="PHÒNG KHÁM"&&((RoomEntry)list[1]).Id=="001","Sorting by ID reorders rows");
        }
        using(var f=new RoomForm((r,h,m,l,s)=>true,(c,m)=>{},new[]{"0","A-WALL","Tên phòng cũ","Mr.T Tên phòng"},new[]{"Standard"},null,null))
        {
            var source=(SearchCombo)Field(f,"sourceLayer");
            Check(source.Text==RoomForm.AllLayers&&source.Choices.Count==5,"Source layer list starts with all layers");
            source.Text="ten phong cu";Check(source.Value=="Tên phòng cũ","Typing part of a layer name without accents picks that layer");
            source.Text="khong co layer nay";Check(source.Value=="Tên phòng cũ","An unknown layer name falls back to the last valid choice");
            var target=(SearchCombo)Field(f,"targetLayer");Check(target.Value==RoomCatalog.DefaultLayer&&target.Choices.Count(n=>n==RoomCatalog.DefaultLayer)==1,"Default target layer is listed once");
            source.Text="mr.t ten phong";typeof(Control).GetMethod("OnLeave",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(source,new object[]{EventArgs.Empty});
            Check(source.Value==RoomCatalog.DefaultLayer&&target.Value==RoomCatalog.NewLayer,"Old names on the default layer move the target to a separate new-name layer");
        }
        var choices=new RoomReviewChoices();int rebuilt=0;
        var firstRows=new List<RoomReviewRow>{new RoomReviewRow{Handle="1/2",Id="510",Current="510",NewName="610",Status="Chữ trong block dùng chung — bật «Tạo nhãn riêng» để áp dụng"},
            new RoomReviewRow{Handle="3",Id="511",Current="511",Status="Thiếu ánh xạ"}};
        Func<RoomReviewChoices,RoomReviewPage> rebuild=c=>{rebuilt++;return new RoomReviewPage{Summary="Lần "+rebuilt,Rows=new List<RoomReviewRow>{
            new RoomReviewRow{Handle="1/2",Id="510",Current="510",NewName="610",After=c.CreateLabels?"610":"",Ready=c.CreateLabels,Action=c.CreateLabels?RoomNameMatcher.ActionLabel:"—",Status=c.CreateLabels?"Sẵn sàng":"Chờ"},
            new RoomReviewRow{Handle="3",Id="511",Current="511",Status="Thiếu ánh xạ"}}};};
        choices.CreateLabels=false;
        using(var review=new RoomReviewForm(firstRows,"Rà soát",choices,rebuild))
        {
            review.ShowInTaskbar=false;review.StartPosition=FormStartPosition.Manual;review.Left=-20000;review.Show();Application.DoEvents();
            var ok=(Button)review.AcceptButton;Check(!ok.Enabled,"Nothing applies while the block label option is off");
            var create=Controls<CheckBox>(review).Single(c=>c.Text.StartsWith("Tạo nhãn riêng"));create.Checked=true;Application.DoEvents();
            Check(rebuilt==1&&ok.Enabled&&ok.Text.Contains("1 dòng")&&review.Choices.CreateLabels,"Turning on labels re-matches and enables the apply button with a count");
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
