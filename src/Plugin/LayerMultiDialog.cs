using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
namespace MedGas2024.Plugin
{
    internal sealed class LayerChoice { public string Name,Info;public bool Chosen; }
    internal sealed class LayerMultiDialog:Form
    {
        readonly List<LayerChoice> items;readonly CheckedListBox list=new CheckedListBox();readonly TextBox search=new TextBox();bool loading;
        public string[] SelectedNames{get{return items.Where(x=>x.Chosen).Select(x=>x.Name).ToArray();}}
        public LayerMultiDialog(string title,IEnumerable<LayerChoice> source)
        {
            items=source.ToList();Text=title;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterParent;Size=new Size(790,620);MinimumSize=new Size(650,480);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(14),RowCount=4,ColumnCount=1};root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));root.RowStyles.Add(new RowStyle(SizeType.Absolute,36));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));Controls.Add(root);
            root.Controls.Add(new Label{Text="Tìm Layer; tích nhiều dòng. Lựa chọn vẫn giữ khi lọc.",Dock=DockStyle.Fill},0,0);search.Dock=DockStyle.Fill;root.Controls.Add(search,0,1);list.Dock=DockStyle.Fill;list.CheckOnClick=true;list.HorizontalScrollbar=true;root.Controls.Add(list,0,2);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var ok=new Button{Text="Chọn",AutoSize=true,DialogResult=DialogResult.OK};var cancel=new Button{Text="Hủy",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.Add(ok);buttons.Controls.Add(cancel);root.Controls.Add(buttons,0,3);AcceptButton=ok;CancelButton=cancel;
            search.TextChanged+=(s,e)=>Filter();list.ItemCheck+=(s,e)=>{if(!loading&&e.Index>=0)((LayerChoice)list.Items[e.Index]).Chosen=e.NewValue==CheckState.Checked;};list.Format+=(s,e)=>{var item=e.ListItem as LayerChoice;if(item!=null)e.Value=item.Name+"    "+item.Info;};list.FormattingEnabled=true;Filter();
        }
        void Filter(){loading=true;list.Items.Clear();foreach(var x in items.Where(x=>x.Name.IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0).OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase))list.Items.Add(x,x.Chosen);loading=false;}
    }
}
