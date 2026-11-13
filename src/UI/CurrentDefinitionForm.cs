using System;
using System.Drawing;
using System.Windows.Forms;
using MedGas2024.Core;

namespace MedGas2024.UI
{
    public sealed class CurrentDefinitionForm : Form
    {
        public DefinitionEdit Value{get;private set;}
        private readonly TextBox text=new TextBox(),description=new TextBox();
        private readonly NumericUpDown dash=new NumericUpDown(),before=new NumericUpDown(),after=new NumericUpDown(),height=new NumericUpDown(),x=new NumericUpDown(),y=new NumericUpDown(),aci=new NumericUpDown();
        private readonly CheckBox fit=new CheckBox(),color=new CheckBox();
        public CurrentDefinitionForm(DrawingLayerInfo row)
        {
            Text="Sửa định nghĩa hiện hành — "+row.LayerName;Font=UiStyle.Body();AutoScaleMode=AutoScaleMode.Dpi;Size=new Size(850,740);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=2,RowCount=12};root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,82));for(int i=0;i<8;i++)root.RowStyles.Add(new RowStyle(SizeType.Absolute,39));root.RowStyles.Add(new RowStyle(SizeType.Absolute,45));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,43));Controls.Add(root);
            var note=new Label{Text=row.LinetypeName+"\r\nFont/style thực: "+row.Font+"\r\nGiữ nguyên tên, font/style và hướng R/U. Cập nhật nét dùng chung có thể ảnh hưởng nhiều Layer.",Dock=DockStyle.Fill};root.Controls.Add(note,0,0);root.SetColumnSpan(note,2);
            text.Name="ActualText";text.Text=row.Text;description.Name="ActualDescription";description.Text=row.Description;
            Field(root,1,"Chữ trên nét",text);Field(root,2,"Mô tả",description);
            Number(dash,row.Dash,true);Number(before,row.Before,true);Number(after,row.After,true);Number(height,row.Height,true);Number(x,row.X,false);Number(y,row.Y,false);
            Field(root,3,"Đoạn nét",dash);Field(root,4,"Khoảng trước chữ",before);Field(root,5,"Khoảng sau chữ",after);Field(root,6,"Cao chữ S",height);Field(root,7,"Offset X",x);Field(root,8,"Offset Y",y);
            fit.Text="Tự căn theo font thực; hở mỗi bên = 0.35 × S";fit.Checked=false;fit.AutoSize=true;fit.CheckedChanged+=delegate{x.Enabled=y.Enabled=before.Enabled=after.Enabled=!fit.Checked;};x.Enabled=y.Enabled=before.Enabled=after.Enabled=true;root.Controls.Add(fit,0,9);root.SetColumnSpan(fit,2);
            var info=new FlowLayoutPanel{Dock=DockStyle.Fill};color.Text="Đổi màu Layer thành ACI:";color.AutoSize=true;aci.Minimum=1;aci.Maximum=255;aci.Value=Math.Max(1,Math.Min(255,row.Aci));aci.Width=65;aci.Enabled=false;color.CheckedChanged+=delegate{aci.Enabled=color.Checked;};info.Controls.Add(color);info.Controls.Add(aci);info.Controls.Add(new Label{Text=row.SharedLayers+" Layer / "+row.AffectedCount+" đối tượng dùng nét; "+row.OverrideCount+" ghi đè trên Layer này.\r\nSẽ xem bảng thay đổi trước khi ghi vào DWG.",AutoSize=true});root.Controls.Add(info,0,10);root.SetColumnSpan(info,2);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var save=new Button{Name="ReviewDefinition",Text="Xem thay đổi…",AutoSize=true};save.Click+=delegate{try{Value=new DefinitionEdit{Text=text.Text.Trim(),Description=description.Text,Dash=(double)dash.Value,Before=(double)before.Value,After=(double)after.Value,Height=(double)height.Value,X=(double)x.Value,Y=(double)y.Value,AutoFit=fit.Checked,ChangeColor=color.Checked,Aci=(int)aci.Value};Value.Validate(row.Editable);DialogResult=DialogResult.OK;Close();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Kiểm tra thông số");}};var cancel=new Button{Text="Hủy",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.Add(save);buttons.Controls.Add(cancel);root.Controls.Add(buttons,0,11);root.SetColumnSpan(buttons,2);AcceptButton=save;CancelButton=cancel;
        }
        private static void Field(TableLayoutPanel root,int row,string title,Control input){root.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,Padding=new Padding(0,7,0,0)},0,row);input.Dock=DockStyle.Fill;root.Controls.Add(input,1,row);}
        private static void Number(NumericUpDown n,double value,bool positive){n.DecimalPlaces=12;n.Minimum=positive?.000000000001M:-1000000000M;n.Maximum=1000000000M;n.Value=Math.Max(n.Minimum,Math.Min(n.Maximum,(decimal)value));}
    }
}
