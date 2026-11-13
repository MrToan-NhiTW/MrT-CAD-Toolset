using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class RoomUIPreview
{
    [STAThread]public static int Main(string[] args){try{
        using(var f=new RoomForm((r,h,m)=>true,(c,m)=>{})){
            var book=RoomWorkbook.Read(args[0]);var t=f.GetType();t.GetField("book",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(f,book);var combo=(ComboBox)t.GetField("sheet",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f);foreach(var sh in book.Sheets)combo.Items.Add(sh.Name);combo.SelectedIndex=0;
            var catalog=RoomWorkbook.Catalog(book.Sheets[0],1,1,2,0);t.GetField("catalog",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(f,catalog);t.GetMethod("RefreshRows",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(f,null);
            f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.ShowInTaskbar=false;f.Show();Application.DoEvents();f.PerformLayout();using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(args[1]);}f.Close();
        }return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
