using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Engine;
using MedGas2024.Core;
using MedGas2024.UI;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;
using Win=System.Windows.Forms;
namespace MedGas2024.Plugin
{
    public sealed partial class Commands
    {
        [CommandMethod("MRTUNIFYR",CommandFlags.Modal)]
        public void NormalizeRNames(){var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;try{using(var locked=doc.LockDocument())ReviewRNames(doc);}catch(System.Exception ex){Report(doc,ex);}}
        void ReviewRNames(Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            var plan=RNameService.Inspect(doc.Database);if(plan.Rows.Count==0){doc.Editor.WriteMessage("\nMr.T: Không tìm thấy nét R mã hoặc biến thể cần thống nhất trên các họ nhận diện được.\n");return;}
            Func<List<string>,bool> review=keys=>{
                var now=RNameService.Inspect(doc.Database);if(now.Fingerprint!=plan.Fingerprint)throw new InvalidOperationException("Bản vẽ đã đổi; đóng và mở lại bảng kiểm tra.");var selected=now.Rows.Where(r=>r.Ready&&keys.Contains(r.Key,StringComparer.OrdinalIgnoreCase)).ToList();if(selected.Count!=keys.Count)throw new InvalidOperationException("Danh sách họ đã đổi; kiểm tra lại.");
                var preview=selected.Select(r=>new BatchPreviewRow{LayerBefore=r.StandardLayer,LayerAfter=r.Layers+" layer",TypeBefore=r.Source,TypeAfter=r.Target,TextBefore=r.Label,TextAfter=r.Label,BeforeState="Lấy chính định nghĩa đang gán cho layer chuẩn",AfterState="Mọi nét cùng họ được gán một định nghĩa chuẩn; giữ nguyên scale từng đối tượng.",Detail=r.Details}).ToList();
                string summary="Hợp nhất "+selected.Count+" họ; "+selected.Sum(r=>r.Layers)+" layer; "+selected.Sum(r=>r.Entities)+" đối tượng, gồm "+selected.Sum(r=>r.Nested)+" đối tượng trong định nghĩa block. Chọn dòng để xem từng nét nguồn.\r\nLấy nét đang gán cho layer chuẩn; các font/nhịp khác đổi theo chuẩn. Khi scale khác nhau, khoảng chữ sẽ khác nhau. Nét cũ chỉ được xóa nếu AutoCAD xác nhận không còn tham chiếu; Xref không xử lý.\r\nThao tác trên bản sao DWG; dùng UNDO nếu cần.";
                using(var form=new BatchReviewForm(preview,summary,true)){if(AcApp.ShowModalDialog(form)!=Win.DialogResult.OK)return false;}
                var result=RNameService.Apply(doc.Database,now,keys);doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T: Đã thống nhất "+result.Families+" họ, "+result.Layers+" layer, "+result.Entities+" đối tượng gán nét riêng. Xóa "+result.Purged+" định nghĩa không còn dùng; giữ "+result.Retained+" định nghĩa AutoCAD chưa cho xóa. Dùng UNDO để hoàn tác.\n");foreach(var name in result.RetainedNames)doc.Editor.WriteMessage("\nMr.T giữ nét chưa thể xóa: "+name);string retained=result.Retained==0?"":"\r\nNét chưa thể xóa (có thể còn tham chiếu):\r\n"+String.Join("\r\n",result.RetainedNames.Take(12))+(result.Retained>12?"\r\n... Xem dòng lệnh để đọc toàn bộ.":"");Win.MessageBox.Show("Đã thống nhất "+result.Families+" họ; cập nhật "+result.Layers+" layer và "+result.Entities+" đối tượng gán nét riêng.\r\nĐã xóa "+result.Purged+" nét không còn dùng; "+result.Retained+" nét AutoCAD chưa cho xóa được giữ."+retained+"\r\nKiểm tra nhịp, font, block và in thử; UNDO nếu cần.","Đồng nhất tên nét R");return true;
            };
            using(var form=new RNameReviewForm(plan.Rows,review)){form.ErrorHandler=ex=>Report(doc,ex);AcApp.ShowModalDialog(form);}
        }
        [CommandMethod("MRTALIGNR",CommandFlags.Modal|CommandFlags.UsePickSet)]
        public void AlignNativeR()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;
            try
            {
                var ids=Preselected(doc);
                if(ids.Length==0)
                {
                    var scope=Win.MessageBox.Show("Chọn phạm vi kiểm tra trong không gian hiện tại:\r\nCó: tất cả đường trên Layer bắt đầu bằng Mr.T\r\nKhông: tự chọn đường\r\nHủy: thoát", "Đồng bộ chiều chữ R",Win.MessageBoxButtons.YesNoCancel,Win.MessageBoxIcon.Question);
                    if(scope==Win.DialogResult.Cancel)return;
                    if(scope==Win.DialogResult.No){var selection=doc.Editor.GetSelection(new PromptSelectionOptions{MessageForAdding="\nChọn đường/PL cần kiểm tra: "});if(selection.Status!=PromptStatus.OK)return;using(selection.Value)ids=selection.Value.GetObjectIds();}
                    else
                    {
                        var list=new List<ObjectId>();using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction())
                        {var space=(BlockTableRecord)tr.GetObject(PickSpace(doc),OpenMode.ForRead);foreach(ObjectId id in space){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||(!(e is Line)&&!(e is Polyline)))continue;var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);if(layer.Name.StartsWith("Mr.T",StringComparison.OrdinalIgnoreCase))list.Add(id);}}
                        ids=list.ToArray();
                    }
                }
                using(var locked=doc.LockDocument())
                {
                    var plan=NativeDirectionService.Inspect(doc.Database,ids);
                    foreach(var skipped in plan.Skipped)doc.Editor.WriteMessage("\nMr.T bỏ qua "+skipped);
                    if(plan.Items.Count==0){Win.MessageBox.Show("Không có tuyến nào đủ điều kiện kiểm tra/sửa. Hãy xem lý do bỏ qua bên dưới.\r\n\r\n"+plan.Summary,"Không có tuyến được xử lý");ReviewRNames(doc);return;}
                    if(plan.Items.All(x=>x.Sign>0)){Win.MessageBox.Show(plan.Summary,"Kết quả kiểm tra chiều R");ReviewRNames(doc);return;}
                    bool mixed=plan.Items.Any(x=>x.Sign==0),split=false;
                    if(mixed)
                    {
                        var result=Win.MessageBox.Show(plan.Summary+"\r\n\r\nCó: cho phép tách polyline cần tách và đảo các đoạn.\r\nKhông: chỉ đảo các tuyến không cần tách.\r\nHủy: không thay đổi.","Xem trước đồng bộ chiều R",Win.MessageBoxButtons.YesNoCancel,Win.MessageBoxIcon.Warning);
                        if(result==Win.DialogResult.Cancel)return;split=result==Win.DialogResult.Yes;
                    }
                    else if(Win.MessageBox.Show(plan.Summary+"\r\n\r\nĐảo các tuyến cần sửa?", "Xem trước đồng bộ chiều R",Win.MessageBoxButtons.YesNo,Win.MessageBoxIcon.Question)!=Win.DialogResult.Yes)return;
                    var replacements=new Dictionary<ObjectId,ObjectId[]>();int count=NativeDirectionService.Apply(doc.Database,plan,split,replacements);IsolationHost.RecordSplit(doc,replacements);doc.Editor.Regen();int untreated=plan.Skipped.Count+(split?0:plan.Items.Count(x=>x.Sign==0));doc.Editor.WriteMessage("\nMr.T: Đã xử lý "+count+" tuyến nguồn. Chưa xử lý "+untreated+"; chi tiết ở dòng lệnh. Có thể UNDO.\n");
                    Win.MessageBox.Show("Đã xử lý "+count+" tuyến nguồn. Chưa xử lý "+untreated+"; xem chi tiết trên dòng lệnh.","Đồng bộ chiều R");ReviewRNames(doc);
                }
            }
            catch(System.Exception ex){Report(doc,ex);}
        }
    }
}
