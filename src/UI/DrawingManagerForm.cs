using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;

namespace MedGas2024.UI
{
    public sealed class DrawingManagerForm : Form
    {
        private readonly DrawingBrowser browser;
        private readonly Func<List<DrawingLayerInfo>> read;
        private readonly Action<DrawingLayerInfo,DefinitionEdit> save;
        private readonly Func<List<DrawingLayerInfo>,BatchEdit,bool> batch;private readonly Func<int,int?> palette;
        private readonly ToolTip tips=new ToolTip{InitialDelay=400,ShowAlways=true,AutoPopDelay=15000};
        public DrawingManagerForm(List<DrawingLayerInfo> rows,Func<List<DrawingLayerInfo>> reload,Action<DrawingLayerInfo,DefinitionEdit> update,Func<List<DrawingLayerInfo>,BatchEdit,bool> bulk=null,Func<int,int?> chooseColor=null)
        {UiTheme.Attach(this);
            read=reload;save=update;batch=bulk;palette=chooseColor;Text="Mr.T Quản lý đường nét | DWG hiện tại | 2.0.1.11";Font=UiStyle.Body();AutoScaleMode=AutoScaleMode.Dpi;Size=new Size(1280,790);MinimumSize=new Size(1050,680);StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(245,247,250);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),RowCount=5,ColumnCount=1};foreach(int h in new[]{44,124})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,85));root.RowStyles.Add(new RowStyle(SizeType.Absolute,24));Controls.Add(root);
            root.Controls.Add(new Label{Text="Mr.T Quản lý đường nét",Font=UiStyle.Emphasis(20),Dock=DockStyle.Fill},0,0);
            var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));foreach(float w in new[]{40F,40F,20F})actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,w));
            var current=new RibbonGroup("CurrentDrawingGroup","DWG HIỆN TẠI");var pipes=new RibbonGroup("PipeAssignmentGroup","GÁN CHO ỐNG");var library=new RibbonGroup("LibraryGroup","THƯ VIỆN");actions.Controls.Add(current,0,0);actions.Controls.Add(pipes,1,0);actions.Controls.Add(library,2,0);
            Add(current,"Đọc lại\nDWG","ReloadDrawing","open","Đọc Layer/nét thực trong DWG, không ghi thay đổi.",delegate{browser.Reload(read());});Add(current,"Sửa dòng /\nSửa nhiều","EditCurrent","edit","Xem giá trị thực và so sánh trước khi sửa định nghĩa dùng chung.",Edit);
            Add(pipes,"Gán nhanh\ncho ống","QuickAssign","assign","Gõ tìm hệ/cỡ; gán Layer và nét ByLayer, mặc định giữ hệ số từng ống.",delegate{DialogResult=DialogResult.Retry;Close();});
            Add(pipes,"Đồng bộ\nScale / Width","SyncScale","fit","Chọn đường hoặc nhiều Layer để đồng bộ Scale/Width; gợi ý A0–A4, giữ màu/nét hiện hành.",delegate{DialogResult=DialogResult.Yes;Close();});
            Add(pipes,"Sửa hướng\nchữ tuyến","FixDirection","direction","Ngang trái → phải; dọc trên → dưới. Nhãn liên kết, giữ polyline; có khôi phục nét gốc.",delegate{DialogResult=DialogResult.No;Close();});
            Add(library,"Thư viện /\nTạo mới","OpenLibrary","new","Nhập/xuất, tạo nhiều cỡ và mẫu riêng. Tên đã có trong DWG được giữ nguyên.",delegate{DialogResult=DialogResult.Abort;Close();});root.Controls.Add(actions,0,1);
            Add(current,"Xóa Layer\nđã chọn","DeleteSelected","delete","Chỉ xóa Layer trống, không xóa hình ống; có xem phạm vi trước.",delegate{DeleteRows(TargetRows());});
            browser=new DrawingBrowser(rows,true);browser.RowActivated+=delegate{EditRows(new List<DrawingLayerInfo>{browser.Selected});};browser.EditRequested+=r=>EditRows(new List<DrawingLayerInfo>{r});browser.DeleteRequested+=r=>DeleteRows(new List<DrawingLayerInfo>{r});root.Controls.Add(browser,0,2);
            root.Controls.Add(new Label{Text="Tích nhiều dòng → Sửa dòng / Sửa nhiều. Ô tích được giữ khi tìm/lọc; Sửa/Xóa từng dòng ở bên phải.\r\nĐổi màu, tên, hệ, nét, chữ/font và khoảng nét có xem trước. Xóa nhanh chỉ xóa Layer trống.\r\nCó thể Ctrl/Shift chọn dòng; nếu không tích ô, Sửa/Xóa dùng các dòng đang tô sáng. Ghi đè màu chỉ đổi khi chọn ByLayer.",Dock=DockStyle.Fill},0,3);
            root.Controls.Add(new Label{Text="workflow by Mr.Toan-NhiTW",Font=new Font("Segoe UI",8.5F),ForeColor=Color.DimGray,TextAlign=ContentAlignment.MiddleRight,Dock=DockStyle.Fill},0,4);Shown+=delegate{browser.FocusSearch();};KeyPreview=true;KeyDown+=delegate(object s,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.F){browser.FocusSearch();e.Handled=true;}};
        }
        private void Add(RibbonGroup panel,string text,string name,string icon,string hint,Action click){var b=new RibbonButton(icon){Text=text,Name=name};tips.SetToolTip(b,hint);b.Click+=delegate{try{click();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Chưa thực hiện");}};panel.AddCommand(b);}
        protected override void Dispose(bool disposing){if(disposing)tips.Dispose();base.Dispose(disposing);}
        private List<DrawingLayerInfo> TargetRows(){return browser.CheckedRows.Count>0?browser.CheckedRows:browser.SelectedRows.Count>0?browser.SelectedRows:browser.Selected==null?new List<DrawingLayerInfo>():new List<DrawingLayerInfo>{browser.Selected};}
        private void Guard(Action action){try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Chưa cập nhật DWG",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
        private void EditRows(List<DrawingLayerInfo> rows)
        {
            Guard(delegate{if(rows.Count==0)return;if(batch==null){EditLegacy(rows[0]);return;}using(var form=new BatchEditForm(rows,read(),palette))
            {
                while(form.ShowDialog(this)==DialogResult.OK)
                {
                    bool applied=false;try{applied=batch(rows,form.Value);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Chưa cập nhật DWG",MessageBoxButtons.OK,MessageBoxIcon.Information);}
                    if(applied){browser.ClearChecks();browser.ShowAllLayers();browser.Reload(read());break;}
                }
            }});
        }
        private void DeleteRows(List<DrawingLayerInfo> rows){Guard(delegate{if(rows.Count==0)return;if(batch==null){MessageBox.Show(this,"Chức năng xóa cần kết nối DWG.");return;}if(batch(rows,new BatchEdit{Delete=true})){browser.ClearChecks();browser.Reload(read());}});}
        private void Edit(){EditRows(TargetRows());}
        private void EditLegacy(DrawingLayerInfo row)
        {
            if(row==null)return;
            if(!row.Editable||!row.Writable){MessageBox.Show(this,row.State+"\r\n"+row.Font+"\r\n"+row.Details,"Chi tiết — chỉ xem",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            using(var dialog=new CurrentDefinitionForm(row))if(dialog.ShowDialog(this)==DialogResult.OK)
            {
                try{save(row,dialog.Value);browser.Reload(read());}
                catch(Exception ex){MessageBox.Show(this,ex.Message,"Chưa cập nhật DWG",MessageBoxButtons.OK,MessageBoxIcon.Information);}
            }
        }
    }
}
