using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;

namespace MedGas2024.UI
{
    public sealed class DrawingBrowser : UserControl
    {
        private List<DrawingLayerInfo> items;
        private readonly bool batch;private bool filtering;private string sortColumn=null;private bool sortAscending=true;private readonly HashSet<string> marked=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly CheckBox all=new CheckBox{Name="DrawingSelectAll",Text="Tích các dòng đang hiện",AutoSize=true,ThreeState=true,AutoCheck=false};
        public event Action<DrawingLayerInfo> EditRequested,DeleteRequested;
        public List<DrawingLayerInfo> CheckedRows{get{return items.Where(r=>marked.Contains(r.LayerName)).ToList();}}
        public void ClearChecks(){marked.Clear();Filter();}
        public void ShowAllLayers(){managed.Checked=false;}
        private readonly TextBox query=new TextBox(),prefix=new TextBox(),suffix=new TextBox();
        private readonly CheckBox managed=new CheckBox(),used=new CheckBox();
        private readonly DataGridView grid=new DataGridView();
        private readonly Label count=new Label();
        public event EventHandler SelectionChanged;
        public event EventHandler RowActivated;
        public List<DrawingLayerInfo> SelectedRows{get{return grid.SelectedRows.Cast<DataGridViewRow>().Select(r=>r.DataBoundItem as DrawingLayerInfo).Where(r=>r!=null).ToList();}}
        public DrawingLayerInfo Selected {get{return grid.CurrentRow==null?null:grid.CurrentRow.DataBoundItem as DrawingLayerInfo;}}
        public DrawingBrowser(List<DrawingLayerInfo> initial,bool enableBatch=false)
        {
            Font=UiStyle.Body();Dock=DockStyle.Fill;items=initial;batch=enableBatch;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));root.RowStyles.Add(new RowStyle(SizeType.Absolute,35));root.RowStyles.Add(new RowStyle(SizeType.Absolute,batch?36:0));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,28));Controls.Add(root);
            var first=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};first.Controls.Add(new Label{Text="Tìm hệ / cỡ:",AutoSize=true,Margin=new Padding(0,7,7,0)});query.Name="DrawingQuery";query.Width=310;first.Controls.Add(query);first.Controls.Add(new Label{Text="VD: CN 25 • D110.HDPE • TN160",AutoSize=true,Margin=new Padding(8,7,0,0)});root.Controls.Add(first,0,0);
            var second=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};second.Controls.Add(new Label{Text="Tiền tố Layer:",AutoSize=true,Margin=new Padding(0,6,5,0)});prefix.Name="DrawingPrefix";prefix.Width=110;second.Controls.Add(prefix);second.Controls.Add(new Label{Text="Hậu tố Layer:",AutoSize=true,Margin=new Padding(8,6,5,0)});suffix.Name="DrawingSuffix";suffix.Width=100;second.Controls.Add(suffix);managed.Name="ManagedOnly";managed.Text="Layer của Mr.T";managed.Checked=true;managed.AutoSize=true;second.Controls.Add(managed);used.Name="UsedOnly";used.Text="Đang có đối tượng";used.AutoSize=true;second.Controls.Add(used);root.Controls.Add(second,0,1);
            grid.Name="DrawingGrid";grid.Dock=DockStyle.Fill;grid.ReadOnly=!batch;grid.MultiSelect=batch;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.AutoGenerateColumns=false;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.RowHeadersVisible=false;grid.BackgroundColor=Color.White;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;UiStyle.Grid(grid);grid.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.False;
            if(batch){grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="Marked",HeaderText="Chọn",Width=55,MinimumWidth=55,AutoSizeMode=DataGridViewAutoSizeColumnMode.None,ReadOnly=false});var selection=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};selection.Controls.Add(all);var clear=new Button{Name="DrawingClearSelection",Text="Bỏ tích tất cả",AutoSize=true};clear.Click+=delegate{marked.Clear();Filter();};selection.Controls.Add(clear);selection.Controls.Add(new Label{Text="Tích nhiều dòng → Sửa / Xóa. Giữ ô tích khi tìm kiếm.",AutoSize=true,Margin=new Padding(10,7,0,0)});root.Controls.Add(selection,0,2);all.Click+=delegate{bool check=all.CheckState!=CheckState.Checked;foreach(DataGridViewRow line in grid.Rows){var info=(DrawingLayerInfo)line.DataBoundItem;if(check)marked.Add(info.LayerName);else marked.Remove(info.LayerName);}Filter();};}
            foreach(var spec in new[]{new[]{"Text","Chữ trên nét","105"},new[]{"LayerName","Layer hiện hành","200"},new[]{"LinetypeName","Linetype hiện hành","200"},new[]{"ColorText","Màu","85"},new[]{"UsedCount","Đối tượng","70"},new[]{"OverrideCount","Ghi đè","65"},new[]{"State","Trạng thái","90"}})grid.Columns.Add(new DataGridViewTextBoxColumn{Name=spec[0],DataPropertyName=spec[0],ReadOnly=true,SortMode=DataGridViewColumnSortMode.Programmatic,HeaderText=spec[1],FillWeight=Int32.Parse(spec[2]),MinimumWidth=spec[0]=="UsedCount"?100:spec[0]=="OverrideCount"?80:spec[0]=="State"?100:55});if(batch){grid.Columns.Add(new DataGridViewButtonColumn{Name="EditRow",HeaderText="",Text="Sửa…",UseColumnTextForButtonValue=true,Width=65,MinimumWidth=65,AutoSizeMode=DataGridViewAutoSizeColumnMode.None});grid.Columns.Add(new DataGridViewButtonColumn{Name="DeleteRow",HeaderText="",Text="Xóa",UseColumnTextForButtonValue=true,Width=60,MinimumWidth=60,AutoSizeMode=DataGridViewAutoSizeColumnMode.None});}
            root.Controls.Add(grid,0,3);
            count.Dock=DockStyle.Fill;root.Controls.Add(count,0,4);
            foreach(var box in new[]{query,prefix,suffix})box.TextChanged+=delegate{Filter();};managed.CheckedChanged+=delegate{Filter();};used.CheckedChanged+=delegate{Filter();};grid.SelectionChanged+=delegate{if(SelectionChanged!=null)SelectionChanged(this,EventArgs.Empty);};grid.CurrentCellChanged+=delegate{if(SelectionChanged!=null)SelectionChanged(this,EventArgs.Empty);};grid.CellDoubleClick+=delegate(object s,DataGridViewCellEventArgs e){if(e.RowIndex>=0&&e.ColumnIndex>=0&&grid.Columns[e.ColumnIndex] is DataGridViewTextBoxColumn&&RowActivated!=null)RowActivated(this,EventArgs.Empty);};if(batch){grid.CurrentCellDirtyStateChanged+=delegate{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};grid.CellValueChanged+=delegate(object sender,DataGridViewCellEventArgs e){if(filtering||e.RowIndex<0||e.ColumnIndex!=grid.Columns["Marked"].Index)return;var line=grid.Rows[e.RowIndex];var info=(DrawingLayerInfo)line.DataBoundItem;if(Convert.ToBoolean(line.Cells["Marked"].Value))marked.Add(info.LayerName);else marked.Remove(info.LayerName);UpdateCounts();};grid.CellContentClick+=delegate(object sender,DataGridViewCellEventArgs e){if(e.RowIndex<0)return;var row=(DrawingLayerInfo)grid.Rows[e.RowIndex].DataBoundItem;string key=grid.Columns[e.ColumnIndex].Name;if(key=="EditRow"&&EditRequested!=null)EditRequested(row);if(key=="DeleteRow"&&DeleteRequested!=null)DeleteRequested(row);};}
            grid.ColumnHeaderMouseClick+=delegate(object sender,DataGridViewCellMouseEventArgs e){if(e.ColumnIndex<0||!(grid.Columns[e.ColumnIndex] is DataGridViewTextBoxColumn))return;string column=grid.Columns[e.ColumnIndex].Name;if(sortColumn==column)sortAscending=!sortAscending;else{sortColumn=column;sortAscending=true;}Filter();};
            query.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Down){grid.Focus();e.Handled=true;}};Filter();
        }
        public void Reload(List<DrawingLayerInfo> value){items=value;marked.RemoveWhere(name=>!items.Any(r=>String.Equals(r.LayerName,name,StringComparison.OrdinalIgnoreCase)));Filter();}
        public void FocusSearch(){query.Focus();query.SelectAll();}
        private void Filter()
        {
            filtering=true;try{string keep=Selected==null?null:Selected.LayerName;var filtered=DrawingSearch.Filter(items,query.Text,prefix.Text,suffix.Text,managed.Checked,used.Checked);if(sortColumn!=null)filtered=DrawingSearch.Sort(filtered,sortColumn,sortAscending);grid.DataSource=filtered;foreach(DataGridViewColumn col in grid.Columns)if(col is DataGridViewTextBoxColumn)col.HeaderCell.SortGlyphDirection=col.Name==sortColumn?(sortAscending?SortOrder.Ascending:SortOrder.Descending):SortOrder.None;
            if(keep!=null)foreach(DataGridViewRow row in grid.Rows)if(((DrawingLayerInfo)row.DataBoundItem).LayerName==keep){grid.CurrentCell=row.Cells[batch?1:0];break;}
            if(batch)foreach(DataGridViewRow line in grid.Rows)line.Cells["Marked"].Value=marked.Contains(((DrawingLayerInfo)line.DataBoundItem).LayerName);
            }finally{filtering=false;}UpdateCounts();
        }
        private void UpdateCounts(){int visible=grid.Rows.Cast<DataGridViewRow>().Count(r=>marked.Contains(((DrawingLayerInfo)r.DataBoundItem).LayerName));all.CheckState=visible==0?CheckState.Unchecked:visible==grid.Rows.Count?CheckState.Checked:CheckState.Indeterminate;count.Text=grid.Rows.Count+" / "+items.Count+" Layer"+(batch?" • đã tích "+marked.Count+" (ẩn sau lọc: "+(marked.Count-visible)+")":"")+" • mẫu Mr.T_PV_ được loại riêng.";if(SelectionChanged!=null)SelectionChanged(this,EventArgs.Empty);}
    }
}
