using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;

namespace MedGas2024.UI
{
    public sealed class RoomFindForm:Form
    {
        public string SelectedHandle { get; private set; }
        public bool PickPoint { get; private set; }

        public RoomFindForm(RoomEntry entry,List<RoomReviewRow> candidates)
        {
            UiTheme.Attach(this);
            Text="Tìm vị trí trong DWG — "+entry.Id;
            Size=new Size(880,540);MinimumSize=new Size(640,400);StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10),ColumnCount=1,RowCount=4};
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(root);
            root.Controls.Add(new Label{AutoSize=true,Text="Tìm theo chữ, layer hoặc handle (không cần dấu, nhiều từ; -từ để loại). Phòng đang gắn: "+entry.OldName+" → "+entry.NewName},0,0);
            var query=new TextBox{Dock=DockStyle.Fill,Text=entry.OldName};root.Controls.Add(query,0,1);
            var grid=new DataGridView{Dock=DockStyle.Fill,AutoGenerateColumns=false,ReadOnly=true,AllowUserToAddRows=false,
                MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,RowHeadersVisible=false};
            UiStyle.Grid(grid);
            foreach(var c in new[]{new[]{"Current","Chữ trên bản vẽ","240"},new[]{"Id","Layer","150"},new[]{"Handle","Vị trí (handle)","110"},new[]{"Action","Hành động","140"},new[]{"Status","Trạng thái","200"}})
                grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName=c[0],HeaderText=c[1],Width=Int32.Parse(c[2]),SortMode=DataGridViewColumnSortMode.Automatic});
            grid.Columns[grid.Columns.Count-1].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;
            root.Controls.Add(grid,0,2);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};
            var use=new Button{Text="Gắn ID vào chữ đã chọn",AutoSize=true};
            new ToolTip().SetToolTip(use,"Chữ trực tiếp: gắn ID vào chính chữ. Chữ trong block dùng chung: tạo nhãn MTEXT có ID đúng vị trí chữ.");
            var zoom=new Button{Text="Phóng tới chữ và chọn điểm…",AutoSize=true};
            var cancel=new Button{Text="Hủy",AutoSize=true,DialogResult=DialogResult.Cancel};
            var free=new Button{Text="Chọn điểm bất kỳ…",AutoSize=true};
            free.Click+=delegate{PickPoint=true;SelectedHandle=null;DialogResult=DialogResult.OK;};
            var all=new Button{Text="Hiện tất cả chữ",AutoSize=true};all.Click+=delegate{query.Clear();};
            actions.Controls.AddRange(new Control[]{use,zoom,free,all,cancel});root.Controls.Add(actions,0,3);CancelButton=cancel;
            Func<RoomReviewRow> selected=()=>grid.CurrentRow==null?null:grid.CurrentRow.DataBoundItem as RoomReviewRow;
            Action enable=()=>{var hit=selected();zoom.Enabled=hit!=null;use.Enabled=hit!=null&&String.IsNullOrEmpty(hit.Status);};
            Action refresh=()=>{
                GridSort.Bind(grid,candidates.Where(h=>RoomSearch.Matches(query.Text,h.Current,h.Id,h.Handle,h.Action)).ToList());
                enable();
            };
            Action<bool> finish=point=>{var hit=selected();if(hit==null)return;SelectedHandle=hit.Handle;PickPoint=point;DialogResult=DialogResult.OK;};
            query.TextChanged+=delegate{refresh();};grid.SelectionChanged+=delegate{enable();};
            use.Click+=delegate{finish(false);};zoom.Click+=delegate{finish(true);};Shown+=delegate{refresh();query.SelectAll();query.Focus();};
        }
    }
}
