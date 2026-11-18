using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;
namespace MedGas2024.UI
{
    public sealed class RoomForm:Form
    {
        readonly Func<RoomEntry,double,bool,bool> place;readonly Action<RoomCatalog,bool> update;
        readonly TextBox file=new TextBox{ReadOnly=true,Width=360},search=new TextBox{Width=230};
        readonly ComboBox sheet=Combo(),oldCol=Combo(),newCol=Combo(),idCol=Combo();
        readonly NumericUpDown header=new NumericUpDown{Minimum=1,Maximum=100000,Value=1,Width=60},height=new CompactNumber{Minimum=.00001M,Maximum=1000000000,Value=250,DecimalPlaces=5,Width=105};
        readonly CheckBox showNew=new CheckBox{Text="Tên mới",AutoSize=true};
        readonly DataGridView grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AutoGenerateColumns=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
        readonly Label state=new Label{Dock=DockStyle.Fill,AutoEllipsis=true};RoomWorkbook book;RoomCatalog catalog;bool loading;
        static ComboBox Combo(){return new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=200};}
        static void Field(FlowLayoutPanel p,string text,Control c){p.Controls.Add(new Label{Text=text,AutoSize=true,Margin=new Padding(6,7,3,0)});p.Controls.Add(c);}
        static void Guard(Action a){try{a();}catch(Exception e){MessageBox.Show(e.Message,"Mr.T tên phòng",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
        public RoomForm(Func<RoomEntry,double,bool,bool> insert,Action<RoomCatalog,bool> apply)
        {UiTheme.Attach(this);
            place=insert;update=apply;Text="Mr.T Tên phòng | 2.0.2.0";Font=UiStyle.Body();Size=new Size(1180,760);MinimumSize=new Size(950,620);StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=6};root.RowStyles.Add(new RowStyle(SizeType.Absolute,36));root.RowStyles.Add(new RowStyle(SizeType.Absolute,39));root.RowStyles.Add(new RowStyle(SizeType.Absolute,80));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,52));root.RowStyles.Add(new RowStyle(SizeType.Absolute,62));Controls.Add(root);
            root.Controls.Add(new Label{Text="TÊN PHÒNG CŨ / MỚI",Font=UiStyle.Emphasis(17),Dock=DockStyle.Fill},0,0);
            var input=new FlowLayoutPanel{Dock=DockStyle.Fill};Field(input,"Excel:",file);var browse=new Button{Text="Chọn Excel...",AutoSize=true};input.Controls.Add(browse);Field(input,"Sheet:",sheet);Field(input,"Dòng tiêu đề:",header);root.Controls.Add(input,0,1);
            var mapping=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};Field(mapping,"Tên cũ:",oldCol);Field(mapping,"Tên mới:",newCol);Field(mapping,"ID:",idCol);var load=new Button{Text="Nạp / kiểm tra danh sách",AutoSize=true};mapping.Controls.Add(load);var issues=new Button{Text="Xem lỗi Excel",AutoSize=true};mapping.Controls.Add(issues);mapping.Controls.Add(new Label{Text="Mặc định ID = tên cũ ban đầu. Excel số/mã cần lưu dạng văn bản.",AutoSize=true,Margin=new Padding(8,7,0,0)});root.Controls.Add(mapping,0,2);
            UiStyle.Grid(grid);grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="Id",HeaderText="ID / tên gốc",Width=230});grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="OldName",HeaderText="Tên cũ",Width=300});grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="NewName",HeaderText="Tên mới",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill});grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="ExcelRow",HeaderText="Dòng Excel",Width=85});root.Controls.Add(grid,0,3);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill};Field(actions,"Tìm:",search);Field(actions,"Cao chữ (đơn vị DWG):",height);actions.Controls.Add(showNew);var add=new Button{Text="Gắn phòng tại điểm",AutoSize=true};var run=new Button{Text="Xem trước / cập nhật DWG",AutoSize=true};var close=new Button{Text="Đóng",AutoSize=true};actions.Controls.Add(add);actions.Controls.Add(run);actions.Controls.Add(close);root.Controls.Add(actions,0,4);root.Controls.Add(state,0,5);
            state.Text="Chọn Excel, sheet và cột. Sau đó chọn một dòng và gắn tại vị trí; hoặc chuyển tên các phòng đã gắn. Không sửa nội dung Excel.";
            browse.Click+=delegate{Guard(()=>{using(var d=new OpenFileDialog{Filter="Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm",Title="Chọn danh sách tên phòng"})if(d.ShowDialog(this)==DialogResult.OK){var loaded=RoomWorkbook.Read(d.FileName);book=loaded;file.Text=d.FileName;loading=true;try{sheet.Items.Clear();foreach(var s in book.Sheets)sheet.Items.Add(s.Name);sheet.SelectedIndex=0;}finally{loading=false;}SetColumns();}});};
            sheet.SelectedIndexChanged+=delegate{if(!loading)Guard(SetColumns);};header.ValueChanged+=delegate{if(!loading&&book!=null)Guard(SetColumns);};
            oldCol.SelectedIndexChanged+=delegate{if(!loading)InvalidateCatalog();};newCol.SelectedIndexChanged+=delegate{if(!loading)InvalidateCatalog();};idCol.SelectedIndexChanged+=delegate{if(!loading)InvalidateCatalog();};
            load.Click+=delegate{Guard(()=>{if(book==null||sheet.SelectedIndex<0)throw new ArgumentException("Chọn file Excel trước.");catalog=RoomWorkbook.Catalog(book.Sheets[sheet.SelectedIndex],(int)header.Value,oldCol.SelectedIndex+1,newCol.SelectedIndex+1,idCol.SelectedIndex);RefreshRows();state.Text="Đã nạp "+catalog.Unique.Count+" ID duy nhất; "+catalog.Ambiguous.Count+" ID xung đột; "+catalog.Problems.Count+" cảnh báo. "+String.Join(" | ",catalog.Problems.Take(4));});};
            issues.Click+=delegate{Guard(()=>{if(catalog==null)throw new ArgumentException("Hãy nạp danh sách Excel trước.");using(var dialog=new Form{Text="Chi tiết lỗi Excel",Size=new Size(850,500),StartPosition=FormStartPosition.CenterParent}){var text=new TextBox{Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Text=catalog.Problems.Count==0?"Không có cảnh báo.":String.Join("\r\n",catalog.Problems)};dialog.Controls.Add(text);dialog.ShowDialog(this);}});};
            search.TextChanged+=delegate{RefreshRows();};
            add.Click+=delegate{Guard(()=>{if(catalog==null)throw new ArgumentException("Hãy nạp danh sách Excel.");var current=grid.CurrentRow;var row=current==null?null:current.DataBoundItem as RoomEntry;if(row==null)throw new ArgumentException("Chọn một dòng phòng.");int index=current.Index;if(place(row,(double)height.Value,showNew.Checked)&&index+1<grid.Rows.Count)grid.CurrentCell=grid.Rows[index+1].Cells[0];});};
            run.Click+=delegate{Guard(()=>{if(book!=null&&catalog==null)throw new InvalidOperationException("Thông số Excel đã thay đổi; hãy bấm Nạp / kiểm tra danh sách trước khi cập nhật.");update(catalog,showNew.Checked);});};close.Click+=delegate{Close();};
        }
        void InvalidateCatalog(){catalog=null;grid.DataSource=null;state.Text="Thông số nhập thay đổi; bấm Nạp / kiểm tra danh sách. Khi chưa nạp, nút cập nhật dùng dữ liệu đã lưu trong DWG.";}
        void SetColumns()
        {
            if(book==null||sheet.SelectedIndex<0)return;loading=true;try{var s=book.Sheets[sheet.SelectedIndex];oldCol.Items.Clear();newCol.Items.Clear();idCol.Items.Clear();idCol.Items.Add("Tên cũ ban đầu (mặc định)");for(int c=1;c<=s.Columns;c++){string name=RoomWorkbook.ColumnName(c)+" — "+s.Cell((int)header.Value,c).Text;oldCol.Items.Add(name);newCol.Items.Add(name);idCol.Items.Add(name);}if(s.Columns>0){oldCol.SelectedIndex=0;newCol.SelectedIndex=Math.Min(1,s.Columns-1);for(int c=1;c<=s.Columns;c++){string title=s.Cell((int)header.Value,c).Text.ToLowerInvariant();if(title.Contains("cũ")||title.Contains("cu"))oldCol.SelectedIndex=c-1;if(title.Contains("mới")||title.Contains("moi"))newCol.SelectedIndex=c-1;}}idCol.SelectedIndex=0;}finally{loading=false;}InvalidateCatalog();
        }
        void RefreshRows(){if(catalog==null){grid.DataSource=null;return;}string q=search.Text.Trim();grid.DataSource=catalog.Unique.Values.Where(r=>q.Length==0||(r.Id??"").IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0||(r.NewName??"").IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0).OrderBy(r=>r.ExcelRow).ToList();}
    }
    public sealed class RoomReviewForm:Form
    {
        public RoomReviewForm(List<RoomReviewRow> rows,string summary)
        {UiTheme.Attach(this);
            Text="Xem trước cập nhật tên phòng";Font=UiStyle.Body();Size=new Size(1100,670);MinimumSize=new Size(800,520);StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=3};root.RowStyles.Add(new RowStyle(SizeType.Absolute,60));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,45));Controls.Add(root);root.Controls.Add(new Label{Text=summary,Dock=DockStyle.Fill},0,0);
            var grid=new DataGridView{Dock=DockStyle.Fill,AutoGenerateColumns=false,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,DataSource=rows};UiStyle.Grid(grid);foreach(var col in new[]{new[]{"Handle","Handle","70"},new[]{"Id","ID gốc","190"},new[]{"Current","Hiện tại","220"},new[]{"After","Sau cập nhật","220"},new[]{"Status","Kết quả / lý do","260"}})grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName=col[0],HeaderText=col[1],Width=Int32.Parse(col[2])});root.Controls.Add(grid,0,1);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text="Hủy",AutoSize=true,DialogResult=DialogResult.Cancel};var ok=new Button{Text="Áp dụng dòng hợp lệ",AutoSize=true,DialogResult=DialogResult.OK,Enabled=rows.Any(r=>r.Status=="Sẵn sàng"||r.Status.StartsWith("Đã đúng"))};buttons.Controls.Add(cancel);buttons.Controls.Add(ok);root.Controls.Add(buttons,0,2);AcceptButton=ok;CancelButton=cancel;
        }
    }
}
