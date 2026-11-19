using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;

namespace MedGas2024.UI
{
    // Lists placed Mr.T_PHONG blocks so names, attributes, size, colour, frame and wipeout can be
    // edited in place, in bulk or through Excel, without going back to the import step.
    public sealed class RoomManagerForm:Form
    {
        public List<RoomBlockInfo> Rows{get;private set;}
        public string ZoomHandle{get;private set;}
        readonly Dictionary<string,RoomBlockInfo> original;
        readonly DataGridView grid=new DataGridView{Dock=DockStyle.Fill,AutoGenerateColumns=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=true,EditMode=DataGridViewEditMode.EditOnKeystrokeOrF2};
        readonly TextBox query=new TextBox{Width=260};
        readonly ComboBox filter=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=190},frame=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=150};
        readonly NumericUpDown height=new CompactNumber{Minimum=0,Maximum=1000000000,DecimalPlaces=5,Width=95},width=new CompactNumber{Minimum=-1,Maximum=1000000000,DecimalPlaces=5,Width=95,Value=-1},
            color=new NumericUpDown{Minimum=0,Maximum=256,Width=60},pad=new NumericUpDown{Minimum=0,Maximum=10,DecimalPlaces=2,Increment=.1M,Width=60},radius=new NumericUpDown{Minimum=0,Maximum=10,DecimalPlaces=2,Increment=.1M,Width=60};
        readonly ComboBox mask=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=95};
        readonly Label shown=new Label{AutoSize=true,Margin=new Padding(8,7,0,0)},state=new Label{Dock=DockStyle.Fill,AutoEllipsis=true};
        const string FilterAll="Tất cả phòng",FilterCheck="Cần kiểm tra",FilterChanged="Đã sửa, chưa lưu";
        static readonly CultureInfo Vietnamese=new CultureInfo("vi-VN");
        static void Field(FlowLayoutPanel p,string text,Control c){p.Controls.Add(new Label{Text=text,AutoSize=true,Margin=new Padding(6,7,3,0)});p.Controls.Add(c);}
        static void Guard(Action a){try{a();}catch(Exception e){MessageBox.Show(e.Message,"Mr.T quản lý phòng",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
        static bool NeedsCheck(RoomBlockInfo r){return !String.IsNullOrWhiteSpace(r.Note)||r.Status==RoomCatalog.StatusSplit||r.Status==RoomCatalog.StatusNotInExcel||r.Status==RoomCatalog.StatusPending;}

        public RoomManagerForm(List<RoomBlockInfo> rows,IEnumerable<RoomBlockInfo> fromDrawing,string note)
        {
            UiTheme.Attach(this);Rows=rows;original=fromDrawing.ToDictionary(r=>r.Handle,r=>r.Copy(),StringComparer.OrdinalIgnoreCase);
            Text="Mr.T Quản lý phòng | 2.0.2.5";Font=UiStyle.Body();Size=new Size(1360,800);MinimumSize=new Size(1000,600);StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=6};
            foreach(int px in new[]{36,40,-1,74,46,30})root.RowStyles.Add(px<0?new RowStyle(SizeType.Percent,100):new RowStyle(SizeType.Absolute,px));
            Controls.Add(root);
            root.Controls.Add(new Label{Text="Quản lý phòng đã đặt",Font=UiStyle.Emphasis(17),Dock=DockStyle.Fill},0,0);
            var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
            Field(bar,"Tìm",query);Field(bar,"Hiện",filter);bar.Controls.Add(shown);
            filter.Items.AddRange(new object[]{FilterAll,FilterCheck,FilterChanged});filter.SelectedIndex=0;
            new ToolTip().SetToolTip(query,"Gõ không dấu, nhiều từ; tìm trong mọi cột. -từ để loại. Bấm tiêu đề cột để sắp xếp.");
            root.Controls.Add(bar,0,1);
            UiStyle.Grid(grid);grid.ReadOnly=false;
            Action<string,string,int,bool> add=(prop,title,w,ro)=>grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName=prop,HeaderText=title,Width=w,ReadOnly=ro,SortMode=DataGridViewColumnSortMode.Automatic});
            add("Id","Mã phòng",90,true);add("Name","Tên hiển thị",220,false);add("OldName","Tên cũ",110,false);add("NewName","Tên mới",180,false);
            add("Status","Trạng thái",100,false);add("Note","Ghi chú",200,false);add("Layer","Layer",150,false);add("Height","Text height",80,false);add("Width","Rộng MTEXT",85,false);add("Color","Màu",50,false);
            var frameColumn=new DataGridViewComboBoxColumn{DataPropertyName="Frame",HeaderText="Khung",Width=140,FlatStyle=FlatStyle.Flat,SortMode=DataGridViewColumnSortMode.Automatic};frameColumn.Items.AddRange(RoomLabelStyle.Frames);grid.Columns.Add(frameColumn);
            add("Pad","Lề (× Text height)",90,false);add("Radius","Bo góc (× Text height)",95,false);
            grid.Columns.Add(new DataGridViewCheckBoxColumn{DataPropertyName="Mask",HeaderText="Nền che",Width=60,SortMode=DataGridViewColumnSortMode.Automatic});
            add("Space","Không gian",80,true);add("Handle","Handle",70,true);
            grid.CellFormatting+=(s,e)=>{
                if(e.RowIndex<0)return;var r=grid.Rows[e.RowIndex].DataBoundItem as RoomBlockInfo;if(r==null)return;
                if(r.Changed)e.CellStyle.BackColor=Color.FromArgb(255,244,214);
                if(grid.Columns[e.ColumnIndex].DataPropertyName=="Note"&&NeedsCheck(r))e.CellStyle.ForeColor=Color.FromArgb(190,30,30);
            };
            grid.CellValueChanged+=(s,e)=>{if(e.RowIndex<0)return;var r=grid.Rows[e.RowIndex].DataBoundItem as RoomBlockInfo;if(r!=null)Mark(r);};
            grid.CurrentCellDirtyStateChanged+=delegate{if(grid.IsCurrentCellDirty&&grid.CurrentCell is DataGridViewCheckBoxCell)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
            grid.DataError+=(s,e)=>{e.ThrowException=false;state.Text="Giá trị không hợp lệ ở cột "+grid.Columns[e.ColumnIndex].HeaderText+": "+e.Exception.Message;};
            root.Controls.Add(grid,0,2);
            var bulk=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true,AutoScroll=true};
            frame.Items.Add("(giữ nguyên)");frame.Items.AddRange(RoomLabelStyle.Frames);frame.SelectedIndex=0;
            mask.Items.AddRange(new object[]{"(giữ nguyên)","Có","Không"});mask.SelectedIndex=0;
            Field(bulk,"Text height",height);Field(bulk,"Rộng MTEXT (-1 giữ, 0 tự do)",width);Field(bulk,"Màu",color);Field(bulk,"Khung",frame);Field(bulk,"Lề (× Text height)",pad);Field(bulk,"Bo góc (× Text height)",radius);Field(bulk,"Nền che",mask);
            var apply=new Button{Text="Áp cho dòng đã chọn",AutoSize=true};var upper=new Button{Text="CHỮ HOA tên hiển thị",AutoSize=true};bulk.Controls.Add(apply);bulk.Controls.Add(upper);
            new ToolTip().SetToolTip(apply,"Text height/Màu để 0, Rộng MTEXT để -1 hoặc (giữ nguyên) thì không đổi. Rộng MTEXT 0 = tự do. Lề và Bo góc chỉ áp khi chọn Khung.");
            root.Controls.Add(bulk,0,3);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};
            var close=new Button{Text="Đóng",AutoSize=true,DialogResult=DialogResult.Cancel};var save=new Button{Text="Lưu vào bản vẽ",AutoSize=true};
            var zoom=new Button{Text="Phóng tới phòng…",AutoSize=true};var export=new Button{Text="Xuất Excel hiện trạng…",AutoSize=true};var import=new Button{Text="Nhập Excel đã sửa…",AutoSize=true};
            var revert=new Button{Text="Bỏ sửa dòng chọn",AutoSize=true};
            actions.Controls.AddRange(new Control[]{close,save,zoom,import,export,revert});root.Controls.Add(actions,0,4);root.Controls.Add(state,0,5);
            CancelButton=close;state.Text=note;
            query.TextChanged+=delegate{Refill();};filter.SelectedIndexChanged+=delegate{Refill();};
            apply.Click+=delegate{Guard(()=>{
                var chosen=Selected();if(chosen.Count==0)throw new ArgumentException("Chọn một hoặc nhiều dòng.");
                foreach(var r in chosen)
                {
                    if(height.Value>0)r.Height=(double)height.Value;if(width.Value>=0)r.Width=(double)width.Value;if(color.Value>0)r.Color=(int)color.Value;
                    if(frame.SelectedIndex>0){r.Frame=(string)frame.SelectedItem;r.Pad=(double)pad.Value;r.Radius=(double)radius.Value;}
                    if(mask.SelectedIndex>0)r.Mask=mask.SelectedIndex==1;Mark(r);
                }
                grid.Refresh();Count();});};
            upper.Click+=delegate{Guard(()=>{foreach(var r in Selected()){r.Name=(r.Name??"").ToUpper(Vietnamese);Mark(r);}grid.Refresh();Count();});};
            revert.Click+=delegate{Guard(()=>{foreach(var r in Selected()){RoomBlockInfo o;if(!original.TryGetValue(r.Handle,out o))continue;int i=Rows.IndexOf(r);Rows[i]=o.Copy();}Refill();});};
            zoom.Click+=delegate{var r=Selected().FirstOrDefault();if(r==null)return;grid.EndEdit();ZoomHandle=r.Handle;DialogResult=DialogResult.Retry;};
            export.Click+=delegate{Guard(()=>{grid.EndEdit();using(var d=new SaveFileDialog{Filter="Excel (*.xlsx)|*.xlsx",FileName="Hien trang phong.xlsx",Title="Xuất hiện trạng phòng"})
                if(d.ShowDialog(this)==DialogResult.OK){RoomManagerFile.Export(d.FileName,Rows);state.Text="Đã xuất "+Rows.Count+" phòng ra "+d.FileName+". Sửa trong Excel rồi dùng Nhập Excel đã sửa.";}});};
            import.Click+=delegate{Guard(()=>{grid.EndEdit();using(var d=new OpenFileDialog{Filter="Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm",Title="Nhập hiện trạng phòng đã sửa"})
                if(d.ShowDialog(this)==DialogResult.OK){var problems=new List<string>();int n=RoomManagerFile.Import(d.FileName,Rows,problems);Refill();
                    state.Text="Đã nhận "+n+" phòng thay đổi từ Excel (chưa lưu vào bản vẽ)."+(problems.Count>0?" "+problems.Count+" dòng bỏ qua: "+String.Join(" | ",problems.Take(3)):"");
                    if(problems.Count>3)MessageBox.Show(String.Join("\r\n",problems),"Dòng Excel bỏ qua");}});};
            save.Click+=delegate{Guard(()=>{grid.EndEdit();var bad=Rows.Where(r=>r.Changed).Select(r=>new{r,p=r.Problem()}).FirstOrDefault(x=>x.p!=null);
                if(bad!=null)throw new ArgumentException("Phòng "+bad.r.Id+": "+bad.p);if(!Rows.Any(r=>r.Changed))throw new ArgumentException("Chưa có thay đổi.");DialogResult=DialogResult.OK;});};
            FormClosing+=(s,e)=>{if(DialogResult==DialogResult.Cancel&&Rows.Any(r=>r.Changed)&&MessageBox.Show("Bỏ các thay đổi chưa lưu?","Mr.T quản lý phòng",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)e.Cancel=true;};
            Refill();
        }
        void Mark(RoomBlockInfo r){RoomBlockInfo o;r.Changed=!original.TryGetValue(r.Handle,out o)||!r.SameAs(o);}
        List<RoomBlockInfo> Selected(){return grid.SelectedRows.Cast<DataGridViewRow>().Select(x=>x.DataBoundItem as RoomBlockInfo).Where(x=>x!=null).ToList();}
        bool Keep(RoomBlockInfo r)
        {
            if((string)filter.SelectedItem==FilterCheck&&!NeedsCheck(r))return false;
            if((string)filter.SelectedItem==FilterChanged&&!r.Changed)return false;
            return RoomSearch.Matches(query.Text,r.Id,r.Name,r.OldName,r.NewName,r.Status,r.Note,r.Layer,r.Handle,r.Space);
        }
        void Refill(){GridSort.Bind(grid,Rows.Where(Keep).ToList());Count();}
        void Count(){shown.Text="Hiển thị "+grid.Rows.Count+"/"+Rows.Count+" phòng · cần kiểm tra "+Rows.Count(NeedsCheck)+" · đã sửa "+Rows.Count(r=>r.Changed);}
    }
}
