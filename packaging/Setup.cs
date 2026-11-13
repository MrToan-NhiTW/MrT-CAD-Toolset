using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

namespace MrT.CAD.Toolset.Setup
{
    public static class Program
    {
        private static string Digest(string path)
        {
            using(var input=File.OpenRead(path))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","");
        }
        private static void Extract(string path)
        {
            using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("MrT.Toolset.Payload.msi"))
            {
                if(resource==null)throw new InvalidDataException("Không tìm thấy gói cài bên trong Setup.exe.");
                using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))resource.CopyTo(output);
            }
            if(Digest(path)!=Payload.Sha256)throw new InvalidDataException("Gói cài không khớp SHA256. Hãy tải lại bộ cài.");
        }
        [STAThread]
        public static int Main(string[] args)
        {
            bool extractOnly=args.Length==2&&args[0]=="--extract";
            try
            {
                // This diagnostic mode only writes the embedded MSI to the exact new
                // file supplied by the caller. It never invokes Windows Installer.
                if(extractOnly){Extract(Path.GetFullPath(args[1]));return 0;}
                if(args.Length!=0&&!(args.Length==1&&args[0]=="--uninstall"))throw new ArgumentException("Chạy Setup.exe để cài / sửa chữa; dùng --uninstall để gỡ.");
                bool created;
                using(var mutex=new Mutex(true,"Local\\MrT.CAD.Toolset.Setup",out created))
                {
                    if(!created)throw new InvalidOperationException("Bộ cài Mr.T CAD tool set đang mở. Hoàn tất cửa sổ đó trước.");
                    try
                    {
                        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                        using(var window=new SetupProgress(delegate {
                        if(Process.GetProcessesByName("acad").Length>0||Process.GetProcessesByName("accoreconsole").Length>0)throw new InvalidOperationException("Hãy lưu bản vẽ và đóng tất cả phiên AutoCAD trước khi cài / cập nhật / gỡ. Sau đó chạy lại Setup.exe.");
                        string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MrT","CADToolset");
                        string cache=Path.Combine(data,"SetupCache","2.0.1.6",Payload.Sha256.Substring(0,16));Directory.CreateDirectory(cache);
                        string msi=Path.Combine(cache,"MrT_CAD_Toolset_2.0.1.6_AutoCAD2024_x64.msi");
                        if(File.Exists(msi)){if(Digest(msi)!=Payload.Sha256)throw new InvalidDataException("Cache bộ cài đã bị thay đổi. Xóa đúng file cache rồi chạy lại: "+msi);}
                        else{string temporary=Path.Combine(cache,Guid.NewGuid().ToString("N")+".msi");Extract(temporary);File.Move(temporary,msi);}
                        string logs=Path.Combine(data,"Logs");Directory.CreateDirectory(logs);
                        string log=Path.Combine(logs,"setup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".log");
                        string executable=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe");
                        var info=new ProcessStartInfo(executable,(args.Length==1?"/x":"/i")+" \""+msi+"\" /L*v \""+log+"\" /norestart"){UseShellExecute=true,Verb="runas"};
                        using(var process=Process.Start(info))
                        {
                            if(process==null)throw new InvalidOperationException("Không mở được Windows Installer.");
                            process.WaitForExit();return process.ExitCode;
                        }
                        })) {window.ShowDialog();return window.Result;}
                    }
                    finally{mutex.ReleaseMutex();}
                }
            }
            catch(Exception ex)
            {
                if(!extractOnly)MessageBox.Show(ex.Message,"Mr.T CAD tool set — Setup",MessageBoxButtons.OK,MessageBoxIcon.Information);
                return 1;
            }
        }
    }
}
