using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Engine;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;
using Win=System.Windows.Forms;
namespace MedGas2024.Plugin
{
    public sealed partial class Commands
    {
        // Pair of steps in one command: (1) every linear pipe to Z=0, (2) optionally LINE → LWPOLYLINE.
        [CommandMethod("MRTZ0PL",CommandFlags.Modal|CommandFlags.UsePickSet)]
        public void FlattenToPolylines()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;
            try
            {
                var ids=Preselected(doc);
                if(ids.Length==0)
                {
                    var scope=Win.MessageBox.Show("Phạm vi trong không gian hiện tại:\r\nCó: mọi LINE / polyline\r\nKhông: tự chọn\r\nHủy: thoát","Mr.T Z=0 + Polyline",Win.MessageBoxButtons.YesNoCancel,Win.MessageBoxIcon.Question);
                    if(scope==Win.DialogResult.Cancel)return;
                    var filter=new SelectionFilter(new[]{new TypedValue((int)DxfCode.Start,"LINE,LWPOLYLINE,POLYLINE")});
                    if(scope==Win.DialogResult.No){var pick=doc.Editor.GetSelection(new PromptSelectionOptions{MessageForAdding="\nChọn LINE/polyline cần đưa về Z=0: "},filter);if(pick.Status!=PromptStatus.OK)return;using(pick.Value)ids=pick.Value.GetObjectIds();}
                    else{var list=new List<ObjectId>();using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction()){var space=(BlockTableRecord)tr.GetObject(PickSpace(doc),OpenMode.ForRead);foreach(ObjectId id in space)list.Add(id);}ids=list.ToArray();}
                }
                var plan=FlattenService.Inspect(doc.Database,ids);
                if(plan.Changes(true)==0){Win.MessageBox.Show("Không có đường cần đưa về Z=0 hoặc LINE cần đổi.\r\n\r\n"+plan.Summary(false),"Mr.T Z=0 + Polyline");return;}
                var mode=Win.MessageBox.Show(plan.Summary(true)+"\r\n\r\nCó: Z=0 VÀ đổi LINE → polyline.\r\nKhông: chỉ đưa về Z=0, giữ LINE.\r\nHủy: không thay đổi.","Xem trước Z=0 + Polyline",Win.MessageBoxButtons.YesNoCancel,Win.MessageBoxIcon.Question);
                if(mode==Win.DialogResult.Cancel)return;bool convert=mode==Win.DialogResult.Yes;
                if(plan.Changes(convert)==0){Win.MessageBox.Show("Mọi đường đã ở Z=0; không có gì để đổi.","Mr.T Z=0 + Polyline");return;}
                int count;using(var locked=doc.LockDocument()){var again=FlattenService.Inspect(doc.Database,ids);if(again.Changes(convert)!=plan.Changes(convert))throw new InvalidOperationException("Bản vẽ đã đổi sau xem trước; chạy lại MRTZ0PL.");count=FlattenService.Apply(doc.Database,again,convert);}
                doc.Editor.Regen();foreach(var s in plan.Skipped)doc.Editor.WriteMessage("\nMr.T Z0 bỏ qua "+s);
                doc.Editor.WriteMessage("\nMr.T: Đã xử lý "+count+" đường"+(convert?" (Z=0 + LINE→polyline)":" (Z=0)")+". Bỏ qua "+plan.Skipped.Count+". Dùng UNDO để hoàn tác; MRTJOIN để nối, MRTALIGNR để kiểm tra chiều chữ.\n");
            }
            catch(System.Exception ex){Report(doc,ex);}
        }
    }
}
