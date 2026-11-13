using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class RoomUITests
{
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
    static object Field(object obj,string name){return obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);}
    [STAThread]public static int Main(string[] args){try{
        var book=RoomWorkbook.Read(args[0]);var catalog=RoomWorkbook.Catalog(book.Sheets[0],1,1,2,0);
        using(var f=new RoomForm((r,h,m)=>true,(c,m)=>{})){
            f.GetType().GetField("book",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(f,book);var sheet=(ComboBox)Field(f,"sheet");foreach(var s in book.Sheets)sheet.Items.Add(s.Name);sheet.SelectedIndex=0;
            Check(((ComboBox)Field(f,"oldCol")).SelectedIndex==0&&((ComboBox)Field(f,"newCol")).SelectedIndex==1,"Column selectors detect Vietnamese headers");
            f.GetType().GetField("catalog",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(f,catalog);f.GetType().GetMethod("RefreshRows",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(f,null);var grid=(DataGridView)Field(f,"grid");
            Check(((ICollection)grid.DataSource).Count==2&&TypeDescriptor.GetProperties(typeof(RoomEntry))["OldName"]!=null,"Catalog binds rows through accessible properties");
            var search=(TextBox)Field(f,"search");search.Text="001";Check(((ICollection)grid.DataSource).Count==1,"Quick search filters room IDs");
        }
        using(var review=new RoomReviewForm(new System.Collections.Generic.List<RoomReviewRow>{new RoomReviewRow{Status="Sẵn sàng"}},"Kiểm tra")){Check(review.AcceptButton!=null,"Preview exposes apply action");}
        return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
