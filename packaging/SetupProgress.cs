using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
namespace MrT.CAD.Toolset.Setup
{
    public sealed class SetupProgress : Form
    {
        readonly Func<int> action; readonly DateTime started=DateTime.Now; readonly Stopwatch elapsed=Stopwatch.StartNew();
        readonly Label status=new Label(), times=new Label(), details=new Label(); readonly Button close=new Button(); readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer(); bool finished;
        public int Result=1;
        public SetupProgress(Func<int> operation)
        {
            action=operation;Text="Mr.T CAD tool set 2.0.2.1 — Cài đặt";ClientSize=new Size(610,240);StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
            status.SetBounds(22,18,565,40);status.Font=new Font(Font.FontFamily,12,FontStyle.Bold);status.Text="Đang chuẩn bị và cài đặt…";
            times.SetBounds(22,68,565,46);details.SetBounds(22,120,565,60);details.Text="Làm theo cửa sổ Windows Installer nếu được yêu cầu. Không đóng cửa sổ này khi đang cài.";
            close.SetBounds(470,190,115,30);close.Text="Đóng";close.Enabled=false;close.Click+=(s,e)=>Close();Controls.AddRange(new Control[]{status,times,details,close});
            timer.Interval=1000;timer.Tick+=(s,e)=>UpdateTime();UpdateTime();timer.Start();
            Shown+=(s,e)=>ThreadPool.QueueUserWorkItem(x=>Run());
            FormClosing+=(s,e)=>{if(!finished)e.Cancel=true;};
        }
        void UpdateTime(){times.Text="Bắt đầu: "+started.ToString("HH:mm:ss dd/MM/yyyy")+"    |    Đã chạy: "+elapsed.Elapsed.ToString(@"hh\:mm\:ss")+(finished?"\r\nKết thúc: "+DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy"):"");}
        void Run()
        {
            int code=1;string error=null;try{code=action();}catch(Exception ex){error=ex.Message;}
            BeginInvoke((MethodInvoker)delegate{Result=code;elapsed.Stop();finished=true;timer.Stop();UpdateTime();
                if(error!=null){status.Text="Chưa hoàn tất";details.Text=error;}
                else if(code==0){status.Text="Đã hoàn tất thành công";details.Text="Bộ cài đã hoàn tất. Có thể đóng cửa sổ này.";}
                else if(code==3010){Result=0;status.Text="Đã hoàn tất — cần khởi động lại";details.Text="Windows yêu cầu khởi động lại để áp dụng hết thay đổi.";}
                else if(code==1602){status.Text="Đã hủy cài đặt";details.Text="Quá trình đã bị hủy; chưa thể xác nhận cập nhật thành công.";}
                else {status.Text="Cài đặt chưa hoàn tất";details.Text="Windows Installer trả mã "+code+". Xem nhật ký cài đặt trong thư mục MrT\\CADToolset\\Logs.";}
                close.Enabled=true;close.Focus();});
        }
    }
}
