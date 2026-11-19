using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MedGas2024.Core;
namespace MedGas2024.UI
{
    public sealed class PipePickerForm : Form
    {
        private readonly DrawingBrowser browser;
        private readonly CheckBox color=new CheckBox(),keepScale=new CheckBox(),isolate=new CheckBox(),family=new CheckBox();private string familyLayer;private bool familyOptions,previousKeep,previousColor;private decimal previousScale;
        private readonly NumericUpDown scale=new NumericUpDown();
        private readonly Button apply=new Button();
        private DrawingLayerInfo accepted;
        public bool IsolateAfterAssign{get{return isolate.Checked;}}
        public bool ColorByLayer{get{return family.Checked||color.Checked;}}
        public bool KeepScale{get{return !family.Checked&&keepScale.Checked;}}
        private double? manualWidth;public double? GlobalWidth{get{return family.Checked?Selected.FamilyWidth:manualWidth;}}
        public double EntityScale{get{return family.Checked?Selected.FamilyScale:(double)scale.Value;}}
        public DrawingLayerInfo Selected{get{return accepted??browser.Selected;}}
        public PipePickerForm(List<DrawingLayerInfo> rows,int selectedCount,ScaleContext context=null)
        {UiTheme.Attach(this);
            Text="Mr.T Gán nhanh thuộc tính ống | 2.0.2.5";Font=UiStyle.Body();AutoScaleMode=AutoScaleMode.Dpi;Size=new Size(1120,760);MinimumSize=new Size(1000,700);StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(245,247,250);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=1,RowCount=6};root.RowStyles.Add(new RowStyle(SizeType.Absolute,56));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,106));root.RowStyles.Add(new RowStyle(SizeType.Absolute,72));root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));root.RowStyles.Add(new RowStyle(SizeType.Absolute,22));Controls.Add(root);
            root.Controls.Add(new Label{Text="1. Gõ tìm hệ/cỡ trong DWG → 2. Chọn dòng → 3. Gán cho đường ống.\r\n"+(selectedCount>0?"Đã chọn sẵn "+selectedCount+" đối tượng; tool sẽ lọc các đường hợp lệ.":"Chưa chọn sẵn; sau khi bấm nút sẽ chọn trên bản vẽ."),Dock=DockStyle.Fill},0,0);
            browser=new DrawingBrowser(rows);browser.SelectionChanged+=delegate{Availability();};browser.RowActivated+=delegate{if(apply.Enabled)apply.PerformClick();};root.Controls.Add(browser,0,1);
            var options=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};color.Name="AssignColorByLayer";color.Text="Màu theo Layer";color.Checked=false;color.AutoSize=true;keepScale.Name="KeepEntityScale";keepScale.Text="Giữ hệ số nét hiện tại";keepScale.Checked=true;keepScale.AutoSize=true;scale.Name="AssignEntityScale";scale.DecimalPlaces=9;scale.Minimum=.000000001M;scale.Maximum=1000000;scale.Value=1;scale.Width=110;options.Controls.Add(color);options.Controls.Add(keepScale);options.Controls.Add(scale);var paperButton=new Button{Name="AssignPaperPreset",Text="A0–A4 / Scale và Width…",AutoSize=true};paperButton.Click+=delegate{using(var dialog=new ScaleForm(rows,selectedCount,context??new ScaleContext(),true,Selected)){if(dialog.ShowDialog(this)!=DialogResult.OK)return;family.Checked=false;keepScale.Checked=!dialog.Value.ApplyScale;if(dialog.Value.ApplyScale)scale.Value=(decimal)dialog.Value.EntityScale;manualWidth=dialog.Value.ApplyWidth?(double?)dialog.Value.GlobalWidth:null;paperButton.Text="A0–A4 / Width: "+(GlobalWidth.HasValue?Profile.N(GlobalWidth.Value):"giữ nguyên");}};options.Controls.Add(paperButton);isolate.Name="IsolateAfterAssign";isolate.Text="Isolate hệ sau gán, giữ nhóm đang cô lập";isolate.Checked=true;isolate.AutoSize=true;options.Controls.Add(isolate);family.Name="UseFamilyDefaults";family.Text="Theo ống mẫu: màu Layer + Scale / Width";family.AutoSize=true;family.CheckedChanged+=delegate{if(family.Checked&&!familyOptions){previousKeep=keepScale.Checked;previousColor=color.Checked;previousScale=scale.Value;familyOptions=true;keepScale.Checked=false;color.Checked=true;}else if(!family.Checked&&familyOptions){familyOptions=false;keepScale.Checked=previousKeep;color.Checked=previousColor;scale.Value=previousScale;}Availability();};options.Controls.Add(family);root.Controls.Add(options,0,2);
            root.Controls.Add(new Label{Name="AssignmentDetails",Text="Đặt Layer đích và Linetype ByLayer; màu/hệ số theo lựa chọn phía trên.\r\nGán được vào Layer tắt/đóng băng. Isolate bật tạm Layer của hệ và giữ các vật thể đang cô lập bằng Mr.T.\r\nChỉ nhận LINE/POLYLINE/ARC/SPLINE trực tiếp trong Model/Layout; bỏ qua chữ, DIM, block và Layer khóa.",Dock=DockStyle.Fill},0,3);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};apply.Name="AssignPipe";apply.Text=selectedCount>0?"Gán cho "+selectedCount+" đối tượng đã chọn":"Chọn ống trên bản vẽ → Gán";apply.AutoSize=true;apply.Click+=delegate{if(Selected!=null&&Selected.CanAssign){accepted=Selected;DialogResult=DialogResult.OK;Close();}};var cancel=new Button{Name="CancelAssignment",Text="Hủy",AutoSize=true,DialogResult=DialogResult.Cancel};cancel.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};buttons.Controls.Add(apply);buttons.Controls.Add(cancel);root.Controls.Add(buttons,0,4);root.Controls.Add(new Label{Text="workflow by Mr.Toan-NhiTW",Font=new Font("Segoe UI",8.5F),ForeColor=Color.DimGray,TextAlign=ContentAlignment.MiddleRight,Dock=DockStyle.Fill},0,5);
            keepScale.CheckedChanged+=delegate{Availability();};AcceptButton=apply;CancelButton=cancel;Shown+=delegate{browser.FocusSearch();};Availability();
        }
        private void Availability(){if(browser==null)return;var row=Selected;if(row==null){familyLayer=null;family.Enabled=false;family.Visible=false;family.Checked=false;}else if(familyLayer!=row.LayerName){familyLayer=row.LayerName;family.Enabled=row.HasFamilyDefaults;family.Visible=row.HasFamilyDefaults;family.Text=row.HasFamilyDefaults?"Theo mẫu "+row.FamilySource+": màu Layer, Scale="+Profile.N(row.FamilyScale)+", Width="+(row.FamilyWidth.HasValue?Profile.N(row.FamilyWidth.Value):"giữ"):"Theo ống mẫu: màu Layer + Scale / Width";family.Checked=row.HasFamilyDefaults;if(row.HasFamilyDefaults)scale.Value=(decimal)row.FamilyScale;}color.Enabled=!family.Checked;keepScale.Enabled=!family.Checked;scale.Enabled=!family.Checked&&!keepScale.Checked;apply.Enabled=row!=null&&row.CanAssign;}
    }
}