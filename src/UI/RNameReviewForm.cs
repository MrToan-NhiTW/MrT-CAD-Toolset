using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;
namespace MedGas2024.UI
{
    public sealed class RNameReviewForm:Form
    {
        readonly BindingList<RNameReviewRow> rows;readonly Func<List<string>,bool> review;readonly DataGridView grid=new DataGridView{Name="RNameGrid",Dock=DockStyle.Fill};readonly TextBox detail=new TextBox{Name="RNameDetail",Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
        public Action<Exception> ErrorHandler{get;set;}
        public RNameReviewForm(IEnumerable<RNameReviewRow> items,Func<List<string>,bool> reviewApply)
        {
            rows=new BindingList<RNameReviewRow>(items.ToList());review=reviewApply;Text="Mr.T Đồng nhất tên nét chữ R | 2.0.1.6";Font=UiStyle.Body();StartPosition=FormStartPosition.CenterParent;Size=new Size(1450,790);MinimumSize=new Size(1100,680);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=4};root.RowStyles.Add(new RowStyle(SizeType.Absolute,90));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,95));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));Controls.Add(root);
            root.Controls.Add(new Label{Dock=DockStyle.Fill,Text="QUÉT TOÀN DWG: gom các nét cùng nhãn về tên layer Mr.T chuẩn; lấy định nghĩa ĐANG GÁN CHO LAYER CHUẨN.\r\nNhịp, font, cỡ chữ và các nét riêng khác sẽ theo chuẩn. Scale đường không đổi: khoảng chữ các đường scale khác nhau có thể khác nhau.\r\nCó thể ảnh hưởng layer khác và block dùng chung; Xref không sửa. Chọn dòng để xem chi tiết, rồi xem trước trước khi ghi."},0,0);
            UiStyle.Grid(grid);grid.AutoGenerateColumns=false;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.RowHeadersVisible=false;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="Use",DataPropertyName="Use",HeaderText="Chọn",MinimumWidth=45,FillWeight=40});
            foreach(var c in new[]{new[]{"Label","Chữ","65"},new[]{"StandardLayer","Layer chuẩn","140"},new[]{"Source","Nét đang gán layer chuẩn","160"},new[]{"Target","Tên thống nhất","145"},new[]{"Layers","Layer ảnh hưởng","60"},new[]{"Entities","Đối tượng","60"},new[]{"Nested","Trong block","60"},new[]{"Variants","Kiểu khác nhau","60"},new[]{"State","Tình trạng","170"}})grid.Columns.Add(new DataGridViewTextBoxColumn{Name=c[0],DataPropertyName=c[0],HeaderText=c[1],ReadOnly=true,MinimumWidth=55,FillWeight=Int32.Parse(c[2])});grid.DataSource=rows;root.Controls.Add(grid,0,1);
            grid.CurrentCellDirtyStateChanged+=delegate{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};grid.CellValueChanged+=delegate(object s,DataGridViewCellEventArgs e){if(e.RowIndex<0)return;var row=grid.Rows[e.RowIndex].DataBoundItem as RNameReviewRow;if(row!=null&&!row.Ready&&row.Use){row.Use=false;rows.ResetItem(e.RowIndex);}};
            grid.SelectionChanged+=delegate{var row=grid.CurrentRow==null?null:grid.CurrentRow.DataBoundItem as RNameReviewRow;detail.Text=row==null?"":row.Details??row.State;};Shown+=delegate{foreach(DataGridViewRow line in grid.Rows){var r=line.DataBoundItem as RNameReviewRow;if(r!=null&&!r.Ready)line.DefaultCellStyle.ForeColor=Color.Firebrick;}if(grid.CurrentRow!=null){var r=grid.CurrentRow.DataBoundItem as RNameReviewRow;if(r!=null)detail.Text=r.Details??r.State;}};root.Controls.Add(detail,0,2);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var close=new Button{Name="RNameClose",Text="Đóng / để sau",DialogResult=DialogResult.Cancel,AutoSize=true};var apply=new Button{Name="RNameApply",Text="Xem trước / áp dụng",AutoSize=true};apply.Click+=delegate{try{grid.EndEdit();var keys=rows.Where(r=>r.Use&&r.Ready).Select(r=>r.Key).ToList();if(keys.Count==0)throw new ArgumentException("Chọn ít nhất một họ đủ điều kiện.");if(review(keys)){DialogResult=DialogResult.OK;Close();}}catch(Exception ex){if(ErrorHandler!=null)ErrorHandler(ex);else MessageBox.Show(this,ex.Message,"Chưa áp dụng");}};buttons.Controls.Add(close);buttons.Controls.Add(apply);root.Controls.Add(buttons,0,3);CancelButton=close;
        }
    }
}
