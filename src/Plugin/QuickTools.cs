using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Engine;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;

namespace MedGas2024.Plugin
{
    public sealed partial class Commands
    {
        static double joinTolerance=0.5;
        [CommandMethod("MRTJOIN",CommandFlags.Modal|CommandFlags.UsePickSet)]
        public void QuickJoin()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;try{
                var ids=Preselected(doc);if(ids.Length==0){var p=doc.Editor.GetSelection(new PromptSelectionOptions{MessageForAdding="\nChọn LINE / polyline cần chuẩn hóa và nối: "});if(p.Status!=PromptStatus.OK)return;using(p.Value)ids=p.Value.GetObjectIds();}
                using(var locked=doc.LockDocument()){
                    var plan=QuickJoinService.Inspect(doc.Database,ids,joinTolerance,false);
                    if(plan.Routes.Count==0){doc.Editor.WriteMessage("\nMr.T JOIN: không có đường phù hợp. "+plan.Summary()+"\n");return;}
                    var replacements=QuickJoinService.Apply(doc.Database,plan,false);IsolationHost.RecordSplit(doc,replacements);doc.Editor.Regen();
                    if(replacements.Count>0)try{doc.Editor.SetImpliedSelection(replacements.Values.SelectMany(x=>x).Distinct().ToArray());}
                    catch(System.Exception){doc.Editor.WriteMessage("\nĐã nối nhưng không thể chọn lại kết quả tự động; hãy chọn đường kết quả để kiểm tra chiều R.\n");}
                    doc.Editor.WriteMessage("\nMr.T JOIN: khe tối đa "+joinTolerance.ToString("0.#####")+"; "+plan.Links+" mối nối; "+plan.Chains.Count+" tuyến; thay "+replacements.Count+" đối tượng. Dùng UNDO để hoàn tác.\n"+plan.Summary()+"\n");
                }
            }catch(System.Exception ex){Report(doc,ex);}
        }
        [CommandMethod("MRTJOINTOL",CommandFlags.Modal)]
        public void QuickJoinTolerance(){var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;var p=new PromptDoubleOptions("\nKhe nối XY cho MRTJOIN <"+joinTolerance.ToString("0.#####")+">: "){AllowNegative=false,AllowZero=true,AllowNone=true};var result=doc.Editor.GetDouble(p);if(result.Status==PromptStatus.OK){if(result.Value>100){doc.Editor.WriteMessage("\nKhe nối tối đa là 100 đơn vị bản vẽ.\n");return;}joinTolerance=result.Value;}doc.Editor.WriteMessage("\nMr.T JOIN: khe "+joinTolerance.ToString("0.#####")+"; áp dụng đến khi đóng AutoCAD.\n");}
        [CommandMethod("MRTBREAK",CommandFlags.Modal)]
        public void QuickBreak(){var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc!=null)doc.SendStringToExecute("_.BREAKATPOINT ",true,false,false);}
        [CommandMethod("MRTALIASES",CommandFlags.Modal)]
        public void AliasManager(){var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;try{string path=HostApplicationServices.Current.FindFile("acad.pgp",doc.Database,FindFileHint.Default);using(var f=new AliasForm(path))AcApp.ShowModalDialog(f);}catch(System.Exception ex){Report(doc,ex);}}
    }
    internal sealed class AliasForm:Form
    {
        readonly string path;readonly TextBox alias=new TextBox();readonly ComboBox command=new ComboBox();readonly Label state=new Label();
        static readonly string[] Commands={"MRTDENSITY","MRTTABLEMASK","MRTFONT","MRTFONTALL","MRTFONTCHECK","MRTJOIN","MRTJOINTOL","MRTZ0","MRTZ0PL","MRTZ0SCAN","MRTZ0CLEAN","MRTZ0HELP","MRTBREAK","MRTALIASES","MRTTOOLS","MRTPIPE","MRTSCALE","MRTR0","MRTSEL","MRTISO","MRTUNISO","MRTVIEW","MRTISOADD","MRTISODIAG","MRTSELLAYER","MRTALIGNR","MRTBLOCKLAY","MRTBLOCKMERGE","MRTLEGEND","MRTLEGEDIT","MRTLEGSYNC","MRTROOMS","MRTHELP"};
        public AliasForm(string pgp){MedGas2024.UI.UiTheme.Attach(this);path=pgp;Text="Mr.T - Phím lệnh (alias)";Width=620;Height=255;StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
            Controls.Add(new Label{Left=15,Top=15,Width=570,Height=35,Text="Gõ alias rồi Enter. Ví dụ: 6 + Enter gọi MRTJOIN. Có thể nhập lệnh AutoCAD khác."});Controls.Add(new Label{Left=16,Top=62,Width=90,Text="Alias:"});alias.SetBounds(105,58,100,26);alias.Text="6";Controls.Add(alias);
            Controls.Add(new Label{Left=220,Top=62,Width=55,Text="Lệnh:"});command.SetBounds(275,58,270,26);command.Items.AddRange(Commands);command.Text="MRTJOIN";Controls.Add(command);
            state.SetBounds(16,97,570,42);Controls.Add(state);alias.TextChanged+=(s,e)=>RefreshState();command.TextChanged+=(s,e)=>RefreshState();RefreshState();
            var ok=new Button{Text="Lưu alias",Left=380,Top=158,Width=100};var cancel=new Button{Text="Đóng",Left=490,Top=158,Width=90,DialogResult=DialogResult.Cancel};Controls.Add(ok);Controls.Add(cancel);CancelButton=cancel;
            ok.Click+=(s,e)=>{try{Save();RefreshState();}catch(System.Exception ex){MessageBox.Show(ex.Message,"Không lưu được alias",MessageBoxButtons.OK,MessageBoxIcon.Error);}};
        }
        static string ReadAscii(byte[] bytes){if(bytes.Length>=2&&((bytes[0]==255&&bytes[1]==254)||(bytes[0]==254&&bytes[1]==255)))throw new InvalidOperationException("acad.pgp dùng UTF-16; công cụ chưa hỗ trợ sửa an toàn. Hãy dùng ALIASEDIT của AutoCAD.");int offset=bytes.Length>=3&&bytes[0]==0xef&&bytes[1]==0xbb&&bytes[2]==0xbf?3:0;return Encoding.Default.GetString(bytes,offset,bytes.Length-offset);}
        string Existing(byte[] bytes,string key){string found=null;foreach(string line in ReadAscii(bytes).Split('\n')){var m=Regex.Match(line,@"^\s*([A-Za-z0-9_-]+)\s*,\s*\*\s*([^\s;]+)");if(m.Success&&String.Equals(m.Groups[1].Value,key,StringComparison.OrdinalIgnoreCase))found=m.Groups[2].Value;}return found;}
        void RefreshState(){try{var b=File.ReadAllBytes(path);string old=Existing(b,alias.Text.Trim());state.Text="Hiện tại: "+(old??"chưa có alias trong acad.pgp")+"\r\nTệp: "+path;}catch(System.Exception ex){state.Text="Không đọc được acad.pgp: "+ex.Message;}}
        void Save(){string key=alias.Text.Trim().ToUpperInvariant();string cmd=command.Text.Trim().TrimStart('_','.','*').ToUpperInvariant();if(!Regex.IsMatch(key,@"^[A-Z0-9_-]+$")||!Regex.IsMatch(cmd,@"^[A-Z0-9_-]+$"))throw new InvalidOperationException("Alias và lệnh chỉ dùng chữ không dấu, số, dấu gạch dưới hoặc gạch ngang.");byte[] before=File.ReadAllBytes(path);string old=Existing(before,key);if(String.Equals(old,cmd,StringComparison.OrdinalIgnoreCase)){MessageBox.Show("Alias đã trỏ tới lệnh này.");return;}
            string warning=old==null?"Gắn "+key+" → "+cmd+"?":"Alias "+key+" hiện trỏ tới "+old+". Đổi thành "+cmd+"?";if(MessageBox.Show(warning+"\r\nSẽ tạo bản sao acad.pgp trước khi lưu.","Xác nhận alias",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            if(!before.SequenceEqual(File.ReadAllBytes(path)))throw new InvalidOperationException("acad.pgp vừa thay đổi, hãy kiểm tra lại trước khi lưu.");string backup=path+".MrT-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".bak";File.Copy(path,backup,false);
            string temp=path+".mrt-"+Guid.NewGuid().ToString("N")+".tmp";try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(before,0,before.Length);var addition=Encoding.ASCII.GetBytes("\r\n; Mr.T CAD Toolset alias\r\n"+key+", *"+cmd+"\r\n");f.Write(addition,0,addition.Length);}if(!before.SequenceEqual(File.ReadAllBytes(path)))throw new InvalidOperationException("acad.pgp vừa thay đổi; chưa ghi đè.");File.Replace(temp,path,null);}finally{if(File.Exists(temp))File.Delete(temp);}try{AcApp.SetSystemVariable("RE-INIT",16);MessageBox.Show("Đã lưu "+key+" → "+cmd+" và nạp lại alias.\r\nBản sao: "+backup);}catch(System.Exception ex){MessageBox.Show("Đã lưu; AutoCAD chưa nạp lại tự động: "+ex.Message+"\r\nDùng REINIT, chọn PGP File.\r\nBản sao: "+backup);}}
    }
}
