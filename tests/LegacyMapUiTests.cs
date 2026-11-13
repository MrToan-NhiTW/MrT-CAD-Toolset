using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class LegacyMapUiTests
{
    static IEnumerable<Control> All(Control c){foreach(Control v in c.Controls){yield return v;foreach(var sub in All(v))yield return sub;}}
    static T Named<T>(Control c,string name)where T:Control{return All(c).OfType<T>().Single(x=>x.Name==name);}
    static void Check(bool condition,string description){if(!condition)throw new Exception(description);Console.WriteLine("PASS: "+description);}
    [STAThread]public static int Main(string[] args)
    {
        try
        {
            var c=new LegacyMapCatalog{Fingerprint="test"};c.Layers.Add("Mr.T_VAC_D15_RAW");c.Types.Add("Mr.T_VAC_D15_RAW");c.Rows.Add(new LegacyMapRow{Source="Mr.T_R0_0123456789abcdef",Label="VAC.D15",Count=7,CurrentLayers="0",TargetLayer=LegacyMapCatalog.KeepLayer,TargetType="",State="Bỏ qua: không xác định"});c.Rows.Add(new LegacyMapRow{Source="Mr.T_R0_1111111111111111",Label="Oxy.D12",Count=2,CurrentLayers="0",TargetLayer=LegacyMapCatalog.KeepLayer,TargetType="",State="Bỏ qua: không xác định"});int manual=0,auto=0;IList<LegacyMapRow> captured=null;
            using(var f=new LegacyMapForm(c,rows=>{manual++;captured=rows;return false;},()=>{auto++;return false;}))
            {
                f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();var grid=Named<DataGridView>(f,"LegacyMapGrid");grid.CurrentCell=grid.Rows[0].Cells[1];grid.ClearSelection();grid.Rows[0].Selected=true;var layer=Named<ComboBox>(f,"LegacyTargetLayer");var type=Named<ComboBox>(f,"LegacyTargetType");layer.SelectedItem="Mr.T_VAC_D15_RAW";type.Text="Mr.T_VAC_D15_RAW";Named<CheckBox>(f,"LegacySyncLayer").Checked=true;Named<CheckBox>(f,"LegacyCopySource").Checked=true;Named<Button>(f,"LegacySetTarget").PerformClick();Application.DoEvents();var mapped=(LegacyMapRow)grid.Rows[0].DataBoundItem;
                Check(mapped.Use&&mapped.TargetLayer=="Mr.T_VAC_D15_RAW"&&mapped.TargetType=="Mr.T_VAC_D15_RAW"&&mapped.SyncLayer&&mapped.CopySourceToExisting,"Explicit layer, linetype and shared-change options attach to selected row");Check(!((LegacyMapRow)grid.Rows[1].DataBoundItem).Use,"Other rows remain unselected and unchanged");Check(manual==0&&auto==0,"Choosing destinations does not apply anything to the drawing");Named<Button>(f,"LegacyReview").PerformClick();Check(manual==1&&captured.Count(r=>r.Use)==1&&f.Visible,"Review callback receives choices and cancellation keeps dialog open");Named<Button>(f,"LegacyAutomatic").PerformClick();Check(auto==1&&f.Visible,"Legacy automatic review remains separately available");
                grid.CurrentCell=grid.Rows[1].Cells[1];grid.ClearSelection();grid.Rows[1].Selected=true;type.Text="Mr.T_OXY_D12_RAW_NEW";layer.SelectedItem=LegacyMapCatalog.KeepLayer;Named<CheckBox>(f,"LegacySyncLayer").Checked=false;Named<CheckBox>(f,"LegacyCopySource").Checked=false;Named<Button>(f,"LegacySetTarget").PerformClick();var second=(LegacyMapRow)grid.Rows[1].DataBoundItem;Check(second.Use&&second.TargetType=="Mr.T_OXY_D12_RAW_NEW"&&second.TargetLayer==LegacyMapCatalog.KeepLayer&&!second.SyncLayer&&!second.CopySourceToExisting,"User may explicitly enter a new linetype name while keeping current layer");
                f.Size=f.MinimumSize;Application.DoEvents();foreach(string name in new[]{"LegacyTargetLayer","LegacyTargetType","LegacySetTarget","LegacySyncLayer","LegacyCopySource","LegacyReview","LegacyAutomatic","LegacyCancel"}){var ctrl=All(f).Single(v=>v.Name==name);Check(ctrl.Parent.ClientRectangle.Contains(ctrl.Bounds),name+" remains visible at minimum size");}using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(System.IO.Path.Combine(args[0],"evidence","ui-legacy-mapping-minimum.png"));}f.Close();
            }
            Console.WriteLine("RESULT: PASS; UI only, native AutoCAD mapping not exercised.");return 0;
        }catch(Exception ex){Console.WriteLine("RESULT: FAIL "+ex);return 1;}
    }
}
