using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;

namespace MedGas2024.UI
{
    public sealed class NewSystemForm : Form
    {
        public List<GasRow> NewRows {get;private set;}
        private readonly Profile profile;
        private readonly GasRow seed;
        private readonly List<GasRow> existing;
        private readonly TextBox code=new TextBox(),label=new TextBox(),description=new TextBox(),diameters=new TextBox(),prefix=new TextBox();
        private readonly ComboBox suffix=new ComboBox();
        private readonly NumericUpDown diameter=new NumericUpDown(),dash=new CompactNumber(),height=new CompactNumber();
        private readonly CheckBox batch=new CheckBox();
        private readonly AciPicker color;
        private readonly DataGridView preview=new DataGridView();
        private readonly Label status=new Label();
        private readonly Button save=new Button();
        private bool loading=true;
        public NewSystemForm(Profile settings,List<GasRow> currentRows,GasRow defaults,Func<int,int?> nativePicker=null)
        {UiTheme.Attach(this);
            profile=settings;seed=defaults.Copy();existing=currentRows.Select(x=>x.Copy()).ToList();NewRows=new List<GasRow>();
            Text="Tạo hệ mới — một cỡ hoặc hàng loạt";Font=UiStyle.Body();AutoScaleMode=AutoScaleMode.Dpi;Size=new Size(950,740);MinimumSize=new Size(900,680);StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=1,RowCount=8};
            foreach(int h in new[]{52,184,68,82,46})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));Controls.Add(root);
            root.Controls.Add(new Label {Text="Mã hệ đặt tên Layer; nhãn hệ hiện trên nét. Tiền tố và hậu tố có thể để trống.\r\nLinetype tự chuyển không dấu, không cách; nếu trùng tên, chọn mã khác.\r\nHoàn tất cửa sổ này mới thêm vào bảng, chưa thay đổi DWG.",Dock=DockStyle.Fill},0,0);
            var fields=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=4,RowCount=5};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,115));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,115));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));for(int i=0;i<5;i++)fields.RowStyles.Add(new RowStyle(SizeType.Absolute,36));root.Controls.Add(fields,0,1);
            code.Name="SystemCode";code.Text=seed.SystemCode;label.Name="LabelPrefix";label.Text=seed.LabelPrefix;description.Name="DescriptionPattern";description.Text="{He} - D{D}";description.MaxLength=200;
            prefix.Name="SystemPrefix";prefix.Text=profile.PrefixFor(seed);suffix.Name="SystemSuffix";suffix.DropDownStyle=ComboBoxStyle.DropDown;suffix.Items.AddRange(new object[]{"","RAW","BASE"});suffix.Text=profile.SuffixFor(seed);
            Number(diameter,"Diameter",1,10000,0,seed.Diameter);Number(dash,"Dash",.00001M,1000000,5,(decimal)seed.Dash);Number(height,"TextHeight",.00001M,1000000,5,(decimal)seed.TextScale);
            AddField(fields,0,0,"Tiền tố",prefix);AddField(fields,2,0,"Hậu tố",suffix);
            AddField(fields,0,1,"Mã hệ",code);AddField(fields,2,1,"Nhãn hệ",label);AddField(fields,0,2,"Một đường kính",diameter);AddField(fields,2,2,"Đoạn nét",dash);AddField(fields,0,3,"Cao chữ S",height);
            fields.Controls.Add(new Label{Text="Hướng chữ: "+seed.RotationMode+"=0; khoảng hở theo Tự căn ở màn hình chính.",Dock=DockStyle.Fill},2,3);fields.SetColumnSpan(fields.GetControlFromPosition(2,3),2);
            fields.Controls.Add(new Label{Text="Mẫu mô tả",Dock=DockStyle.Fill},0,4);description.Dock=DockStyle.Fill;fields.Controls.Add(description,1,4);fields.SetColumnSpan(description,3);
            var sizes=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};sizes.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,190));sizes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));batch.Name="CreateMany";batch.Text="Tạo nhiều cỡ trong cùng hệ";batch.AutoSize=true;sizes.Controls.Add(batch,0,0);sizes.SetColumnSpan(batch,2);sizes.Controls.Add(new Label{Text="Danh sách đường kính:",Dock=DockStyle.Fill},0,1);diameters.Name="Diameters";diameters.Text="";diameters.MaxLength=3000;diameters.Dock=DockStyle.Fill;diameters.Enabled=false;sizes.Controls.Add(diameters,1,1);root.Controls.Add(sizes,0,2);
            color=new AciPicker(seed.ColorIndex,nativePicker){Dock=DockStyle.Fill};root.Controls.Add(color,0,3);
            root.Controls.Add(new Label{Text="Xem trước: tên đã có sẽ bỏ qua. Mô tả: {D} = đường kính, {He} = nhãn hệ.\r\nVí dụ mô tả: Ống HDPE D{D}. Chữ trên nét luôn tạo theo D<cỡ>.<nhãn>.",Dock=DockStyle.Fill},0,4);
            UiStyle.Grid(preview);preview.Name="NewRowsPreview";preview.Dock=DockStyle.Fill;preview.ReadOnly=true;preview.AllowUserToAddRows=false;preview.AllowUserToDeleteRows=false;preview.RowHeadersVisible=false;preview.AutoGenerateColumns=false;preview.BackgroundColor=Color.White;
            foreach(var c in new[]{new DataGridViewTextBoxColumn{DataPropertyName="Diameter",HeaderText="Cỡ",Width=55},new DataGridViewTextBoxColumn{DataPropertyName="Text",HeaderText="Chữ trên nét",Width=150},new DataGridViewTextBoxColumn{DataPropertyName="Description",HeaderText="Mô tả",Width=220},new DataGridViewTextBoxColumn{DataPropertyName="Layer",HeaderText="Tên Layer",Width=235},new DataGridViewTextBoxColumn{DataPropertyName="TypeName",HeaderText="Tên Linetype",Width=235},new DataGridViewTextBoxColumn{DataPropertyName="State",HeaderText="Kết quả",Width=160}})preview.Columns.Add(c);root.Controls.Add(preview,0,5);
            status.Name="CreateStatus";status.Dock=DockStyle.Fill;root.Controls.Add(status,0,6);var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};save.Name="CreateSystem";save.AutoSize=true;save.Click+=delegate{RefreshPreview();if(NewRows.Count>0){DialogResult=DialogResult.OK;Close();}};var cancel=new Button{Text="Hủy",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.Add(save);buttons.Controls.Add(cancel);root.Controls.Add(buttons,0,7);AcceptButton=save;CancelButton=cancel;
            foreach(var t in new[]{code,label,description,diameters,prefix})t.TextChanged+=delegate{RefreshPreview();};suffix.TextChanged+=delegate{RefreshPreview();};foreach(var n in new[]{diameter,dash,height})n.ValueChanged+=delegate{RefreshPreview();};color.ValueChanged+=delegate{RefreshPreview();};batch.CheckedChanged+=delegate{diameters.Enabled=batch.Checked;diameter.Enabled=!batch.Checked;RefreshPreview();};loading=false;RefreshPreview();
        }
        private static void Number(NumericUpDown n,string name,decimal min,decimal max,int decimals,decimal value){n.Name=name;n.Minimum=min;n.Maximum=max;n.DecimalPlaces=decimals;n.Value=value;n.Dock=DockStyle.Fill;}
        private static void AddField(TableLayoutPanel p,int column,int row,string title,Control input){p.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,Margin=new Padding(0,6,0,0)},column,row);input.Dock=DockStyle.Fill;p.Controls.Add(input,column+1,row);}
        private void RefreshPreview()
        {
            if(loading)return;NewRows=new List<GasRow>();preview.DataSource=null;save.Enabled=false;
            try
            {
                var template=seed.Copy();template.SystemCode=code.Text.Trim().Normalize().ToUpperInvariant();template.LabelPrefix=label.Text.Trim().Normalize();template.PrefixOverride=prefix.Text.Trim();template.SuffixOverride=suffix.Text.Trim().ToUpperInvariant();template.LinetypeNameOverride=null;template.TextScale=(double)height.Value;template.Dash=(double)dash.Value;template.ColorIndex=color.Value;template.CustomText=null;template.Description=null;template.Enabled=true;
                var candidates=Profile.Expand(template,batch.Checked?diameters.Text:((int)diameter.Value).ToString(CultureInfo.InvariantCulture));
                foreach(var r in candidates)r.Description=description.Text.Trim().Replace("{D}",r.Diameter.ToString(CultureInfo.InvariantCulture)).Replace("{He}",r.LabelPrefix);
                var check=new Profile{Rows=candidates,FontFile=profile.FontFile,FontBold=profile.FontBold,LayerPrefix=profile.LayerPrefix,UnitTag=profile.UnitTag,GeometryFactor=profile.GeometryFactor,PaddingRatio=profile.PaddingRatio};check.RequireValid();var entries=new List<PreviewRow>();
                foreach(var r in candidates)
                {
                    var old=existing.FirstOrDefault(x=>String.Equals(profile.LayerNameFor(x),profile.LayerNameFor(r),StringComparison.OrdinalIgnoreCase)&&String.Equals(profile.NameFor(x),profile.NameFor(r),StringComparison.OrdinalIgnoreCase));
                    var collision=existing.FirstOrDefault(x=>!Object.ReferenceEquals(x,old)&&(String.Equals(profile.NameFor(x),profile.NameFor(r),StringComparison.OrdinalIgnoreCase)||String.Equals(profile.LayerNameFor(x),profile.LayerNameFor(r),StringComparison.OrdinalIgnoreCase)));
                    if(collision!=null)throw new InvalidDataException("Trùng tên Layer hoặc Linetype với hệ '"+collision.SystemCode+"'. Hãy đổi mã hệ, tiền tố hoặc hậu tố.");var shown=old??r;
                    entries.Add(new PreviewRow{Diameter=r.Diameter,Text=shown.Text,Description=shown.DescriptionText,Layer=profile.LayerNameFor(shown),TypeName=profile.NameFor(shown),State=old==null?"Sẽ thêm":"Đã có — bỏ qua"});if(old==null)NewRows.Add(r);
                }
                preview.DataSource=entries;status.ForeColor=Color.FromArgb(30,75,100);status.Text="Sẽ thêm "+NewRows.Count+" dòng; bỏ qua "+(candidates.Count-NewRows.Count)+" cỡ đã có. Các dòng mới được tích Dùng.\r\n"+(NewRows.Count==0?"Không còn cỡ mới. Sửa danh sách hoặc nhấn Hủy.":"Chưa ghi DWG. Sau khi thêm: Kiểm tra → Áp dụng vào DWG.");save.Text="Thêm "+NewRows.Count+" dòng vào bảng";save.Enabled=NewRows.Count>0;
            }
            catch(Exception ex){status.ForeColor=Color.Firebrick;status.Text=ex.Message;save.Text="Nhập thông số hợp lệ";}
        }
        public sealed class PreviewRow {public int Diameter{get;set;}public string Text{get;set;}public string Description{get;set;}public string Layer{get;set;}public string TypeName{get;set;}public string State{get;set;}}
    }
}
