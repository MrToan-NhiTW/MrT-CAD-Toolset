using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MrT.CAD.Toolset.Setup;
class SetupProgressTests
{
    [STAThread] static void Main(){Run(0,"Đã hoàn tất thành công");Run(1602,"Đã hủy cài đặt");Run(42,"Cài đặt chưa hoàn tất");Console.WriteLine("PASS: fake installer results remain visible with timestamps until closed");}
    static void Run(int result,string expected)
    {
        using(var form=new SetupProgress(()=>{Thread.Sleep(250);return result;}))
        using(var timer=new System.Windows.Forms.Timer())
        {
            form.StartPosition=FormStartPosition.Manual;form.Left=-2000;form.ShowInTaskbar=false;int ticks=0;
            timer.Interval=100;timer.Tick+=(s,e)=>{if(++ticks>100)throw new Exception("timeout");var close=form.Controls.OfType<Button>().Single();if(!close.Enabled)return;timer.Stop();var labels=form.Controls.OfType<Label>().Select(x=>x.Text).ToArray();if(!labels.Any(x=>x.Contains(expected))||!labels.Any(x=>x.Contains("Bắt đầu:")&&x.Contains("Kết thúc:")))throw new Exception("missing status/timestamps");close.PerformClick();};timer.Start();form.ShowDialog();
            if(form.Result!=result)throw new Exception("wrong result");
        }
    }
}
