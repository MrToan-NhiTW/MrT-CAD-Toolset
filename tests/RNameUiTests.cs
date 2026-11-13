using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
class RNameUiTests
{
    static System.Collections.Generic.IEnumerable<Control> All(Control c){foreach(Control x in c.Controls){yield return x;foreach(var y in All(x))yield return y;}}
    [STAThread]static void Main(){Application.EnableVisualStyles();var rows=new[]{new RNameReviewRow{Use=true,Ready=true,Key="Mr.T_OXY_D12_RAW",Label="D12.Oxy",StandardLayer="Mr.T_OXY_D12_RAW",Target="Mr.T_OXY_D12_RAW",State="Khác thông số"},new RNameReviewRow{Ready=false,Label="Khác",State="Chưa xác định"}};using(var form=new RNameReviewForm(rows,keys=>false)){form.ShowInTaskbar=false;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();var grid=All(form).OfType<DataGridView>().Single(x=>x.Name=="RNameGrid");if(grid.Rows.Count!=2||!rows[0].Use||rows[1].Use)throw new Exception("Ready and blocked families are not distinguished.");form.Size=form.MinimumSize;Application.DoEvents();foreach(string name in new[]{"RNameGrid","RNameDetail","RNameApply","RNameClose"}){var c=All(form).Single(x=>x.Name==name);if(!c.Parent.ClientRectangle.Contains(c.Bounds))throw new Exception(name+" does not fit.");}form.Close();}Console.WriteLine("PASS: R naming review displays ready and blocked families and fits minimum size.");}
}
