using System;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Engine;
using MedGas2024.UI;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;
namespace MedGas2024.Plugin
{
    public sealed partial class Commands
    {
        static string LegendOutcome(LegendPipePlan p)
        {
            return "Bảng ký hiệu: cập nhật "+p.Changes.Count+" mẫu ống; đã cùng nét "+p.Already+"; bỏ qua "+p.Skipped.Count+"."
                +(p.Skipped.Count==0?"":"\r\n"+String.Join("\r\n",p.Skipped.Take(10))+(p.Skipped.Count>10?"\r\nXem dòng lệnh để đọc các lý do còn lại.":""));
        }
        static void LogLegend(Autodesk.AutoCAD.ApplicationServices.Document doc,LegendPipePlan p)
        {
            doc.Editor.WriteMessage("\nMr.T: "+LegendOutcome(p).Replace("\r\n"," | ")+"\n");
            foreach(var s in p.Skipped)doc.Editor.WriteMessage("\nMr.T bảng bỏ qua: "+s);
            foreach(var x in p.Changes)doc.Editor.WriteMessage("\nMr.T bảng "+x.Table.Handle+": "+x.Label+" ← "+x.Layer+".");
        }
        // Pipe linetype/colour edits used to leave legend tables stale until MRTLEGSYNC was run
        // by hand. Follow-up sync is silent, only touches rows whose source is unambiguous, and
        // reports skipped rows on the command line.
        internal static void AutoSyncLegends(Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            try{
                using(var locked=doc.LockDocument()){
                    var plan=LegendPipeSync.Inspect(doc.Database,DensityService.Scan(doc.Database));if(plan.Tables==0)return;
                    if(plan.Changes.Count>0){LegendPipeSync.Apply(doc.Database,plan);doc.Editor.Regen();}
                    if(plan.Changes.Count>0||plan.Skipped.Count>0)LogLegend(doc,plan);
                }
            }catch(System.Exception ex){doc.Editor.WriteMessage("\nMr.T: Chưa tự đồng bộ bảng ký hiệu: "+ex.Message+" Chạy MRTLEGSYNC để xem chi tiết.\n");}
        }
        [CommandMethod("MRTDENSITY",CommandFlags.Modal)]
        public void Density()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;
            try{var scan=DensityService.Scan(doc.Database);DensityPlan plan=null;string outcome="";
                using(var form=new DensityForm(scan,(options,names)=>{plan=DensityService.Preview(doc.Database,names,options);return plan.Rows;},(options,names)=>{
                    using(var locked=doc.LockDocument()){
                        var sources=DensityService.Scan(doc.Database);int count=DensityService.Apply(doc.Database,plan,options,names);
                        try{var legends=LegendPipeSync.Inspect(doc.Database,sources);LegendPipeSync.Apply(doc.Database,legends);outcome=LegendOutcome(legends);LogLegend(doc,legends);}
                        catch(System.Exception ex){outcome="Mật độ ống đã áp dụng, nhưng chưa đồng bộ được bảng: "+ex.Message+". Chạy MRTLEGSYNC để thử lại.";doc.Editor.WriteMessage("\nMr.T: "+outcome);}
                        doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T mật độ: đã xử lý "+count+" nhóm layer/nét; dùng UNDO nếu cần.\n");return count;
                    }},()=>outcome))AcApp.ShowModalDialog(form);
            }catch(System.Exception ex){Report(doc,ex);}
        }
        [CommandMethod("MRTLEGSYNC",CommandFlags.Modal)]
        public void SyncLegendPipes()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;
            try{
                var plan=LegendPipeSync.Inspect(doc.Database,DensityService.Scan(doc.Database));
                string changes=String.Join("\r\n",plan.Changes.Take(12).Select(x=>"• "+x.Label+" ← "+x.Layer+" (bảng "+x.Table.Handle+")"));
                string detail=LegendOutcome(plan)+(changes.Length==0?"":"\r\n\r\nSẽ cập nhật:\r\n"+changes+(plan.Changes.Count>12?"\r\n...":""));
                if(plan.Changes.Count==0){MessageBox.Show(detail,"Đồng bộ mẫu ống trong bảng");LogLegend(doc,plan);return;}
                if(MessageBox.Show(detail+"\r\n\r\nGiữ tỷ lệ, độ rộng và cỡ mẫu hiện có. Bảng trong block dùng chung sẽ ảnh hưởng mọi bản chèn. Áp dụng?","Xem trước đồng bộ mẫu ống",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
                using(var locked=doc.LockDocument()){int n=LegendPipeSync.Apply(doc.Database,plan);doc.Editor.Regen();LogLegend(doc,plan);MessageBox.Show("Đã cập nhật "+n+" mẫu. "+LegendOutcome(plan)+"\r\nDùng UNDO nếu cần.","Mr.T");}
            }catch(System.Exception ex){Report(doc,ex);}
        }
    }
}
