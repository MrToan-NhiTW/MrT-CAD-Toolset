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
            var tab=new RibbonTab{Id=TabId,Name=TabId,Title="Mr.T CAD tool set",IsVisible=true};
            var panel=new RibbonPanelSource{Title="Công cụ bản vẽ"};
            panel.Items.Add(Button("Mr.T Quản lý\nđường nét","MRTTOOLS","Mr.T Quản lý đường nét: gom mọi đường cùng nhãn; sửa R=0, màu và Scale cho cả cụm/toàn DWG.","manage32.png",command,resources));
            panel.Items.Add(Button("Gán nhanh\ncho ống","MRTPIPE","Gõ tìm hệ/cỡ trong DWG và gán cho các đường đã chọn hoặc chọn sau.","assign32.png",command,resources));
            panel.Items.Add(Button("Đồng bộ\nScale / Width","MRTSCALE","Đồng bộ hệ số nét và độ rộng cho đường/Layer được chọn; gợi ý A0–A4.","assign32.png",command,resources));
            panel.Items.Add(Button("Mật độ\nchữ ống","MRTDENSITY","Chỉnh nhịp chữ cho layer/nét Mr.T; xem phạm vi, nét ghi đè và ước tính trước khi áp dụng.","direction32.png",command,resources));
            panel.Items.Add(Button("Hướng dẫn","MRTHELP","Mở hướng dẫn Mr.T CAD tool set lưu trên máy.","help32.png",command,resources));
            panel.Items.Insert(3,Button("Chuyển R=0\nhàng loạt","MRTR0","Chuyển mọi đường có nhãn sang linetype R=0 native; có thể đổi cả màu và Scale, có xem trước.","direction32.png",command,resources));
            panel.Items.Insert(4,Button("Pick /\nSelect Similar","MRTSEL","Chọn hỗn hợp: cùng hệ + Select Similar + toàn bộ Layer mẫu ngoại lai; nhận một/nhiều mẫu.","pick32.png",command,resources));
            panel.Items.Insert(5,Button("Isolate\nđã chọn","MRTISO","Cô lập các đối tượng đã pick; bật tạm Layer tắt/đóng băng của chúng.","pick32.png",command,resources));panel.Items.Insert(6,Button("Kết thúc\nIsolate","MRTUNISO","Kết thúc cô lập và trả trạng thái tắt/đóng băng của Layer đã bật tạm.","manage32.png",command,resources));
            panel.Items.Add(Button("Pick tất cả\ntheo Layer","MRTSELLAYER","Chọn mọi đối tượng trên các Layer của mẫu đã chọn trong không gian hiện hành.","pick32.png",command,resources));
            panel.Items.Add(Button("Kiểm tra\nchiều chữ R","MRTALIGNR","Kiểm tra và đảo chiều đường R để đọc trái sang phải, trên xuống dưới; tuyến mâu thuẫn được báo bỏ qua.","direction32.png",command,resources));
            panel.Items.Add(Button("Bối cảnh /\nIsolate","MRTVIEW","Chuyển nhanh giữa nhóm đang cô lập và bối cảnh trước phiên; nhóm được ghi nhớ.","pick32.png",command,resources));
            panel.Items.Add(Button("Thêm vào\nIsolate","MRTISOADD","Thêm đối tượng hoặc Layer vào phiên MRTISO đang có.","pick32.png",command,resources));
            panel.Items.Add(Button("Kiểm tra\nLayer block","MRTBLOCKLAY","Quét Layer block mẫu và pick block mẹ cùng đối tượng ngoài.","manage32.png",command,resources));
            panel.Items.Add(Button("Dồn Layer\nblock","MRTBLOCKMERGE","Dồn Layer thành phần block thường với xem trước phạm vi định nghĩa dùng chung.","manage32.png",command,resources));
            var input=new RibbonPanelSource{Title="Xử lý đầu vào"};
            panel.Items.Add(Button("Nối nhanh\npolyline","MRTJOIN","Chọn LINE/polyline và nối ngay; khe mặc định 0.5. Đổi khe bằng MRTJOINTOL; Z0 là bước riêng.","join32.png",command,resources));
            input.Items.Add(Button("Làm sạch\nZ = 0","MRTZ0","Quét và xác nhận trước khi đưa về Z=0; theo Z0Clean của Mr.Toan-NhiTW.","z032.png",command,resources));
            panel.Items.Add(Button("Tách tại\nmột điểm","MRTBREAK","Chọn đường rồi chỉ định một điểm tách bằng BREAKATPOINT của AutoCAD.","break32.png",command,resources));
            panel.Items.Add(Button("Gán nhanh\nphím lệnh","MRTALIASES","Quản lý alias, ví dụ 6 + Enter gọi MRTJOIN; hỏi trước khi thay alias cũ.","alias32.png",command,resources));
            tab.Panels.Add(new RibbonPanel{Source=panel});
            input.Items.Add(Button("Đổi font\nđã chọn","MRTFONT","Đổi font, chuyển TCVN3/ABC sang Unicode khi nhận diện; chọn font và đối tượng.","font32.png",command,resources));
            input.Items.Add(Button("Đổi font\ntoàn bản vẽ","MRTFONTALL","CẢNH BÁO: có thể ảnh hưởng chữ Unicode/block động; đang chờ mẫu kiểm tra, chỉ thử trên bản sao DWG.","fontall32.png",command,resources));
            input.Items.Add(Button("Kiểm tra\nfont chữ","MRTFONTCHECK","Kiểm tra font và bảng mã của chữ được chọn.","fontcheck32.png",command,resources));
            tab.Panels.Add(new RibbonPanel{Source=input});
            var legend=new RibbonPanelSource{Title="Ký hiệu / Ghi chú"};
            legend.Items.Add(Button("Bảng\nký hiệu","MRTLEGEND","Tạo bảng ký hiệu: mẫu nét ống, block/hình 2D, diễn giải và bố cục 2/4 cột.","legend32.png",command,resources));
            legend.Items.Add(Button("Sửa bảng\nký hiệu","MRTLEGEDIT","Chọn bảng Mr.T đã chèn để đổi font, thêm hàng và sắp lại bố cục.","legend32.png",command,resources));
            legend.Items.Add(Button("Đồng bộ\nmẫu ống","MRTLEGSYNC","Cập nhật nét ống trong các bảng Mr.T theo ống nguồn, giữ tỷ lệ và độ rộng mẫu; có xem trước.","legend32.png",command,resources));
            legend.Items.Add(Button("Tên phòng\ncũ / mới","MRTROOMS","Chọn danh sách Excel, gắn tên phòng và chuyển hàng loạt theo ID gốc; có xem trước.","legend32.png",command,resources));
            tab.Panels.Add(new RibbonPanel{Source=legend});return tab;
        }
        private static RibbonButton Button(string title,string name,string hint,string image,ICommand command,string resources)
        {
            var b=new RibbonButton{Id="MRT_"+name,Text=title,ShowText=true,ShowImage=false,Size=RibbonItemSize.Large,Orientation=System.Windows.Controls.Orientation.Vertical,ToolTip=hint,CommandParameter=name,CommandHandler=command};
            return b;
        }
        internal static bool LightTheme(){try{return Convert.ToInt32(AcApp.GetSystemVariable("COLORTHEME"))==1;}catch{return false;}}
        internal static void RefreshImages(RibbonTab tab,string resources)
        {
            string suffix=LightTheme()?"-light":"-dark";
            foreach(var b in tab.Panels.SelectMany(p=>p.Source.Items).OfType<RibbonButton>())
            {
                string key=Convert.ToString(b.CommandParameter).ToLowerInvariant();
                try{b.LargeImage=ReadImage(Path.Combine(resources,key+suffix+"32.png"));b.Image=ReadImage(Path.Combine(resources,key+suffix+"16.png"));b.ShowImage=b.LargeImage!=null;}
                catch{b.LargeImage=null;b.Image=null;b.ShowImage=false;}
            }
        }
        private static BitmapImage ReadImage(string path)
        {
            if(!File.Exists(path))return null;var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(path,UriKind.Absolute);bitmap.EndInit();bitmap.Freeze();return bitmap;
        }
    }
}
