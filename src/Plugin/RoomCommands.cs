using System;
using System.Linq;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;
using MedGas2024.UI;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;
namespace MedGas2024.Plugin
{
    public sealed partial class Commands
    {
        [CommandMethod("MRTROOMS",CommandFlags.Modal)]
        public void Rooms()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;RoomForm form=null;
            try{
                Func<RoomEntry,double,bool,bool> place=(row,height,mode)=>{
                    using(var interaction=doc.Editor.StartUserInteraction(form)){
                        var result=doc.Editor.GetPoint("\nChọn tâm tên phòng ["+row.Id+"]: ");if(result.Status!=PromptStatus.OK)return false;
                        var cs=doc.Editor.CurrentUserCoordinateSystem;var axes=cs.CoordinateSystem3d;using(var locked=doc.LockDocument()){var id=RoomService.Place(doc.Database,row,result.Value.TransformBy(cs),axes.Zaxis,axes.Xaxis,height,mode);doc.Editor.WriteMessage("\nMr.T phòng: đã gắn ID "+row.Id+" ("+id.Handle+").");}return true;
                    }
                };
                Action<RoomCatalog,bool> update=(catalog,mode)=>{
                    var plan=RoomService.Inspect(doc.Database,catalog,mode);var rows=plan.Rows.Select(x=>new RoomReviewRow{Handle=x.Id.Handle.ToString(),Id=x.RoomId,Current=x.Current,After=x.After,Status=x.Reason}).ToList();
                    int ready=plan.Rows.Count(x=>x.Ready),skipped=plan.Rows.Count-ready;
                    string summary="Phạm vi: các phòng Mr.T trực tiếp trong Model và Layout; không quét block/Xref. Nguồn: "+(catalog==null?"tên đã lưu trong DWG":"danh sách Excel vừa nạp")+". Hợp lệ: "+ready+"; bỏ qua: "+skipped+". "+(catalog!=null&&catalog.Problems.Count>0?"Excel có "+catalog.Problems.Count+" cảnh báo; xem lại trước khi áp dụng.":"");
                    using(var review=new RoomReviewForm(rows,summary)){if(AcApp.ShowModalDialog(review)!=System.Windows.Forms.DialogResult.OK)return;using(var locked=doc.LockDocument()){int n=RoomService.Apply(doc.Database,plan);doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T phòng: đã cập nhật "+n+" đối tượng; bỏ qua "+skipped+". UNDO để hoàn tác.\n");System.Windows.Forms.MessageBox.Show("Đã cập nhật "+n+" đối tượng; bỏ qua "+skipped+". Dùng UNDO nếu cần.","Mr.T tên phòng");}}
                };
                using(form=new RoomForm(place,update))AcApp.ShowModalDialog(form);
            }catch(System.Exception ex){Report(doc,ex);}
        }
    }
}
