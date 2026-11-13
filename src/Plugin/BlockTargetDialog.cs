using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace MedGas2024.Plugin
{
    internal sealed class BlockTargetDialog:Form
    {
        readonly ComboBox target=new ComboBox();readonly CheckBox parent=new CheckBox(),outside=new CheckBox();public string TargetName{get{return target.Text.Trim();}}public bool IncludeParent{get{return parent.Checked;}}public bool IncludeOutside{get{return outside.Checked;}}
        public BlockTargetDialog(string[] layers)
        {
            Text="Chọn Layer đích";Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(680,240);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(14),ColumnCount=1,RowCount=5};root.RowStyles.Add(new RowStyle(SizeType.Absolute,35));root.RowStyles.Add(new RowStyle(SizeType.Absolute,35));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(root);
            root.Controls.Add(new Label{Text="Chọn Layer hiện có hoặc nhập tên Layer mới:",Dock=DockStyle.Fill},0,0);target.DropDownStyle=ComboBoxStyle.DropDown;target.Items.AddRange(layers.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());target.Dock=DockStyle.Fill;root.Controls.Add(target,0,1);parent.Text="Chuyển thêm block mẫu bên ngoài sang Layer này";parent.AutoSize=true;root.Controls.Add(parent,0,2);
            outside.Text="Chuyển các đối tượng trực tiếp cùng Layer trong không gian hiện hành";outside.AutoSize=true;root.Controls.Add(outside,0,3);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var ok=new Button{Text="Xem ảnh hưởng",DialogResult=DialogResult.OK,AutoSize=true};var cancel=new Button{Text="Hủy",DialogResult=DialogResult.Cancel,AutoSize=true};buttons.Controls.Add(ok);buttons.Controls.Add(cancel);root.Controls.Add(buttons,0,4);AcceptButton=ok;CancelButton=cancel;
        }
    }
}
