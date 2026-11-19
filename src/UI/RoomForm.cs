using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;
namespace MedGas2024.UI
{
    public sealed class RoomForm:Form
    {
        public const string AllLayers="(Tất cả layer nội bộ)";
        public event Action InspectTag;
        public event Action ManageRooms;
        readonly SearchCombo sourceLayer=new SearchCombo(),targetLayer=new SearchCombo{AllowNew=true},targetStyle=new SearchCombo();
        readonly TextBox file=new TextBox{ReadOnly=true,Width=280},search=new TextBox{Width=230};
        readonly ComboBox sheet=Combo(),oldCol=Combo(),newCol=Combo(),idCol=Combo(),frame=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=150};
        readonly NumericUpDown header=new NumericUpDown{Minimum=1,Maximum=100000,Value=1,Width=60},
            height=new CompactNumber{Minimum=.00001M,Maximum=1000000000,Value=250,DecimalPlaces=5,Width=95},
            width=new CompactNumber{Minimum=0,Maximum=1000000000,Value=0,DecimalPlaces=5,Width=95},
            pad=new NumericUpDown{Minimum=0,Maximum=5,DecimalPlaces=2,Increment=.1M,Value=.4M,Width=60},
            radius=new NumericUpDown{Minimum=0,Maximum=5,DecimalPlaces=2,Increment=.1M,Value=.6M,Width=60},
            color=new NumericUpDown{Minimum=1,Maximum=255,Value=158,Width=60},reviewColor=new NumericUpDown{Minimum=1,Maximum=255,Value=1,Width=60};
        readonly CheckBox showNew=new CheckBox{Text="Hiển thị tên mới",AutoSize=true},upper=new CheckBox{Text="CHỮ HOA",AutoSize=true,Checked=true},
            replace=new CheckBox{Text="Gộp chữ cũ ngoài block vào block mới",AutoSize=true,Checked=true},mask=new CheckBox{Text="Nền che (wipeout theo khung)",AutoSize=true};
        readonly DataGridView grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AutoGenerateColumns=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
        readonly Label state=new Label{Dock=DockStyle.Fill,AutoEllipsis=true},count=new Label{AutoSize=true,Margin=new Padding(8,7,0,0)};
        RoomWorkbook book;RoomCatalog catalog;bool loading;
        readonly RoomSettings settings;readonly Action<RoomSettings> save;
        static ComboBox Combo(){return new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=185,DropDownWidth=390};}
        static void Field(FlowLayoutPanel p,string text,Control c){p.Controls.Add(new Label{Text=text,AutoSize=true,Margin=new Padding(6,7,3,0)});p.Controls.Add(c);}
        static void Guard(Action a){try{a();}catch(Exception e){MessageBox.Show(e.Message,"Mr.T tên phòng",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
        public RoomForm(Func<RoomEntry,double,bool,bool> insert,Action<RoomCatalog,bool> apply)
            :this((r,m,s)=>insert(r,s.Height,m),apply,new string[0],new string[0],null,null){}
        public RoomForm(Func<RoomEntry,bool,RoomLabelStyle,bool> place,Action<RoomCatalog,bool> update,
            string[] layers,string[] styles,Func<RoomEntry,bool,string,RoomLabelStyle,bool> find,
            Action<RoomCatalog,bool,string,RoomLabelStyle,RoomSettings> scan,RoomSettings remembered=null,Action<RoomSettings> store=null)
        {
            UiTheme.Attach(this);settings=remembered??new RoomSettings();save=store;
            Text="Mr.T Tên phòng | 2.0.2.6";Font=UiStyle.Body();Size=new Size(1240,840);
            MinimumSize=new Size(1000,720);StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=9};
            foreach(int pixels in new[]{36,39,80,-1,46,74,46,46,46})
                root.RowStyles.Add(pixels<0?new RowStyle(SizeType.Percent,100):new RowStyle(SizeType.Absolute,pixels));
            Controls.Add(root);
            root.Controls.Add(new Label{Text="Tên phòng cũ / mới",Font=UiStyle.Emphasis(17),Dock=DockStyle.Fill},0,0);
            var input=new FlowLayoutPanel{Dock=DockStyle.Fill};Field(input,"Excel:",file);var browse=new Button{Text="Chọn Excel...",AutoSize=true};input.Controls.Add(browse);Field(input,"Trang tính",sheet);Field(input,"Dòng tiêu đề:",header);root.Controls.Add(input,0,1);
            var mapping=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true,AutoScroll=true};Field(mapping,"Cột Excel chứa tên cũ",oldCol);Field(mapping,"Cột Excel chứa tên mới",newCol);Field(mapping,"Cột mã phòng (ID)",idCol);var load=new Button{Text="Nạp / kiểm tra danh sách",AutoSize=true};mapping.Controls.Add(load);var issues=new Button{Text="Xem lỗi Excel",AutoSize=true};mapping.Controls.Add(issues);mapping.Controls.Add(new Label{Text="Mặc định ID = tên cũ ban đầu. Một mã cũ có nhiều tên mới = tách phòng (ID 402-1, 402-2…).",AutoSize=true,Margin=new Padding(8,7,0,0)});root.Controls.Add(mapping,0,2);
            UiStyle.Grid(grid);grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="Id",HeaderText="ID / tên gốc",Width=200});grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="OldName",HeaderText="Tên cũ",Width=260});grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="NewName",HeaderText="Tên mới",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill});grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="Status",HeaderText="Trạng thái",Width=110});grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="ExcelRow",HeaderText="Dòng Excel",Width=85});root.Controls.Add(grid,0,3);
            foreach(DataGridViewColumn c in grid.Columns)c.SortMode=DataGridViewColumnSortMode.Automatic;
            var layerRow=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true};
            sourceLayer.SetChoices(new[]{AllLayers}.Concat(layers),AllLayers);
            targetLayer.SetChoices(new[]{RoomCatalog.NewLayer,RoomCatalog.DefaultLayer}.Concat(layers),RoomCatalog.NewLayer);
            targetStyle.SetChoices(new[]{RoomCatalog.DefaultStyle}.Concat(styles),RoomCatalog.DefaultStyle);
            // Old names on the target layer: move the target to a separate layer so the old one can be turned off.
            sourceLayer.ValueChanged+=delegate{var s=SourceLayer;if(s!=null&&String.Equals(s,targetLayer.Value,StringComparison.OrdinalIgnoreCase))
                targetLayer.Choose(String.Equals(s,RoomCatalog.NewLayer,StringComparison.OrdinalIgnoreCase)?RoomCatalog.DefaultLayer:RoomCatalog.NewLayer);};
            Field(layerRow,"Layer chữ cũ",sourceLayer);Field(layerRow,"Layer tên phòng mới",targetLayer);Field(layerRow,"Kiểu chữ",targetStyle);
            root.Controls.Add(layerRow,0,4);
            var look=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true,AutoScroll=true};
            frame.Items.AddRange(RoomLabelStyle.Frames);frame.SelectedIndex=0;
            Field(look,"Text height",height);Field(look,"Rộng MTEXT (0 = tự do)",width);look.Controls.Add(upper);look.Controls.Add(mask);
            Field(look,"Khung",frame);Field(look,"Lề chữ–khung (× Text height)",pad);Field(look,"Bo góc (× Text height)",radius);Field(look,"Màu",color);Field(look,"Màu cần kiểm tra",reviewColor);look.Controls.Add(replace);
            root.Controls.Add(look,0,5);
            var options=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true};
            Field(options,"Tìm trong danh sách Excel",search);options.Controls.Add(count);options.Controls.Add(showNew);root.Controls.Add(options,0,6);
            var tips=new ToolTip();tips.SetToolTip(search,"Gõ không dấu, nhiều từ (mọi từ đều phải có); thêm -từ để loại. Bấm tiêu đề cột để sắp xếp.");
            tips.SetToolTip(replace,"Chữ tên phòng cũ nằm ngoài block được thay bằng block Mr.T_PHONG; nội dung cũ lưu ở thuộc tính TEN_CU. Chữ nằm trong block dùng chung được giữ nguyên.");
            tips.SetToolTip(sourceLayer,"Gõ vài chữ (không cần dấu) rồi bấm chọn trong danh sách, hoặc dùng mũi tên lên/xuống và Enter.");
            tips.SetToolTip(targetLayer,"Chọn hoặc gõ tên layer; layer chưa có sẽ được tạo khi áp dụng.");
            tips.SetToolTip(reviewColor,"Màu chỉ số AutoCAD cho phòng cần kiểm tra: tách phòng gán theo vị trí, tách xếp chồng, mã trùng nhiều vị trí. Lý do ghi ở thuộc tính GHI_CHU.");
            tips.SetToolTip(frame,"Khung là polyline kín cùng layer tên phòng mới, gom nhóm với block và tự vẽ lại khi đổi tên.");
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true};
            var add=new Button{Text="Gắn tại điểm",AutoSize=true};
            var locate=new Button{Text="Tìm chữ trong DWG…",AutoSize=true,Enabled=find!=null};
            var auto=new Button{Text="Rà soát layer và gắn…",AutoSize=true,Enabled=scan!=null};
            var run=new Button{Text="Cập nhật phòng đã gắn…",AutoSize=true};
            var close=new Button{Text="Đóng",AutoSize=true};
            var info=new Button{Text="Xem ID của chữ…",AutoSize=true};info.Click+=delegate{if(InspectTag!=null)Guard(InspectTag);};
            var manage=new Button{Text="Quản lý phòng đã đặt…",AutoSize=true};manage.Click+=delegate{if(ManageRooms!=null){Remember();Guard(ManageRooms);}};
            actions.Controls.AddRange(new Control[]{add,locate,auto,run,manage,info,close});root.Controls.Add(actions,0,7);root.Controls.Add(state,0,8);
            state.Text="Chọn Excel và nạp danh sách. Chọn một phòng để tìm chữ hoặc gắn tại điểm. Để gắn hàng loạt, chọn layer chữ cũ rồi rà soát.";
            locate.Click+=delegate{Guard(()=>{var row=SelectedRoom();Remember();if(find(row,showNew.Checked,SourceLayer,LabelStyle))NextRoom();});};
            auto.Click+=delegate{Guard(()=>{if(catalog==null)throw new ArgumentException("Hãy nạp danh sách Excel.");
                Remember();scan(catalog,showNew.Checked,SourceLayer,LabelStyle,settings);Remember();});};
            browse.Click+=delegate{Guard(()=>{using(var d=new OpenFileDialog{Filter="Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm",Title="Chọn danh sách tên phòng"}){if(File.Exists(file.Text))d.InitialDirectory=Path.GetDirectoryName(file.Text);if(d.ShowDialog(this)==DialogResult.OK)OpenWorkbook(d.FileName,null);}});};
            sheet.SelectedIndexChanged+=delegate{if(!loading)Guard(SelectSheet);};header.ValueChanged+=delegate{if(!loading&&book!=null)Guard(SetColumns);};
            oldCol.SelectedIndexChanged+=delegate{if(!loading)InvalidateCatalog();};newCol.SelectedIndexChanged+=delegate{if(!loading)InvalidateCatalog();};idCol.SelectedIndexChanged+=delegate{if(!loading)InvalidateCatalog();};
            load.Click+=delegate{Guard(LoadCatalog);};
            issues.Click+=delegate{Guard(()=>{if(catalog==null)throw new ArgumentException("Hãy nạp danh sách Excel trước.");using(var dialog=new Form{Text="Chi tiết lỗi Excel",Size=new Size(850,500),StartPosition=FormStartPosition.CenterParent}){var text=new TextBox{Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Text=catalog.Problems.Count==0?"Không có cảnh báo.":String.Join("\r\n",catalog.Problems)};dialog.Controls.Add(text);dialog.ShowDialog(this);}});};
            search.TextChanged+=delegate{RefreshRows();};
            add.Click+=delegate{Guard(()=>{Remember();if(place(SelectedRoom(),showNew.Checked,LabelStyle))NextRoom();});};
            run.Click+=delegate{Guard(()=>{if(book!=null&&catalog==null)throw new InvalidOperationException("Thông số Excel đã thay đổi; hãy bấm Nạp / kiểm tra danh sách trước khi cập nhật.");Remember();update(catalog,showNew.Checked);});};close.Click+=delegate{Close();};
            FormClosing+=delegate{Remember();};
            Restore();
        }
        public RoomLabelStyle LabelStyle
        {
            get
            {
                return new RoomLabelStyle{Layer=targetLayer.Value,Style=targetStyle.Value,Height=(double)height.Value,Width=(double)width.Value,Upper=upper.Checked,
                    Mask=mask.Checked,Frame=(string)frame.SelectedItem,Pad=(double)pad.Value,Radius=(double)radius.Value,ReplaceOld=replace.Checked,Color=(int)color.Value,ReviewColor=(int)reviewColor.Value}.Valid();
            }
        }
        // Puts the remembered values back: label look, layers, Excel file, sheet, columns, then loads the list.
        void Restore()
        {
            var s=settings.Label==null?new RoomLabelStyle():settings.Label.Valid();
            Func<double,decimal,decimal,decimal> clamp=(v,min,max)=>Math.Max(min,Math.Min(max,(decimal)v));
            height.Value=clamp(s.Height,height.Minimum,height.Maximum);width.Value=clamp(s.Width,width.Minimum,width.Maximum);
            pad.Value=clamp(s.Pad,pad.Minimum,pad.Maximum);radius.Value=clamp(s.Radius,radius.Minimum,radius.Maximum);color.Value=s.Color;reviewColor.Value=s.ReviewColor;
            upper.Checked=s.Upper;mask.Checked=s.Mask;replace.Checked=s.ReplaceOld;frame.SelectedItem=s.Frame;showNew.Checked=settings.ShowNew;
            targetStyle.Choose(s.Style);targetLayer.Choose(s.Layer);
            if(!String.IsNullOrEmpty(settings.SourceLayer))sourceLayer.Choose(settings.SourceLayer);
            if(String.IsNullOrEmpty(settings.ExcelPath)||!File.Exists(settings.ExcelPath))return;
            try
            {
                OpenWorkbook(settings.ExcelPath,settings.Sheet);
                loading=true;
                try
                {
                    if(settings.HeaderRow>0&&settings.HeaderRow<=header.Maximum&&settings.HeaderRow!=(int)header.Value){header.Value=settings.HeaderRow;loading=false;SetColumns();loading=true;}
                    if(settings.OldColumn>=0&&settings.OldColumn<oldCol.Items.Count)oldCol.SelectedIndex=settings.OldColumn;
                    if(settings.NewColumn>=0&&settings.NewColumn<newCol.Items.Count)newCol.SelectedIndex=settings.NewColumn;
                    if(settings.IdColumn>=0&&settings.IdColumn<idCol.Items.Count)idCol.SelectedIndex=settings.IdColumn;
                }
                finally{loading=false;}
                LoadCatalog();
            }
            catch(Exception ex){state.Text="Không mở lại được Excel đã dùng lần trước: "+ex.Message;}
        }
        // Saves the current choices for next time.
        void Remember()
        {
            settings.ExcelPath=file.Text;settings.Sheet=sheet.SelectedItem as string;settings.HeaderRow=(int)header.Value;
            settings.OldColumn=oldCol.SelectedIndex;settings.NewColumn=newCol.SelectedIndex;settings.IdColumn=idCol.SelectedIndex;
            settings.SourceLayer=SourceLayer;settings.ShowNew=showNew.Checked;settings.Label=LabelStyle;
            if(save!=null)save(settings);
        }
        void OpenWorkbook(string path,string sheetName)
        {
            var loaded=RoomWorkbook.Read(path);book=loaded;file.Text=path;loading=true;
            try{sheet.Items.Clear();foreach(var s in book.Sheets)sheet.Items.Add(s.Name);int i=sheetName==null?-1:sheet.Items.IndexOf(sheetName);sheet.SelectedIndex=i>=0?i:0;}
            finally{loading=false;}
            SelectSheet();
        }
        void LoadCatalog()
        {
            if(book==null||sheet.SelectedIndex<0)throw new ArgumentException("Chọn file Excel trước.");
            catalog=RoomWorkbook.Catalog(book.Sheets[sheet.SelectedIndex],(int)header.Value,oldCol.SelectedIndex+1,newCol.SelectedIndex+1,idCol.SelectedIndex);RefreshRows();
            int splits=catalog.Unique.Values.Count(r=>r.SplitOf!=null),merges=catalog.Unique.Values.Count(r=>r.Status==RoomCatalog.StatusMerge);
            state.Text="Đã nạp "+catalog.Unique.Count+" phòng ("+splits+" từ tách, "+merges+" ghép); "+catalog.Ambiguous.Count+" ID xung đột; "+catalog.Problems.Count+" ghi chú/cảnh báo. "+String.Join(" | ",catalog.Problems.Take(3));
        }
        string SourceLayer{get{var v=sourceLayer.Value;return v==AllLayers||String.IsNullOrEmpty(v)?null:v;}}
        RoomEntry SelectedRoom()
        {
            var row=grid.CurrentRow==null?null:grid.CurrentRow.DataBoundItem as RoomEntry;
            if(row==null)throw new ArgumentException("Nạp Excel và chọn một phòng trong danh sách.");
            return row;
        }
        void NextRoom(){var row=grid.CurrentRow;if(row!=null&&row.Index+1<grid.Rows.Count)grid.CurrentCell=grid.Rows[row.Index+1].Cells[0];}
        void InvalidateCatalog(){catalog=null;grid.DataSource=null;count.Text="";state.Text="Thông số nhập thay đổi; bấm Nạp / kiểm tra danh sách.";}
        void SelectSheet()
        {
            if(book==null||sheet.SelectedIndex<0)return;
            int detected=RoomWorkbook.HeaderRow(book.Sheets[sheet.SelectedIndex]);
            loading=true;
            try{header.Value=detected;}finally{loading=false;}
            SetColumns();
        }
        void SetColumns()
        {
            if(book==null||sheet.SelectedIndex<0)return;loading=true;
            try
            {
                var s=book.Sheets[sheet.SelectedIndex];int row=(int)header.Value;
                oldCol.Items.Clear();newCol.Items.Clear();idCol.Items.Clear();idCol.Items.Add("Dùng tên cũ làm ID");
                for(int c=1;c<=s.Columns;c++)
                {
                    string title=s.Cell(row,c).Text.Trim();
                    string example=s.Rows.Where(pair=>pair.Key>row&&pair.Value.ContainsKey(c)).Select(pair=>pair.Value[c].Text)
                        .FirstOrDefault(t=>!String.IsNullOrWhiteSpace(t));
                    string name="Cột "+RoomWorkbook.ColumnName(c)+" — "+(title.Length>0?title:"(trống tiêu đề)")+
                        (String.IsNullOrWhiteSpace(example)?"":" · VD: "+example);
                    oldCol.Items.Add(name);newCol.Items.Add(name);idCol.Items.Add(name);
                }
                if(s.Columns>0)
                {
                    oldCol.SelectedIndex=0;newCol.SelectedIndex=Math.Min(1,s.Columns-1);
                    int oldFound=Detect(s,row,RoomWorkbook.IsOldHeader,-1),newFound=Detect(s,row,RoomWorkbook.IsNewHeader,oldFound);
                    if(oldFound<0)oldFound=Detect(s,row,t=>Words(t).Contains("cu"),-1);
                    if(newFound<0)newFound=Detect(s,row,t=>Words(t).Contains("moi"),oldFound);
                    if(oldFound>0)oldCol.SelectedIndex=oldFound-1;
                    if(newFound>0)newCol.SelectedIndex=newFound-1;
                }
                idCol.SelectedIndex=0;
            }
            finally{loading=false;}
            InvalidateCatalog();
        }
        static string[] Words(string text){return RoomSearch.Fold(text).Split(new[]{' ','/','(',')','-',':'},StringSplitOptions.RemoveEmptyEntries);}
        static int Detect(RoomSheet s,int row,Func<string,bool> test,int except)
        {
            for(int c=1;c<=s.Columns;c++)if(c!=except&&test(s.Cell(row,c).Text))return c;
            return -1;
        }
        void RefreshRows()
        {
            if(catalog==null){grid.DataSource=null;count.Text="";return;}
            string q=search.Text;
            var rows=catalog.Unique.Values.Where(r=>RoomSearch.Matches(q,r.Id,r.OldName,r.NewName,r.ExcelRow.ToString())).OrderBy(r=>r.ExcelRow).ToList();
            GridSort.Bind(grid,rows);
            count.Text=rows.Count+"/"+catalog.Unique.Count+" phòng";
        }
    }
    public sealed class RoomReviewForm:Form
    {
        public RoomReviewRow SelectedRoom {get;private set;}
        public RoomReviewChoices Choices {get;private set;}
        List<RoomReviewRow> rows;
        readonly DataGridView grid=new DataGridView{Dock=DockStyle.Fill,AutoGenerateColumns=false,ReadOnly=true,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,RowHeadersVisible=false};
        readonly TextBox query=new TextBox{Width=280};
        readonly ComboBox filter=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=210};
        readonly Label shown=new Label{AutoSize=true,Margin=new Padding(8,7,0,0)},summaryLabel=new Label{Dock=DockStyle.Fill};
        readonly Button ok=new Button{Text="Áp dụng dòng hợp lệ",AutoSize=true,DialogResult=DialogResult.OK};
        const string FilterAll="Tất cả dòng",FilterReady="Sẵn sàng áp dụng",FilterBlocked="Cần xử lý",FilterMissing="Excel chưa tìm thấy",FilterDone="Đã gắn / đã có nhãn";
        static bool IsReady(RoomReviewRow r){return r.Ready||r.Status=="Sẵn sàng"||(r.Status??"").StartsWith("Đã đúng");}
        public RoomReviewForm(List<RoomReviewRow> rows,string summary,bool placement=false):this(rows,summary,placement,null,null){}
        public RoomReviewForm(List<RoomReviewRow> rows,string summary,RoomReviewChoices choices,Func<RoomReviewChoices,RoomReviewPage> rebuild)
            :this(rows,summary,true,choices,rebuild){}
        RoomReviewForm(List<RoomReviewRow> rows,string summary,bool placement,RoomReviewChoices choices,Func<RoomReviewChoices,RoomReviewPage> rebuild)
        {
            UiTheme.Attach(this);this.rows=rows;Choices=choices;
            Text="Xem trước cập nhật tên phòng";Font=UiStyle.Body();Size=new Size(placement?1280:1100,placement?720:670);MinimumSize=new Size(900,560);StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=5};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,60));root.RowStyles.Add(new RowStyle(SizeType.Absolute,choices!=null?66:0));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,45));
            Controls.Add(root);summaryLabel.Text=summary;root.Controls.Add(summaryLabel,0,0);
            var choicePanel=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true,AutoScroll=true,Visible=choices!=null};root.Controls.Add(choicePanel,0,1);
            var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
            bar.Controls.Add(new Label{Text="Tìm",AutoSize=true,Margin=new Padding(0,7,3,0)});bar.Controls.Add(query);
            bar.Controls.Add(new Label{Text="Hiện",AutoSize=true,Margin=new Padding(10,7,3,0)});bar.Controls.Add(filter);bar.Controls.Add(shown);
            filter.Items.AddRange(new object[]{FilterAll,FilterReady,FilterBlocked,FilterMissing,FilterDone});filter.SelectedIndex=0;
            new ToolTip().SetToolTip(query,"Gõ không dấu, nhiều từ; tìm trong mọi cột (mã, tên, chữ CAD, layer, lý do). -từ để loại. Bấm tiêu đề cột để sắp xếp.");
            root.Controls.Add(bar,0,2);
            UiStyle.Grid(grid);
            var columns=placement?new[]{new[]{"Handle","Vị trí (handle)","110"},new[]{"Id","ID gốc","90"},new[]{"OldName","Tên cũ Excel","140"},new[]{"NewName","Tên mới Excel","160"},
                    new[]{"Current","Chữ trong DWG","140"},new[]{"After","Sau cập nhật","140"},new[]{"Action","Hành động","130"},new[]{"Layer","Layer","110"},new[]{"Status","Kết quả / lý do","260"}}
                :new[]{new[]{"Handle","Handle","70"},new[]{"Id","ID gốc","190"},new[]{"Current","Hiện tại","220"},new[]{"After","Sau cập nhật","220"},new[]{"Status","Kết quả / lý do","260"}};
            foreach(var col in columns)grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName=col[0],HeaderText=col[1],Width=Int32.Parse(col[2]),SortMode=DataGridViewColumnSortMode.Automatic});
            grid.Columns[grid.Columns.Count-1].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns[grid.Columns.Count-1].MinimumWidth=180;
            grid.CellFormatting+=(s,e)=>{
                if(e.RowIndex<0||grid.Columns[e.ColumnIndex].DataPropertyName!="Status")return;
                var r=grid.Rows[e.RowIndex].DataBoundItem as RoomReviewRow;if(r==null)return;
                e.CellStyle.ForeColor=IsReady(r)?Color.FromArgb(16,124,16):(String.IsNullOrEmpty(r.Handle)?Color.FromArgb(110,110,110):Color.FromArgb(176,92,0));
            };
            root.Controls.Add(grid,0,3);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text="Hủy",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.Add(cancel);buttons.Controls.Add(ok);
            if(placement){
                var point=new Button{Text="Chọn điểm cho riêng phòng này…",AutoSize=true};
                Func<RoomReviewRow> selected=()=>grid.CurrentRow==null?null:grid.CurrentRow.DataBoundItem as RoomReviewRow;
                Action enable=()=>{var row=selected();point.Enabled=row!=null&&row.CanPlace;};
                point.Click+=delegate{SelectedRoom=selected();if(SelectedRoom!=null&&SelectedRoom.CanPlace)DialogResult=DialogResult.Retry;};
                grid.SelectionChanged+=delegate{enable();};Shown+=delegate{enable();};buttons.Controls.Add(point);
            }
            root.Controls.Add(buttons,0,4);AcceptButton=ok;CancelButton=cancel;
            if(choices!=null)
            {
                var repeats=new CheckBox{Text="Áp dụng mọi vị trí trùng mã (tô màu cần kiểm tra)",AutoSize=true,Checked=choices.AllowRepeats};
                var keep=new CheckBox{Text="Tạo block cả cho chữ chưa có / chưa rõ tên trong Excel (giữ tên cũ, tô màu cần kiểm tra)",AutoSize=true,Checked=choices.KeepUnmatched};
                var hide=new CheckBox{AutoSize=true,Checked=choices.HideSource};
                choicePanel.Controls.AddRange(new Control[]{repeats,keep,hide});
                Action hideText=()=>{
                    hide.Enabled=choices.CanHide;if(!choices.CanHide)hide.Checked=false;
                    hide.Text=choices.CanHide?"Tắt layer «"+choices.HideLayer+"» sau khi áp dụng (ẩn cả đối tượng khác trên layer này)":
                        "Tắt layer chữ cũ: chỉ khi layer chữ cũ là layer cụ thể (không phải 0), khác layer đích, và mọi nhãn đều cho chữ nằm trên layer đó";
                };
                hideText();
                EventHandler changed=delegate{
                    choices.AllowRepeats=repeats.Checked;choices.KeepUnmatched=keep.Checked;
                    if(rebuild!=null){var page=rebuild(choices);this.rows=page.Rows;summaryLabel.Text=page.Summary;}
                    hideText();Refill();
                };
                repeats.CheckedChanged+=changed;keep.CheckedChanged+=changed;
                hide.CheckedChanged+=delegate{choices.HideSource=hide.Checked;};
            }
            query.TextChanged+=delegate{Refill();};filter.SelectedIndexChanged+=delegate{Refill();};
            Refill();
        }
        bool Keep(RoomReviewRow r)
        {
            switch((string)filter.SelectedItem)
            {
                case FilterReady:if(!IsReady(r))return false;break;
                case FilterBlocked:if(IsReady(r)||String.IsNullOrEmpty(r.Handle)||(r.Status??"").StartsWith("Đã"))return false;break;
                case FilterMissing:if(!String.IsNullOrEmpty(r.Handle))return false;break;
                case FilterDone:if(!(r.Status??"").StartsWith("Đã"))return false;break;
            }
            return RoomSearch.Matches(query.Text,r.Handle,r.Id,r.OldName,r.NewName,r.Current,r.After,r.Action,r.Layer,r.Status);
        }
        void Refill()
        {
            var visible=rows.Where(Keep).ToList();
            GridSort.Bind(grid,visible);
            int ready=rows.Count(IsReady);
            shown.Text="Hiển thị "+visible.Count+"/"+rows.Count+" dòng · sẵn sàng "+ready;
            ok.Text=ready>0?"Áp dụng "+ready+" dòng sẵn sàng":"Áp dụng dòng hợp lệ";ok.Enabled=ready>0;
        }
    }
}
