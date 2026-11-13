using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;

namespace MedGas2024.UI
{
    public sealed class ColorGroupForm : Form
    {
        public List<GasRow> Targets {get;private set;}
        public int SelectedColor {get {return picker.Value;}}
        private readonly List<GasRow> rows;
        private readonly GasRow current;
        private readonly ComboBox scope=new ComboBox(),system=new ComboBox();
        private readonly AciPicker picker;
        private readonly Label count=new Label();
        private readonly DataGridView preview=new DataGridView();
        private readonly Button save=new Button();
        public ColorGroupForm(List<GasRow> all,GasRow selected,Func<int,int?> nativePicker=null)
        {
            rows=all.ToList();current=selected;Targets=new List<GasRow>();Text="Đổi màu một dòng / cả cụm";Font=UiStyle.Body();AutoScaleMode=AutoScaleMode.Dpi;Size=new Size(800,540);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=1,RowCount=6};
            foreach(int h in new[]{44,44,84})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));Controls.Add(layout);
            layout.Controls.Add(new Label {Text="Chọn phạm vi và màu mới. Bước này chỉ đổi bảng; áp dụng vào DWG sau khi kiểm tra.",Dock=DockStyle.Fill},0,0);
            var choices=new FlowLayoutPanel {Dock=DockStyle.Fill};
            scope.Name="ColorScope";scope.DropDownStyle=ComboBoxStyle.DropDownList;scope.Width=260;scope.Items.AddRange(new object[]{"Chỉ dòng đang chọn","Cả hệ (mọi cỡ cùng mã hệ)","Các dòng tích Dùng","Toàn bộ danh sách"});choices.Controls.Add(scope);
            system.Name="ColorSystem";system.DropDownStyle=ComboBoxStyle.DropDownList;system.Width=160;system.Items.AddRange(rows.Select(x=>x.SystemCode).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).Cast<object>().ToArray());choices.Controls.Add(system);layout.Controls.Add(choices,0,1);
            picker=new AciPicker(selected==null?7:selected.ColorIndex,nativePicker){Dock=DockStyle.Fill};layout.Controls.Add(picker,0,2);
            UiStyle.Grid(preview);preview.Dock=DockStyle.Fill;preview.ReadOnly=true;preview.AllowUserToAddRows=false;preview.AllowUserToDeleteRows=false;preview.RowHeadersVisible=false;preview.AutoGenerateColumns=false;preview.BackgroundColor=Color.White;
            foreach(var c in new[]{new DataGridViewTextBoxColumn{DataPropertyName="SystemCode",HeaderText="Mã hệ",Width=130},new DataGridViewTextBoxColumn{DataPropertyName="Text",HeaderText="Chữ trên nét",Width=220},new DataGridViewTextBoxColumn{DataPropertyName="ColorIndex",HeaderText="ACI hiện tại",Width=140}})preview.Columns.Add(c);layout.Controls.Add(preview,0,3);
            count.Name="ColorCount";count.Dock=DockStyle.Fill;layout.Controls.Add(count,0,4);
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};save.Name="ApplyColor";save.AutoSize=true;save.Click+=delegate {UpdateTargets();if(Targets.Count>0){DialogResult=DialogResult.OK;Close();}};var cancel=new Button{Text="Hủy",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.Add(save);buttons.Controls.Add(cancel);layout.Controls.Add(buttons,0,5);AcceptButton=save;CancelButton=cancel;
            scope.SelectedIndexChanged+=delegate {UpdateTargets();};system.SelectedIndexChanged+=delegate {UpdateTargets();};picker.ValueChanged+=delegate {UpdateTargets();};
            if(selected!=null)system.SelectedItem=selected.SystemCode;else if(system.Items.Count>0)system.SelectedIndex=0;scope.SelectedIndex=1;
        }
        private void UpdateTargets()
        {
            system.Enabled=scope.SelectedIndex==1;string code=Convert.ToString(system.SelectedItem);
            Targets=rows.Where(r=>scope.SelectedIndex==0?Object.ReferenceEquals(r,current):scope.SelectedIndex==1?String.Equals(r.SystemCode,code,StringComparison.OrdinalIgnoreCase):scope.SelectedIndex==2?r.Enabled:scope.SelectedIndex==3).ToList();
            preview.DataSource=null;preview.DataSource=Targets;count.Text="Sẽ đổi "+Targets.Count+" dòng sang ACI "+SelectedColor+". Các ô Dùng không bị thay đổi.";save.Text="Đổi màu "+Targets.Count+" dòng trong bảng";save.Enabled=Targets.Count>0;
        }
    }
}
