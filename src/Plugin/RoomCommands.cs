using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices;
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
        static void ZoomRoom(Document doc,RoomHit hit)
        {
            Extents3d bounds;
            using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction())
                bounds=((Entity)tr.GetObject(hit.Id,OpenMode.ForRead)).GeometricExtents;
            bounds.TransformBy(hit.ToWorld);
            using(var view=doc.Editor.GetCurrentView())
            {
                var toWorld=Matrix3d.PlaneToWorld(view.ViewDirection);
                toWorld=Matrix3d.Displacement(view.Target-Point3d.Origin)*toWorld;
                toWorld=Matrix3d.Rotation(-view.ViewTwist,view.ViewDirection,view.Target)*toWorld;
                bounds.TransformBy(toWorld.Inverse());
                double aspect=view.Width/view.Height;
                var size=bounds.MaxPoint-bounds.MinPoint;
                double h=Math.Max(Math.Max(size.Y,size.X/aspect)*3,1);
                view.Height=h;view.Width=h*aspect;
                view.CenterPoint=new Point2d((bounds.MinPoint.X+bounds.MaxPoint.X)/2,(bounds.MinPoint.Y+bounds.MaxPoint.Y)/2);
                doc.Editor.SetCurrentView(view);
            }
        }
        static void InspectRoomTag(Document doc)
        {
            var pick=doc.Editor.GetNestedEntity("\nChọn chữ tên phòng để xem ID: ");
            if(pick.Status!=PromptStatus.OK)return;
            var tag=RoomService.ReadTag(doc.Database,pick.ObjectId);
            string detail=tag==null?"Đối tượng chưa có ID phòng Mr.T.":
                "ID: "+tag.Id+"\nTên gốc khi gắn: "+tag.Original+"\nTên cũ trong danh sách: "+tag.OldName+
                "\nTên mới: "+tag.NewName+"\nChữ lần cuối công cụ đã ghi: "+tag.LastText;
            System.Windows.Forms.MessageBox.Show(detail,"Thông tin phòng · "+pick.ObjectId.Handle);
        }
        [CommandMethod("MRTROOMINFO",CommandFlags.Modal)]
        public void RoomInfo()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;
            if(doc!=null)try{InspectRoomTag(doc);}catch(System.Exception ex){Report(doc,ex);}
        }
        [CommandMethod("MRTROOMS",CommandFlags.Modal)]
        public void Rooms()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;RoomForm form=null;
            try{
                Func<RoomEntry,double,bool,string,string,bool> place=(row,height,mode,layer,style)=>{
                    using(var interaction=doc.Editor.StartUserInteraction(form)){
                        var result=doc.Editor.GetPoint("\nChọn tâm tên phòng ["+row.Id+"]: ");if(result.Status!=PromptStatus.OK)return false;
                        var cs=doc.Editor.CurrentUserCoordinateSystem;var axes=cs.CoordinateSystem3d;using(var locked=doc.LockDocument()){var id=RoomService.Place(doc.Database,row,result.Value.TransformBy(cs),axes.Zaxis,axes.Xaxis,height,mode,layer,style);doc.Editor.WriteMessage("\nMr.T phòng: đã gắn ID "+row.Id+" ("+id.Handle+").");}return true;
                    }
                };
                Action<RoomCatalog,bool> update=(catalog,mode)=>{
                    var plan=RoomService.Inspect(doc.Database,catalog,mode);var rows=plan.Rows.Select(x=>new RoomReviewRow{Handle=x.Id.Handle.ToString(),Id=x.RoomId,Current=x.Current,After=x.After,Status=x.Reason}).ToList();
                    int ready=plan.Rows.Count(x=>x.Ready),skipped=plan.Rows.Count-ready;
                    string summary="Phạm vi: các phòng Mr.T trực tiếp trong Model, Layout và thuộc tính đã gắn của từng block. Nguồn: "+(catalog==null?"tên đã lưu trong DWG":"danh sách Excel vừa nạp")+". Hợp lệ: "+ready+"; bỏ qua: "+skipped+". "+(catalog!=null&&catalog.Problems.Count>0?"Excel có "+catalog.Problems.Count+" cảnh báo; xem lại trước khi áp dụng.":"");
                    using(var review=new RoomReviewForm(rows,summary)){if(AcApp.ShowModalDialog(review)!=System.Windows.Forms.DialogResult.OK)return;using(var locked=doc.LockDocument()){int n=RoomService.Apply(doc.Database,plan);doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T phòng: đã cập nhật "+n+" đối tượng; bỏ qua "+skipped+". UNDO để hoàn tác.\n");System.Windows.Forms.MessageBox.Show("Đã cập nhật "+n+" đối tượng; bỏ qua "+skipped+". Dùng UNDO nếu cần.","Mr.T tên phòng");}}
                };
                Func<RoomEntry,double,bool,string,string,string,bool> find=(row,height,mode,source,target,style)=>{
                    var space=doc.Database.CurrentSpaceId;
                    var hits=RoomService.ScanNames(doc.Database,space,source)
                        .Where(h=>h.OnSourceLayer).ToList();
                    var rows=hits.Select(h=>new RoomReviewRow{Handle=h.Address,Current=h.Text,Id=h.Layer,Status=h.Status}).ToList();
                    using(var dialog=new RoomFindForm(row,rows))
                    {
                        if(AcApp.ShowModalDialog(dialog)!=System.Windows.Forms.DialogResult.OK)return false;
                        if(dialog.SelectedHandle==null)return place(row,height,mode,target,style);
                        var hit=hits.Single(h=>h.Address==dialog.SelectedHandle);
                        if(dialog.PickPoint)
                        {
                            using(var interaction=doc.Editor.StartUserInteraction(form))
                            {
                                ZoomRoom(doc,hit);
                                var point=doc.Editor.GetPoint("\nChọn điểm gắn tên phòng ["+row.Id+"] (Esc để quay lại): ");
                                if(point.Status!=PromptStatus.OK)return false;
                                var cs=doc.Editor.CurrentUserCoordinateSystem;
                                using(doc.LockDocument())
                                    RoomService.Place(doc.Database,row,point.Value.TransformBy(cs),cs.CoordinateSystem3d.Zaxis,cs.CoordinateSystem3d.Xaxis,height,mode,target,style);
                            }
                        }
                        else
                        {
                            hit.Entry=row;
                            using(doc.LockDocument())RoomService.Adopt(doc.Database,space,new[]{hit},mode,target,style);
                        }
                        doc.Editor.Regen();return true;
                    }
                };
                Action<RoomCatalog,bool,string,string,string> scan=(catalog,mode,source,target,style)=>{
                    var db=doc.Database;var space=db.CurrentSpaceId;
                    Func<List<RoomHit>> inspect=()=>RoomNameMatcher.Match(RoomService.ScanNames(db,space,source),catalog,mode);
                    var preview=inspect();
                    var rows=preview.Where(h=>h.Tag==null||h.OnSourceLayer||catalog.Find(h.Tag)!=null).Select(h=>new RoomReviewRow{
                        OldName=h.Entry==null?"":h.Entry.OldName,NewName=h.Entry==null?"":h.Entry.NewName,CanPlace=h.Entry!=null&&!String.IsNullOrWhiteSpace(RoomRules.Desired(h.Entry,mode)),
                        Handle=h.Id.IsNull?"":h.Address,Id=h.Entry==null?h.Tag:h.Entry.Id,Current=h.Text,
                        After=h.Ready?RoomRules.Desired(h.Entry,mode):"",Status=h.Status}).ToList();
                    var ready=preview.Where(h=>h.Ready).ToDictionary(h=>h.Id);
                    string summary="Không gian hiện tại · Layer chữ cũ: "+(source??"tất cả layer nội bộ")+" · Layer đích: "+target+" · Kiểu chữ: "+style+
                        ". Có "+ready.Count+" chữ khớp duy nhất. Gắn trực tiếp vào chữ, giữ vị trí và độ cao.";
                    using(var review=new RoomReviewForm(rows,summary,true))
                    {
                        var answer=AcApp.ShowModalDialog(review);
                        if(answer==System.Windows.Forms.DialogResult.Retry)
                        {
                            var chosen=review.SelectedRoom;
                            var room=catalog.Find(chosen.Id);
                            if(!String.IsNullOrEmpty(chosen.Handle))
                            {
                                var location=preview.First(h=>!h.Id.IsNull&&h.Address==chosen.Handle);
                                using(doc.Editor.StartUserInteraction(form))ZoomRoom(doc,location);
                            }
                            place(room,form.LabelHeight,mode,target,style);return;
                        }
                        if(answer!=System.Windows.Forms.DialogResult.OK)return;
                        using(doc.LockDocument())
                        {
                            var current=inspect().Where(h=>h.Ready).ToList();
                            if(space!=db.CurrentSpaceId||current.Count!=ready.Count||current.Any(h=>!ready.ContainsKey(h.Id)||
                                h.Text!=ready[h.Id].Text||h.Entry.Id!=ready[h.Id].Entry.Id))
                                throw new InvalidOperationException("Danh sách vị trí đã đổi; hãy rà soát lại.");
                            int n=RoomService.Adopt(db,space,current,mode,target,style);
                            doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T: Đã gắn "+n+" tên phòng. UNDO để hoàn tác.");
                            System.Windows.Forms.MessageBox.Show("Đã gắn "+n+" tên phòng.","Mr.T tên phòng");
                        }
                    }
                };
                using(form=new RoomForm(place,update,RoomService.Layers(doc.Database),RoomService.Styles(doc.Database),find,scan))
                {
                    form.InspectTag+=delegate{using(doc.Editor.StartUserInteraction(form))InspectRoomTag(doc);};
                    AcApp.ShowModalDialog(form);
                }
            }catch(System.Exception ex){Report(doc,ex);}
        }
    }
}
