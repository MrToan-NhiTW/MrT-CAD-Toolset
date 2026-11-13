using System;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Plugin;
class BlockLayerUiTests
{
    [STAThread]static void Main()
    {
        var choices=new[]{new LayerChoice{Name="A_NOTE",Info="(12)",Chosen=true},new LayerChoice{Name="B_PIPE",Info="(2)",Chosen=false}};
        using(var f=new LayerMultiDialog("Kiểm tra",choices)){f.StartPosition=FormStartPosition.Manual;f.Left=-2000;f.ShowInTaskbar=false;f.Show();var search=f.Controls.Find("unused",true);var box=Find<TextBox>(f);var list=Find<CheckedListBox>(f);box.Text="B_";list.SetItemChecked(0,true);box.Text="";if(f.SelectedNames.Length!=2||!f.SelectedNames.Contains("A_NOTE")||!f.SelectedNames.Contains("B_PIPE"))throw new Exception("Layer selection lost across filtering");f.Close();}
        using(var f=new BlockTargetDialog(new[]{"A","B"})){f.StartPosition=FormStartPosition.Manual;f.Left=-2000;f.ShowInTaskbar=false;f.Show();var combo=Find<ComboBox>(f);combo.Text="NEW_LAYER";if(f.TargetName!="NEW_LAYER"||f.IncludeOutside||f.IncludeParent)throw new Exception("Unsafe default scope");f.Close();}
        Console.WriteLine("PASS: Layer selection survives filtering; block target defaults exclude parent and outside objects.");
    }
    static T Find<T>(Control c)where T:Control{foreach(Control x in c.Controls){var found=x as T;if(found!=null)return found;found=Find<T>(x);if(found!=null)return found;}return null;}
}
