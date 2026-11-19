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
                "\nTên mới: "+tag.NewName+"\nChữ lần cuối công cụ đã ghi: "+tag.LastText+
                (String.IsNullOrEmpty(tag.Source)?"":"\nNhãn riêng cho chữ trong block: "+tag.Source);
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
                    var rows=hits.Select(h=>new RoomReviewRow{Handle=h.Address,Current=h.Text,Id=h.Layer,Status=h.Status,
                        Action=h.Status!=null?RoomNameMatcher.ActionNone:h.NeedsLabel?RoomNameMatcher.ActionLabel:RoomNameMatcher.ActionAdopt}).ToList();
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
                            using(doc.LockDocument())RoomService.ApplyMatches(doc.Database,space,new[]{hit},mode,target,style,false,null);
                        }
                        doc.Editor.Regen();return true;
                    }
                };
                Action<RoomCatalog,bool,string,string,string> scan=(catalog,mode,source,target,style)=>{
                    var db=doc.Database;var space=db.CurrentSpaceId;
                    var found=RoomService.ScanNames(db,space,source);
                    Func<RoomReviewChoices,RoomMatchOptions> options=c=>new RoomMatchOptions{CreateLabels=c.CreateLabels,AllowRepeats=c.AllowRepeats};
                    List<RoomHit> preview=null;Dictionary<string,RoomHit> ready=null;
                    Func<RoomReviewChoices,RoomReviewPage> build=c=>{
                        preview=RoomNameMatcher.Match(found,catalog,mode,options(c));
                        ready=preview.Where(h=>h.Ready).ToDictionary(h=>h.Address,StringComparer.OrdinalIgnoreCase);
                        var labels=ready.Values.Where(h=>h.NeedsLabel).ToList();
                        // Turning the old layer off is offered only when every label is for text
                        // stored on that layer itself; layer 0 would hide whole blocks.
                        c.HideLayer=source;
                        c.CanHide=source!=null&&source!="0"&&!String.Equals(source,target,StringComparison.OrdinalIgnoreCase)&&labels.Count>0&&labels.All(h=>String.Equals(h.StoredLayer,source,StringComparison.OrdinalIgnoreCase));
                        var rows=preview.Where(h=>h.Tag==null||h.OnSourceLayer||catalog.Find(h.Tag)!=null).Select(h=>new RoomReviewRow{
                            OldName=h.Entry==null?"":h.Entry.OldName,NewName=h.Entry==null?(h.ExcelTargets??""):h.Entry.NewName,
                            CanPlace=h.Entry!=null&&!String.IsNullOrWhiteSpace(RoomRules.Desired(h.Entry,mode)),
                            Handle=h.Id.IsNull?"":h.Address,Id=h.Entry==null?h.Tag:h.Entry.Id,Current=h.Text,Layer=h.Layer,
                            After=h.Ready?RoomRules.Desired(h.Entry,mode):"",Action=h.Action,Ready=h.Ready,Status=h.Status}).ToList();
                        int adopt=ready.Count-labels.Count,missing=preview.Count(h=>h.Id.IsNull),blocked=preview.Count(h=>!h.Id.IsNull&&h.Tag==null&&!h.Ready);
                        string summary="Không gian hiện tại · Layer chữ cũ: "+(source??"tất cả layer nội bộ")+" · Layer đích: "+target+" · Kiểu chữ: "+style+
                            ". Sẵn sàng "+ready.Count+" (gắn trực tiếp "+adopt+", tạo nhãn riêng "+labels.Count+"); cần xử lý "+blocked+"; Excel chưa tìm thấy "+missing+"."+
                            (labels.Count>0?" Nhãn riêng là MTEXT có ID đặt đúng tâm chữ trong từng lần chèn block; chữ gốc vẫn còn bên dưới.":"")+
                            (String.Equals(source,target,StringComparison.OrdinalIgnoreCase)?" Lưu ý: layer đích trùng layer chữ cũ nên không thể tắt riêng chữ cũ; nên chọn layer đích khác (vd. "+RoomCatalog.NewLayer+").":"");
                        return new RoomReviewPage{Rows=rows,Summary=summary};
                    };
                    var choices=new RoomReviewChoices();
                    var first=build(choices);
                    using(var review=new RoomReviewForm(first.Rows,first.Summary,choices,build))
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
                        var final=review.Choices;
                        using(doc.LockDocument())
                        {
                            var current=RoomNameMatcher.Match(RoomService.ScanNames(db,space,source),catalog,mode,options(final)).Where(h=>h.Ready).ToList();
                            if(space!=db.CurrentSpaceId||current.Count!=ready.Count||current.Any(h=>!ready.ContainsKey(h.Address)||
                                h.Text!=ready[h.Address].Text||h.Entry.Id!=ready[h.Address].Entry.Id||h.NeedsLabel!=ready[h.Address].NeedsLabel))
                                throw new InvalidOperationException("Danh sách vị trí đã đổi; hãy rà soát lại.");
                            string hide=final.HideSource&&final.CanHide?final.HideLayer:null;
                            var done=RoomService.ApplyMatches(db,space,current,mode,target,style,final.Mask,hide);
                            doc.Editor.Regen();
                            string message="Đã gắn trực tiếp "+done.Adopted+" chữ và tạo "+done.Labels+" nhãn riêng."+
                                (done.HiddenLayer!=null?" Đã tắt layer "+done.HiddenLayer+".":"")+" UNDO để hoàn tác.";
                            doc.Editor.WriteMessage("\nMr.T: "+message);
                            System.Windows.Forms.MessageBox.Show(message,"Mr.T tên phòng");
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
