using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MedGas2024.Core;

namespace MedGas2024.UI
{
    public sealed class RowEditorForm : Form
    {
        public GasRow Value { get; private set; }
        private readonly TextBox code=new TextBox(), label=new TextBox(), text=new TextBox(), description=new TextBox();
        private readonly NumericUpDown diameter=new NumericUpDown(), color=new NumericUpDown();
        private readonly Label error=new Label();
        private readonly Func<GasRow,string> extraValidation;
        public RowEditorForm(GasRow source,bool creating=false,Func<GasRow,string> validateExtra=null,Func<int,int?> colorPicker=null)
        {UiTheme.Attach(this);
            extraValidation=validateExtra;
            Value=source.Copy(); Text=creating?"Tạo hệ mới":"Sửa hệ / nhãn / mô tả"; Font=UiStyle.Body();AutoScaleMode=AutoScaleMode.Dpi;
            Size=new Size(740,580); MinimumSize=Size; StartPosition=FormStartPosition.CenterParent;
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=10,Padding=new Padding(20)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,165)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,100));
            for(int i=0;i<7;i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
            Controls.Add(layout);
            var help=new Label {Text="Mã hệ dùng đặt tên Layer / Linetype; Nhãn hệ là chữ in trên nét.\r\nVí dụ: mã HDPE, nhãn HDPE, cỡ 110 → D110.HDPE.\r\nMô tả Linetype giúp nhận diện trong CAD; chỉ chữ trên nét xuất hiện dọc đường. Bước này chỉ thêm / sửa bảng, chưa ghi vào DWG.",Dock=DockStyle.Fill};
            layout.Controls.Add(help,0,0); layout.SetColumnSpan(help,2);
            code.Name="SystemCode"; code.Text=source.SystemCode; label.Name="LabelPrefix"; label.Text=source.LabelPrefix;
            text.Name="Text"; text.Text=source.Text; description.Name="Description"; description.Text=source.DescriptionText;
            diameter.Name="Diameter"; diameter.Minimum=1; diameter.Maximum=10000; diameter.Value=source.Diameter;
            color.Name="ColorIndex"; color.Minimum=1; color.Maximum=255; color.Value=source.ColorIndex;
            string priorText=source.DefaultText, priorDescription=source.SystemCode+" - D"+source.Diameter;
            Action refreshAutomatic=delegate
            {
                string nextText=GasRow.FormatText(label.Text.Trim().Normalize(),(int)diameter.Value);
                string nextDescription=code.Text.Trim().Normalize().ToUpperInvariant()+" - D"+diameter.Value;
                if(text.Text==priorText) text.Text=nextText;
                if(description.Text==priorDescription) description.Text=nextDescription;
                priorText=nextText; priorDescription=nextDescription;
            };
            label.TextChanged+=delegate { refreshAutomatic(); }; diameter.ValueChanged+=delegate { refreshAutomatic(); }; code.TextChanged+=delegate { refreshAutomatic(); };
            var colors=new FlowLayoutPanel {Dock=DockStyle.Fill,Margin=Padding.Empty};color.Width=75;colors.Controls.Add(color);
            var chooseColor=new Button {Text="Bảng màu…",AutoSize=true,Enabled=colorPicker!=null};chooseColor.Click+=delegate {var chosen=colorPicker((int)color.Value);if(chosen.HasValue)color.Value=chosen.Value;};colors.Controls.Add(chooseColor);
            Control[] inputs={code,diameter,label,text,description,colors};
            string[] labels={"Mã hệ","Đường kính","Nhãn hệ","Chữ hiện trên nét","Mô tả Linetype","Màu ACI"};
            for(int i=0;i<inputs.Length;i++) { layout.Controls.Add(new Label {Text=labels[i],AutoSize=true,Margin=new Padding(0,6,0,0)},0,i+1); inputs[i].Dock=DockStyle.Fill; layout.Controls.Add(inputs[i],1,i+1); }
            var auto=new Button {Text="Chữ tự động: D25.CN / D110.HDPE",Dock=DockStyle.Fill};
            auto.Click+=delegate { text.Text=GasRow.FormatText(label.Text.Trim().Normalize(),(int)diameter.Value); };
            layout.Controls.Add(auto,1,7); error.Name="ValidationError"; error.ForeColor=Color.Firebrick; error.Dock=DockStyle.Fill; layout.Controls.Add(error,0,8); layout.SetColumnSpan(error,2);
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};
            var save=new Button {Name="Save",Text=creating?"Thêm vào bảng":"Áp dụng vào bảng",AutoSize=true}; var cancel=new Button {Text="Hủy",DialogResult=DialogResult.Cancel,AutoSize=true};
            save.Click+=delegate { Save(); }; buttons.Controls.Add(save); buttons.Controls.Add(cancel); layout.Controls.Add(buttons,0,9); layout.SetColumnSpan(buttons,2);
            AcceptButton=save; CancelButton=cancel;
        }
        private void Save()
        {
            var candidate=Value.Copy(); candidate.SystemCode=code.Text.Trim().Normalize().ToUpperInvariant(); candidate.Diameter=(int)diameter.Value;
            if(!String.Equals(candidate.SystemCode,Value.SystemCode,StringComparison.OrdinalIgnoreCase)||candidate.Diameter!=Value.Diameter)candidate.LinetypeNameOverride=null;
            candidate.LabelPrefix=label.Text.Trim().Normalize(); candidate.Text=text.Text;
            string enteredDescription=description.Text.Trim();
            candidate.Description=enteredDescription==candidate.SystemCode+" - D"+candidate.Diameter ? null : enteredDescription;
            candidate.ColorIndex=(int)color.Value;
            var validation=candidate.Copy(); validation.Enabled=true;
            var check=new Profile {Rows=new List<GasRow>{validation}};
            var errors=check.Validate(); if(errors.Count>0) { error.Text=String.Join(" ",errors); return; }
            if(extraValidation!=null) { var extra=extraValidation(candidate); if(!String.IsNullOrEmpty(extra)) { error.Text=extra; return; } }
            Value=candidate; DialogResult=DialogResult.OK; Close();
        }
    }
}
