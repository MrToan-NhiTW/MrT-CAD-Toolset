using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MedGas2024.UI
{
    // Autodesk-style dialog theme (neutral greys, Autodesk blue accent, flat controls,
    // horizontal-rule grids, 11 pt Segoe UI). Applied once per form at Load and to any
    // control added later, so each form keeps its own layout code.
    public static class UiTheme
    {
        public static readonly Color Canvas=Color.FromArgb(245,245,245);
        public static readonly Color Surface=Color.White;
        public static readonly Color Ink=Color.FromArgb(60,60,60);
        public static readonly Color Muted=Color.FromArgb(128,128,128);
        public static readonly Color Line=Color.FromArgb(217,217,217);
        public static readonly Color Accent=Color.FromArgb(6,150,215);       // Autodesk blue
        public static readonly Color AccentDark=Color.FromArgb(0,119,179);
        public static readonly Color AccentSoft=Color.FromArgb(229,243,251);
        public static readonly Color Selection=Color.FromArgb(205,232,246);
        public static readonly Color HeaderBack=Color.FromArgb(238,238,238);
        public const float BodySize=11F;
        public const string Family="Segoe UI";

        static readonly string[] PrimaryWords={"áp dụng","chèn","tạo vào","tạo hệ","lưu","đồng ý","ok","gán","xác nhận","thực hiện","chuyển"};

        public static readonly Color Banner=Color.FromArgb(32,39,49);         // dark header / Win11 caption
        public static readonly Color BannerSub=Color.FromArgb(163,178,194);
        public static readonly Color BannerTag=Color.FromArgb(96,186,236);

        public static void Attach(Form form)
        {
            if(form==null)return;
            form.HandleCreated+=delegate{Chrome(form);};
            if(form.IsHandleCreated)Chrome(form);
            form.Load+=delegate{AddHeader(form);Apply(form);};
            form.ControlAdded+=delegate(object s,ControlEventArgs e){if(form.IsHandleCreated)Apply(e.Control);};
        }

        // ---------- Windows 11 frame: Autodesk-blue border, caption matching the header ----------
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        static int ColorRef(Color c){return c.R|(c.G<<8)|(c.B<<16);}
        static void Chrome(Form form)
        {
            try
            {
                int border=ColorRef(Accent),caption=ColorRef(Banner),text=ColorRef(Color.White);
                DwmSetWindowAttribute(form.Handle,34,ref border,4);   // DWMWA_BORDER_COLOR (Win11)
                DwmSetWindowAttribute(form.Handle,35,ref caption,4);  // DWMWA_CAPTION_COLOR
                DwmSetWindowAttribute(form.Handle,36,ref text,4);     // DWMWA_TEXT_COLOR
            }
            catch(Exception){}                                         // Windows 10: keep system frame
            try{if(form.ShowIcon)form.Icon=UiHeader.WindowIcon(IconFor(form));}catch(Exception){}
        }

        public static string IconFor(Form form)
        {
            switch(form.GetType().Name)
            {
                case "DrawingManagerForm":case "LabelManagerForm":case "LegacyMapForm":case "LayerMultiDialog":return "layers";
                case "ManagerForm":case "NewSystemForm":case "RowEditorForm":case "FamilySizesForm":return "new";
                case "LegendForm":return "legend";
                case "DensityForm":return "density";
                case "DirectionForm":case "RNameReviewForm":return "direction";
                case "PipePickerForm":case "PickModeForm":return "pick";
                case "ScaleForm":return "fit";
                case "ColorGroupForm":return "color";
                case "RoomForm":case "RoomReviewForm":return "room";
                case "AliasForm":return "keyboard";
                case "BlockTargetDialog":return "block";
                case "BatchReviewForm":case "LabelReviewForm":return "inspect";
                default:return "edit";
            }
        }

        // ---------- Header band ----------
        // Added once per form at Load. In-layout big titles move into the band (their row is
        // collapsed), so the form grows only by what the band adds. Loose (absolutely
        // positioned) controls are shifted below the band.
        static void AddHeader(Form form)
        {
            if(form.Controls.ContainsKey("MrTHeader"))return;
            var header=new UiHeader(form){Name="MrTHeader",Dock=DockStyle.Top};
            int h=header.Height,reclaimed=0;
            var title=FindTitle(form);
            if(title!=null){reclaimed=Collapse(title);header.TitleSource=title;}
            int grow=Math.Max(0,h-reclaimed);
            form.SuspendLayout();
            try
            {
                foreach(Control c in form.Controls)
                {
                    if(c.Dock!=DockStyle.None)continue;
                    bool top=(c.Anchor&AnchorStyles.Top)!=0,bottom=(c.Anchor&AnchorStyles.Bottom)!=0;
                    if(top&&bottom)c.SetBounds(c.Left,c.Top+grow,c.Width,Math.Max(10,c.Height-grow));
                    else if(top||!bottom)c.Top+=grow;
                }
                if(grow>0)
                {
                    if(form.MinimumSize.Height>0)form.MinimumSize=new Size(form.MinimumSize.Width,form.MinimumSize.Height+grow);
                    if(form.MaximumSize.Height>0)form.MaximumSize=new Size(form.MaximumSize.Width,form.MaximumSize.Height+grow);
                    form.ClientSize=new Size(form.ClientSize.Width,form.ClientSize.Height+grow);
                    if(form.StartPosition==FormStartPosition.CenterScreen||form.StartPosition==FormStartPosition.CenterParent)form.Top=Math.Max(0,form.Top-grow/2);
                }
                form.Controls.Add(header);header.SendToBack();      // docks first: always the top band
            }
            finally{form.ResumeLayout(true);}
        }
        static Label FindTitle(Control root)
        {
            foreach(Control c in root.Controls)
            {
                var l=c as Label;
                if(l!=null&&l.Font!=null&&l.Font.SizeInPoints>=17F&&l.Parent is TableLayoutPanel)return l;
                if(c is DataGridView||c is UiHeader)continue;
                var inner=FindTitle(c);if(inner!=null)return inner;
            }
            return null;
        }
        static int Collapse(Label title)
        {
            var table=(TableLayoutPanel)title.Parent;int row=table.GetRow(title);
            foreach(Control c in table.Controls)if(c!=title&&table.GetRow(c)==row)return 0;   // shared row: keep
            if(row<0||row>=table.RowStyles.Count)return 0;
            var style=table.RowStyles[row];int reclaimed;
            if(style.SizeType==SizeType.Absolute){reclaimed=(int)style.Height;style.Height=0;}
            else if(style.SizeType==SizeType.AutoSize)reclaimed=title.Height+title.Margin.Vertical;
            else return 0;
            title.Visible=false;return reclaimed;
        }
        public static void Apply(Control root)
        {
            if(root==null)return;
            Style(root);
            foreach(Control child in root.Controls)Apply(child);
            if(!(root is DataGridView)&&!(root is NumericUpDown)&&!(root is UpDownBase))
            {
                root.ControlAdded-=OnAdded;root.ControlAdded+=OnAdded;
            }
        }
        static void OnAdded(object sender,ControlEventArgs e){Apply(e.Control);}
        static Font Resize(Font f,float minimum)
        {
            if(f==null)return new Font(Family,minimum);
            float size=f.SizeInPoints<minimum&&f.SizeInPoints>=8F?minimum:f.SizeInPoints;
            string family=f.FontFamily.Name.StartsWith("Segoe",StringComparison.OrdinalIgnoreCase)||f.FontFamily.Name=="Microsoft Sans Serif"||f.FontFamily.Name=="Tahoma"?f.FontFamily.Name.Replace("Microsoft Sans Serif",Family).Replace("Tahoma",Family):f.FontFamily.Name;
            if(Math.Abs(size-f.SizeInPoints)<.01F&&family==f.FontFamily.Name)return f;
            return new Font(family,size,f.Style,GraphicsUnit.Point);
        }
        public static bool IsPrimary(Button b)
        {
            var form=b.FindForm();if(form!=null&&form.AcceptButton==b)return true;
            if(b.DialogResult==DialogResult.OK)return true;string text=(b.Text??"").Trim().ToLowerInvariant();
            if(text.StartsWith("đóng")||text.StartsWith("hủy")||text.StartsWith("xem")||text.StartsWith("chọn")||text.StartsWith("bỏ"))return false;
            return PrimaryWords.Any(w=>text.StartsWith(w));
        }
        static void Style(Control c)
        {
            if(c is RibbonButton||c is RibbonGroup||c is UiHeader)return;   // own paint, already themed
            var form=c as Form;
            if(form!=null){form.BackColor=Canvas;form.ForeColor=Ink;form.Font=Resize(form.Font,BodySize);return;}
            if(c.Font!=null&&c.Parent!=null&&!ReferenceEquals(c.Font,c.Parent.Font))c.Font=Resize(c.Font,BodySize-.5F);
            var grid=c as DataGridView;if(grid!=null){Grid(grid);return;}
            var button=c as Button;if(button!=null){Button(button);return;}
            var label=c as Label;
            if(label!=null)
            {
                if(label.Font.SizeInPoints>=14F){label.ForeColor=Color.FromArgb(38,38,38);}
                else if((label.Text??"").StartsWith("workflow by",StringComparison.OrdinalIgnoreCase)){label.ForeColor=Muted;label.Font=new Font(Family,9F);}
                else if(label.BackColor==Color.Transparent||label.BackColor==SystemColors.Control)label.ForeColor=Ink;
                if(label.BackColor==SystemColors.Control)label.BackColor=Color.Transparent;
                return;
            }
            var text=c as TextBoxBase;
            if(text!=null){if(text.BorderStyle==BorderStyle.Fixed3D)text.BorderStyle=BorderStyle.FixedSingle;if(text.ReadOnly&&text is TextBox&&((TextBox)text).Multiline){text.BackColor=Color.FromArgb(250,250,250);}else if(!text.ReadOnly)text.BackColor=Surface;text.ForeColor=Ink;return;}
            var updown=c as UpDownBase;if(updown!=null){updown.BorderStyle=BorderStyle.FixedSingle;updown.BackColor=Surface;updown.ForeColor=Ink;return;}
            var combo=c as ComboBox;if(combo!=null){combo.FlatStyle=FlatStyle.Flat;combo.BackColor=Surface;combo.ForeColor=Ink;return;}
            var check=c as CheckBox;if(check!=null){check.ForeColor=Ink;if(check.BackColor==SystemColors.Control)check.BackColor=Color.Transparent;return;}
            var radio=c as RadioButton;if(radio!=null){radio.ForeColor=Ink;if(radio.BackColor==SystemColors.Control)radio.BackColor=Color.Transparent;return;}
            var group=c as GroupBox;if(group!=null){group.ForeColor=AccentDark;group.BackColor=Color.Transparent;return;}
            var list=c as ListBox;if(list!=null){list.BorderStyle=BorderStyle.FixedSingle;list.BackColor=Surface;list.ForeColor=Ink;return;}
            var tabs=c as TabPage;if(tabs!=null){tabs.BackColor=Canvas;tabs.ForeColor=Ink;return;}
            if((c is Panel||c is TableLayoutPanel||c is FlowLayoutPanel||c is UserControl||c is SplitContainer)&&c.BackColor==SystemColors.Control)c.BackColor=Color.Transparent;
        }
        static void Button(Button b)
        {
            if(b.FlatStyle==FlatStyle.System)return;
            bool primary=IsPrimary(b);b.FlatStyle=FlatStyle.Flat;b.UseVisualStyleBackColor=false;b.Cursor=Cursors.Hand;
            // Rows in existing layouts are sized for ~34 px buttons; keep vertical padding at 0
            // so the larger font never pushes a button outside its row.
            // Sizes stay with each form's layout (tested at minimum window size); the theme
            // changes colour and border only.
            b.FlatAppearance.BorderSize=1;
            if(primary){b.BackColor=Accent;b.ForeColor=Color.White;b.FlatAppearance.BorderColor=Accent;b.FlatAppearance.MouseOverBackColor=AccentDark;b.FlatAppearance.MouseDownBackColor=AccentDark;}
            else{b.BackColor=Surface;b.ForeColor=Ink;b.FlatAppearance.BorderColor=Color.FromArgb(160,160,160);b.FlatAppearance.MouseOverBackColor=AccentSoft;b.FlatAppearance.MouseDownBackColor=Selection;}
            b.EnabledChanged-=Recolor;b.EnabledChanged+=Recolor;Recolor(b,EventArgs.Empty);
        }
        static void Recolor(object sender,EventArgs e)
        {
            var b=(Button)sender;bool primary=IsPrimary(b);
            if(!b.Enabled){b.BackColor=Color.FromArgb(235,235,235);b.FlatAppearance.BorderColor=Color.FromArgb(210,210,210);}
            else if(primary){b.BackColor=Accent;b.FlatAppearance.BorderColor=Accent;}
            else{b.BackColor=Surface;b.FlatAppearance.BorderColor=Color.FromArgb(160,160,160);}
        }
        // TabControl stays system-drawn: owner-drawn tabs change its preferred width and
        // break the minimum-size layout tests.
        public static void Grid(DataGridView grid)
        {
            grid.BackgroundColor=Surface;grid.BorderStyle=BorderStyle.FixedSingle;grid.GridColor=Line;
            grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.EnableHeadersVisualStyles=false;
            grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.Single;
            var header=grid.ColumnHeadersDefaultCellStyle;header.BackColor=HeaderBack;header.ForeColor=Ink;header.SelectionBackColor=HeaderBack;header.SelectionForeColor=Ink;header.Font=new Font("Segoe UI Semibold",10.5F);header.Padding=new Padding(6,0,4,0);
            var cell=grid.DefaultCellStyle;cell.BackColor=Surface;cell.ForeColor=Ink;cell.SelectionBackColor=Selection;cell.SelectionForeColor=Color.FromArgb(20,20,20);cell.Font=new Font(Family,10.5F);if(cell.Padding.Left<6)cell.Padding=new Padding(6,1,4,1);
            grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(250,250,250);
            grid.RowHeadersDefaultCellStyle.BackColor=HeaderBack;grid.RowHeadersDefaultCellStyle.SelectionBackColor=Selection;
            if(grid.RowTemplate.Height<36)grid.RowTemplate.Height=36;
            if(grid.ColumnHeadersHeightSizeMode!=DataGridViewColumnHeadersHeightSizeMode.AutoSize&&grid.ColumnHeadersHeight<40)grid.ColumnHeadersHeight=40;
        }
    }
}
