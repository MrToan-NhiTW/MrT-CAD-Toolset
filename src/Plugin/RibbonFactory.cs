using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Autodesk.Windows;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;

namespace MedGas2024.Plugin
{
    internal static class RibbonFactory
    {
        public const string TabId="MRT_CAD_TOOLSET_TAB";
        public static RibbonTab Ensure(RibbonControl ribbon,RibbonTab cached,bool activate,ICommand command,string resources)
        {
            var existing=ribbon.Tabs.FirstOrDefault(t=>t.Id==TabId);
            if(existing!=null){RefreshImages(existing,resources);existing.IsVisible=true;if(activate)existing.IsActive=true;return existing;}
            var tab=cached??Create(command,resources);
            RefreshImages(tab,resources);
            tab.IsVisible=true;ribbon.Tabs.Add(tab);if(activate)tab.IsActive=true;return tab;
        }
        private static RibbonTab Create(ICommand command,string resources)
        {
            // Panels follow the work order: pipes → pick/isolate → geometry/blocks → text → tables/rooms → utilities.
            // Frequent tools are large buttons; related secondary tools are stacked small buttons.
            var tab=new RibbonTab{Id=TabId,Name=TabId,Title="Mr.T CAD tool set",IsVisible=true};
            var pipes=Panel("Đường ống");
            pipes.Items.Add(Button("Quản lý\nđường nét","MRTTOOLS","Mr.T Quản lý đường nét: gom mọi đường cùng nhãn; sửa R=0, màu và Scale cho cả cụm/toàn DWG.",command));
            pipes.Items.Add(Button("Gán nhanh\ncho ống","MRTPIPE","Gõ tìm hệ/cỡ trong DWG và gán cho các đường đã chọn hoặc chọn sau.",command));
            pipes.Items.Add(Stack(
                Small("Scale / Width","MRTSCALE","Đồng bộ hệ số nét và độ rộng cho đường/Layer được chọn; gợi ý A0–A4.",command),
                Small("R=0 hàng loạt","MRTR0","Chuyển mọi đường có nhãn sang linetype R=0 native; có thể đổi cả màu và Scale, có xem trước.",command),
                Small("Mật độ chữ ống","MRTDENSITY","Chỉnh nhịp chữ cho layer/nét Mr.T; xem phạm vi, nét ghi đè và ước tính trước khi áp dụng.",command)));
            pipes.Items.Add(Stack(
                Small("Chiều chữ R","MRTALIGNR","Kiểm tra và đảo chiều đường R để đọc trái sang phải, trên xuống dưới; tuyến mâu thuẫn được báo bỏ qua.",command)));
            var pick=Panel("Chọn & Cô lập");
            pick.Items.Add(Button("Pick /\nSimilar","MRTSEL","Chọn hỗn hợp: cùng hệ + Select Similar + toàn bộ Layer mẫu ngoại lai; nhận một/nhiều mẫu.",command));
            pick.Items.Add(Button("Isolate\nđã chọn","MRTISO","Cô lập các đối tượng đã pick; bật tạm Layer tắt/đóng băng của chúng.",command));
            pick.Items.Add(Button("Bối cảnh /\nIsolate","MRTVIEW","Chuyển nhanh giữa nhóm đang cô lập và bối cảnh trước phiên; nhóm được ghi nhớ.",command));
            pick.Items.Add(Stack(
                Small("Pick theo Layer","MRTSELLAYER","Chọn mọi đối tượng trên các Layer của mẫu đã chọn trong không gian hiện hành.",command),
                Small("Thêm vào Isolate","MRTISOADD","Thêm đối tượng hoặc Layer vào phiên MRTISO đang có.",command),
                Small("Kết thúc Isolate","MRTUNISO","Kết thúc cô lập và trả trạng thái tắt/đóng băng của Layer đã bật tạm.",command)));
            var geometry=Panel("Hình học & Block");
            geometry.Items.Add(Button("Nối nhanh\npolyline","MRTJOIN","Chọn LINE/polyline và nối ngay; khe mặc định 0.5. Đổi khe bằng MRTJOINTOL; Z0 là bước riêng.",command));
            geometry.Items.Add(Stack(
                Small("Tách tại điểm","MRTBREAK","Chọn đường rồi chỉ định một điểm tách bằng BREAKATPOINT của AutoCAD.",command),
                Small("Làm sạch Z = 0","MRTZ0","Quét và xác nhận trước khi đưa về Z=0; theo Z0Clean của Mr.Toan-NhiTW.",command),
                Small("Z=0 + Polyline","MRTZ0PL","Đưa LINE/polyline (cả 3D, lật pháp tuyến) về Z=0 và tùy chọn đổi LINE → polyline giữ handle; có xem trước.",command)));
            geometry.Items.Add(Stack(
                Small("Kiểm tra Layer block","MRTBLOCKLAY","Quét Layer block mẫu và pick block mẹ cùng đối tượng ngoài.",command),
                Small("Dồn Layer block","MRTBLOCKMERGE","Dồn Layer thành phần block thường với xem trước phạm vi định nghĩa dùng chung.",command)));
            var text=Panel("Chữ & Font");
            text.Items.Add(Button("Đổi font\nđã chọn","MRTFONT","Đổi font, chuyển TCVN3/ABC sang Unicode khi nhận diện; chọn font và đối tượng.",command));
            text.Items.Add(Stack(
                Small("Font toàn bản vẽ","MRTFONTALL","CẢNH BÁO: có thể ảnh hưởng chữ Unicode/block động; đang chờ mẫu kiểm tra, chỉ thử trên bản sao DWG.",command),
                Small("Kiểm tra font","MRTFONTCHECK","Kiểm tra font và bảng mã của chữ được chọn.",command)));
            var legend=Panel("Bảng & Phòng");
            legend.Items.Add(Button("Bảng\nký hiệu","MRTLEGEND","Tạo bảng ký hiệu: mẫu nét ống, block/hình 2D, diễn giải và bố cục 2/4 cột.",command));
            legend.Items.Add(Stack(
                Small("Sửa bảng","MRTLEGEDIT","Chọn bảng Mr.T đã chèn để đổi font, thêm hàng và sắp lại bố cục.",command),
                Small("Đồng bộ mẫu ống","MRTLEGSYNC","Cập nhật nét ống trong các bảng Mr.T theo ống nguồn, giữ tỷ lệ và độ rộng mẫu; có xem trước.",command)));
            legend.Items.Add(Button("Tên phòng\ncũ / mới","MRTROOMS","Chọn danh sách Excel, gắn tên phòng và chuyển hàng loạt theo ID gốc; có xem trước.",command));
            var utility=Panel("Tiện ích");
            utility.Items.Add(Stack(
                Small("Phím tắt (alias)","MRTALIASES","Quản lý alias, ví dụ 6 + Enter gọi MRTJOIN; hỏi trước khi thay alias cũ.",command),
                Small("Hướng dẫn","MRTHELP","Mở hướng dẫn Mr.T CAD tool set lưu trên máy.",command)));
            foreach(var source in new[]{pipes,pick,geometry,text,legend,utility})tab.Panels.Add(new RibbonPanel{Source=source});
            return tab;
        }
        private static RibbonPanelSource Panel(string title){return new RibbonPanelSource{Title=title};}
        private static RibbonButton Button(string title,string name,string hint,ICommand command)
        {
            return new RibbonButton{Id="MRT_"+name,Text=title,ShowText=true,ShowImage=false,Size=RibbonItemSize.Large,Orientation=System.Windows.Controls.Orientation.Vertical,ToolTip=hint,CommandParameter=name,CommandHandler=command};
        }
        private static RibbonButton Small(string title,string name,string hint,ICommand command)
        {
            return new RibbonButton{Id="MRT_"+name,Text=title,ShowText=true,ShowImage=false,Size=RibbonItemSize.Standard,Orientation=System.Windows.Controls.Orientation.Horizontal,ToolTip=hint,CommandParameter=name,CommandHandler=command};
        }
        // One column of up to three small buttons.
        private static RibbonRowPanel Stack(params RibbonButton[] buttons)
        {
            var column=new RibbonRowPanel();
            for(int i=0;i<buttons.Length;i++){if(i>0)column.Items.Add(new RibbonRowBreak());column.Items.Add(buttons[i]);}
            return column;
        }
        // Buttons may sit directly in a panel or inside a stacked row panel.
        internal static System.Collections.Generic.IEnumerable<RibbonButton> AllButtons(RibbonTab tab)
        {
            foreach(var panel in tab.Panels)foreach(var item in panel.Source.Items)
            {
                var button=item as RibbonButton;if(button!=null){yield return button;continue;}
                var row=item as RibbonRowPanel;if(row!=null)foreach(var inner in row.Items.OfType<RibbonButton>())yield return inner;
            }
        }
        internal static bool LightTheme(){try{return Convert.ToInt32(AcApp.GetSystemVariable("COLORTHEME"))==1;}catch{return false;}}
        internal static void RefreshImages(RibbonTab tab,string resources)
        {
            string suffix=LightTheme()?"-light":"-dark";
            foreach(var b in AllButtons(tab))
            {
                string key=Convert.ToString(b.CommandParameter).ToLowerInvariant();
                try{b.LargeImage=ReadImage(Path.Combine(resources,key+suffix+"32.png"));b.Image=ReadImage(Path.Combine(resources,key+suffix+"16.png"));b.ShowImage=b.Size==RibbonItemSize.Large?b.LargeImage!=null:b.Image!=null;}
                catch{b.LargeImage=null;b.Image=null;b.ShowImage=false;}
            }
        }
        private static BitmapImage ReadImage(string path)
        {
            if(!File.Exists(path))return null;var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(path,UriKind.Absolute);bitmap.EndInit();bitmap.Freeze();return bitmap;
        }
    }
}
