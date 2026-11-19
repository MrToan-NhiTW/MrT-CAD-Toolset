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
            var pick=doc.Editor.GetNestedEntity("\nChọn tên phòng (chữ hoặc block Mr.T_PHONG) để xem ID: ");
            if(pick.Status!=PromptStatus.OK)return;
            var id=pick.ObjectId;var tag=RoomService.ReadTag(doc.Database,id);
            var attributes=new List<string>();
            using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                // A pick on the frame-less block body or on an invisible attribute: use the insert's name attribute.
                var containers=pick.GetContainers();
                var insert=(tr.GetObject(id,OpenMode.ForRead) as AttributeReference)!=null?tr.GetObject(tr.GetObject(id,OpenMode.ForRead).OwnerId,OpenMode.ForRead) as BlockReference:
                    containers.Length>0?tr.GetObject(containers[containers.Length-1],OpenMode.ForRead) as BlockReference:null;
                if(insert!=null)
                    foreach(ObjectId aid in insert.AttributeCollection)
                    {
                        var ar=tr.GetObject(aid,OpenMode.ForRead) as AttributeReference;if(ar==null)continue;
                        attributes.Add(ar.Tag+": "+(ar.IsMTextAttribute?ar.MTextAttribute.Text:ar.TextString));
                        if(tag==null&&ar.Tag==RoomService.TagName){tag=RoomService.ReadTag(doc.Database,aid);id=aid;}
                    }
            }
            string detail=tag==null?"Đối tượng chưa có ID phòng Mr.T.":
                "ID: "+tag.Id+"\nTên gốc khi gắn: "+tag.Original+"\nTên cũ trong danh sách: "+tag.OldName+
                "\nTên mới: "+tag.NewName+"\nChữ lần cuối công cụ đã ghi: "+tag.LastText+
                (String.IsNullOrEmpty(tag.Status)?"":"\nTrạng thái: "+tag.Status)+
                (String.IsNullOrEmpty(tag.Source)?"":"\nĐặt cho chữ cũ trong block: "+tag.Source)+
                (attributes.Count>0?"\n\nThuộc tính block:\n"+String.Join("\n",attributes):"");
            System.Windows.Forms.MessageBox.Show(detail,"Thông tin phòng · "+id.Handle);
        }
        // Room manager: edit placed Mr.T_PHONG blocks; Retry zooms to a room and reopens with the edits kept.
        static void ManageRoomBlocks(Document doc,System.Windows.Forms.IWin32Window owner)
        {
            var fromDrawing=RoomService.ListRoomBlocks(doc.Database);
            if(fromDrawing.Count==0){System.Windows.Forms.MessageBox.Show("Bản vẽ chưa có block "+RoomService.RoomBlockName+". Hãy rà soát layer và gắn trước.","Mr.T quản lý phòng");return;}
            var rows=fromDrawing.Select(r=>r.Copy()).ToList();
            string note="Sửa trực tiếp trong bảng, hoặc chọn nhiều dòng rồi Áp cho dòng đã chọn. Xuất Excel để sửa ngoài rồi Nhập Excel đã sửa. Lưu vào bản vẽ là một bước UNDO.";
            while(true)
            {
                using(var manager=new RoomManagerForm(rows,fromDrawing,note))
                {
                    var answer=AcApp.ShowModalDialog(manager);rows=manager.Rows;
                    if(answer==System.Windows.Forms.DialogResult.Retry&&manager.ZoomHandle!=null)
                    {
                        var box=RoomService.RoomExtents(doc.Database,manager.ZoomHandle);
                        Action look=()=>{ZoomTo(doc,box);doc.Editor.GetString("\nĐang xem phòng "+manager.ZoomHandle+". Nhấn Enter để quay lại bảng quản lý: ");};
                        // From the room form (still open) the editor must be borrowed from it.
                        if(owner!=null){using(doc.Editor.StartUserInteraction(owner.Handle))look();}else look();
                        note="Đã phóng tới phòng "+manager.ZoomHandle+".";continue;
                    }
                    if(answer!=System.Windows.Forms.DialogResult.OK)return;
                    using(doc.LockDocument())
                    {
                        int n=RoomService.SaveRoomBlocks(doc.Database,rows.Where(r=>r.Changed));doc.Editor.Regen();
                        doc.Editor.WriteMessage("\nMr.T: đã cập nhật "+n+" phòng. UNDO để hoàn tác.");
                        System.Windows.Forms.MessageBox.Show("Đã cập nhật "+n+" phòng.","Mr.T quản lý phòng");
                    }
                    return;
                }
            }
        }
        static void ZoomTo(Document doc,Extents3d bounds)
        {
            using(var view=doc.Editor.GetCurrentView())
            {
                var toWorld=Matrix3d.PlaneToWorld(view.ViewDirection);
                toWorld=Matrix3d.Displacement(view.Target-Point3d.Origin)*toWorld;
                toWorld=Matrix3d.Rotation(-view.ViewTwist,view.ViewDirection,view.Target)*toWorld;
                bounds.TransformBy(toWorld.Inverse());
                double aspect=view.Width/view.Height;var size=bounds.MaxPoint-bounds.MinPoint;
                double h=Math.Max(Math.Max(size.Y,size.X/aspect),1);view.Height=h;view.Width=h*aspect;
                view.CenterPoint=new Point2d((bounds.MinPoint.X+bounds.MaxPoint.X)/2,(bounds.MinPoint.Y+bounds.MaxPoint.Y)/2);
                doc.Editor.SetCurrentView(view);
            }
        }
        [CommandMethod("MRTROOMLIST",CommandFlags.Modal)]
        public void RoomList()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;
            if(doc!=null)try{ManageRoomBlocks(doc,null);}catch(System.Exception ex){Report(doc,ex);}
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
                Func<RoomEntry,bool,RoomLabelStyle,bool> place=(row,mode,look)=>{
                    using(var interaction=doc.Editor.StartUserInteraction(form)){
                        var result=doc.Editor.GetPoint("\nChọn tâm tên phòng ["+row.Id+"]: ");if(result.Status!=PromptStatus.OK)return false;
                        var cs=doc.Editor.CurrentUserCoordinateSystem;var axes=cs.CoordinateSystem3d;
                        using(var locked=doc.LockDocument()){var id=RoomService.PlaceBlock(doc.Database,row,result.Value.TransformBy(cs),axes.Zaxis,axes.Xaxis,mode,look);doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T phòng: đã đặt block "+RoomService.RoomBlockName+" ID "+row.Id+".");}
                        return true;
                    }
                };
                Action<RoomCatalog,bool> update=(catalog,mode)=>{
                    var plan=RoomService.Inspect(doc.Database,catalog,mode);var rows=plan.Rows.Select(x=>new RoomReviewRow{Handle=x.Id.Handle.ToString(),Id=x.RoomId,Current=x.Current,After=x.After,Status=x.Reason}).ToList();
                    int ready=plan.Rows.Count(x=>x.Ready),skipped=plan.Rows.Count-ready;
                    string summary="Phạm vi: các phòng Mr.T trực tiếp trong Model, Layout và thuộc tính đã gắn của từng block. Nguồn: "+(catalog==null?"tên đã lưu trong DWG":"danh sách Excel vừa nạp")+". Hợp lệ: "+ready+"; bỏ qua: "+skipped+". "+(catalog!=null&&catalog.Problems.Count>0?"Excel có "+catalog.Problems.Count+" ghi chú/cảnh báo; xem lại trước khi áp dụng.":"");
                    using(var review=new RoomReviewForm(rows,summary)){if(AcApp.ShowModalDialog(review)!=System.Windows.Forms.DialogResult.OK)return;using(var locked=doc.LockDocument()){int n=RoomService.Apply(doc.Database,plan);doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T phòng: đã cập nhật "+n+" đối tượng; bỏ qua "+skipped+". UNDO để hoàn tác.\n");System.Windows.Forms.MessageBox.Show("Đã cập nhật "+n+" đối tượng; bỏ qua "+skipped+". Dùng UNDO nếu cần.","Mr.T tên phòng");}}
                };
                Func<RoomEntry,bool,string,RoomLabelStyle,bool> find=(row,mode,source,look)=>{
                    var space=doc.Database.CurrentSpaceId;
                    var hits=RoomService.ScanNames(doc.Database,space,source).Where(h=>h.OnSourceLayer).ToList();
                    var rows=hits.Select(h=>new RoomReviewRow{Handle=h.Address,Current=h.Text,Id=h.Layer,Status=h.Status,
                        Action=h.Status!=null?RoomNameMatcher.ActionNone:h.Direct?(look.ReplaceOld?RoomNameMatcher.ActionReplace:RoomNameMatcher.ActionBlock):RoomNameMatcher.ActionOver}).ToList();
                    using(var dialog=new RoomFindForm(row,rows))
                    {
                        if(AcApp.ShowModalDialog(dialog)!=System.Windows.Forms.DialogResult.OK)return false;
                        if(dialog.SelectedHandle==null)return place(row,mode,look);
                        var hit=hits.Single(h=>h.Address==dialog.SelectedHandle);
                        if(dialog.PickPoint)
                        {
                            using(var interaction=doc.Editor.StartUserInteraction(form))
                            {
                                ZoomRoom(doc,hit);
                                var point=doc.Editor.GetPoint("\nChọn điểm đặt tên phòng ["+row.Id+"] (Esc để quay lại): ");
                                if(point.Status!=PromptStatus.OK)return false;
                                var cs=doc.Editor.CurrentUserCoordinateSystem;
                                using(doc.LockDocument())
                                    RoomService.PlaceBlock(doc.Database,row,point.Value.TransformBy(cs),cs.CoordinateSystem3d.Zaxis,cs.CoordinateSystem3d.Xaxis,mode,look);
                            }
                        }
                        else
                        {
                            hit.Entry=row;
                            using(doc.LockDocument())RoomService.ApplyBlocks(doc.Database,space,new[]{hit},mode,look,null);
                        }
                        doc.Editor.Regen();return true;
                    }
                };
                Action<RoomCatalog,bool,string,RoomLabelStyle,RoomSettings> scan=(catalog,mode,source,look,remembered)=>{
                    var db=doc.Database;var space=db.CurrentSpaceId;
                    var found=RoomService.ScanNames(db,space,source);
                    Func<RoomReviewChoices,RoomMatchOptions> options=c=>new RoomMatchOptions{CreateLabels=true,Blocks=true,ReplaceOld=look.ReplaceOld,AllowRepeats=c.AllowRepeats,KeepUnmatched=c.KeepUnmatched};
                    List<RoomHit> preview=null;Dictionary<string,RoomHit> ready=null;
                    Func<RoomReviewChoices,RoomReviewPage> build=c=>{
                        preview=RoomNameMatcher.Match(found,catalog,mode,options(c));
                        ready=preview.Where(h=>h.Ready).ToDictionary(h=>h.Address,StringComparer.OrdinalIgnoreCase);
                        var kept=ready.Values.Where(h=>!(h.Direct&&look.ReplaceOld)).ToList();
                        // Turning the old layer off is offered only when every old text that stays is stored
                        // on that layer itself (layer 0 would hide whole blocks) and it is not the target layer.
                        c.HideLayer=source;
                        c.CanHide=source!=null&&source!="0"&&!String.Equals(source,look.Layer,StringComparison.OrdinalIgnoreCase)&&ready.Count>0&&kept.All(h=>String.Equals(h.StoredLayer,source,StringComparison.OrdinalIgnoreCase));
                        var rows=preview.Where(h=>h.Tag==null||h.OnSourceLayer||catalog.Find(h.Tag)!=null).Select(h=>new RoomReviewRow{
                            OldName=h.Entry==null?"":h.Entry.OldName,NewName=(h.Splits!=null&&h.Splits.Count>1)||h.Entry==null?(h.ExcelTargets??""):h.Entry.NewName,
                            CanPlace=h.Entry!=null&&!String.IsNullOrWhiteSpace(RoomRules.Desired(h.Entry,mode)),
                            Handle=h.Id.IsNull?"":h.Address,Id=h.Splits!=null?String.Join(", ",h.Splits.Select(r=>r.Id)):h.Entry==null?h.Tag:h.Entry.Id,Current=h.Text,Layer=h.Layer,
                            After=h.Ready?(h.Splits!=null?String.Join(" | ",h.Splits.Select(r=>RoomRules.Display(r,mode,look.Upper))):RoomRules.Display(h.Entry,mode,look.Upper)):"",
                            Action=h.Action,Ready=h.Ready,Status=h.Status}).ToList();
                        int blocks=ready.Values.Sum(h=>h.Splits==null?1:h.Splits.Count),replaced=ready.Values.Count(h=>h.Direct&&look.ReplaceOld);
                        int missing=preview.Count(h=>h.Id.IsNull),blocked=preview.Count(h=>!h.Id.IsNull&&h.Tag==null&&!h.Ready),review=ready.Values.Where(h=>h.Review).Sum(h=>h.Splits==null?1:h.Splits.Count);
                        string summary="Layer chữ cũ: "+(source??"tất cả layer nội bộ")+" · Layer tên mới: "+look.Layer+" · Kiểu chữ: "+look.Style+" · Cao "+look.Height.ToString("0.###")+
                            (look.Width>0?" · Rộng "+look.Width.ToString("0.###"):"")+(look.Upper?" · CHỮ HOA":"")+" · "+look.Frame+
                            ". Sẵn sàng "+ready.Count+" vị trí → "+blocks+" block "+RoomService.RoomBlockName+" (thay "+replaced+" chữ cũ ngoài block; "+review+" block màu "+look.ReviewColor+" cần kiểm tra, còn lại màu "+look.Color+"); cần xử lý "+blocked+"; Excel chưa tìm thấy "+missing+"."+
                            (String.Equals(source,look.Layer,StringComparison.OrdinalIgnoreCase)?" Lưu ý: layer tên mới trùng layer chữ cũ; nên chọn layer khác (vd. "+RoomCatalog.NewLayer+").":"");
                        return new RoomReviewPage{Rows=rows,Summary=summary};
                    };
                    // 2.0.2.5: repeated codes and rooms missing from Excel are applied by default (flagged red).
                    if(!remembered.RepeatsDecided){remembered.AllowRepeats=true;remembered.KeepUnmatched=true;remembered.RepeatsDecided=true;}
                    var choices=new RoomReviewChoices{AllowRepeats=remembered.AllowRepeats,HideSource=remembered.HideSource,KeepUnmatched=remembered.KeepUnmatched};
                    var first=build(choices);
                    using(var review=new RoomReviewForm(first.Rows,first.Summary,choices,build))
                    {
                        var answer=AcApp.ShowModalDialog(review);
                        remembered.AllowRepeats=choices.AllowRepeats;remembered.HideSource=choices.HideSource;remembered.KeepUnmatched=choices.KeepUnmatched;
                        if(answer==System.Windows.Forms.DialogResult.Retry)
                        {
                            var chosen=review.SelectedRoom;
                            var room=catalog.Find(chosen.Id.Split(',')[0].Trim());
                            if(!String.IsNullOrEmpty(chosen.Handle))
                            {
                                var location=preview.First(h=>!h.Id.IsNull&&h.Address==chosen.Handle);
                                using(doc.Editor.StartUserInteraction(form))ZoomRoom(doc,location);
                            }
                            place(room,mode,look);return;
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
                            var done=RoomService.ApplyBlocks(db,space,current,mode,look,hide);
                            doc.Editor.Regen();
                            string message="Đã tạo "+done.Labels+" block tên phòng (thay "+done.Replaced+" chữ cũ ngoài block, đổi "+done.Upgraded+" nhãn MTEXT bản trước) trên layer "+look.Layer+"."+
                                (done.HiddenLayer!=null?" Đã tắt layer "+done.HiddenLayer+".":"")+" UNDO để hoàn tác.";
                            doc.Editor.WriteMessage("\nMr.T: "+message);
                            System.Windows.Forms.MessageBox.Show(message,"Mr.T tên phòng");
                        }
                    }
                };
                var settingsPath=RoomSettings.DefaultPath;
                using(form=new RoomForm(place,update,RoomService.Layers(doc.Database),RoomService.Styles(doc.Database),find,scan,RoomSettings.Load(settingsPath),s=>s.Save(settingsPath)))
                {
                    form.InspectTag+=delegate{using(doc.Editor.StartUserInteraction(form))InspectRoomTag(doc);};
                    form.ManageRooms+=delegate{ManageRoomBlocks(doc,form);};
                    AcApp.ShowModalDialog(form);
                }
            }catch(System.Exception ex){Report(doc,ex);}
        }
    }
}
