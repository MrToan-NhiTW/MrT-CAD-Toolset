using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
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
        [CommandMethod("MRTLEGEND",CommandFlags.Modal|CommandFlags.UsePickSet)]
        public void OpenLegend(){Legend(false);}
        [CommandMethod("MRTLEGEDIT",CommandFlags.Modal|CommandFlags.UsePickSet)]
        public void EditLegend(){Legend(true);}
        [CommandMethod("MRTTABLEMASK",CommandFlags.Modal|CommandFlags.UsePickSet)]
        public void TableMask()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;try{
                var ids=Preselected(doc);var tables=new List<ObjectId>();
                using(var scan=doc.Database.TransactionManager.StartOpenCloseTransaction())foreach(var id in ids.Distinct())if(!id.IsNull&&id.IsValid&&!id.IsErased&&scan.GetObject(id,OpenMode.ForRead) is Table)tables.Add(id);
                if(tables.Count==0){var opts=new PromptSelectionOptions{MessageForAdding="\nChọn bảng TABLE cần tạo/cập nhật WIPEOUT: "};var filter=new SelectionFilter(new[]{new TypedValue((int)DxfCode.Start,"ACAD_TABLE")});var result=doc.Editor.GetSelection(opts,filter);if(result.Status!=PromptStatus.OK)return;using(result.Value)tables.AddRange(result.Value.GetObjectIds());}
                int created=0,updated=0,failed=0;
                using(var locked=doc.LockDocument())foreach(var id in tables.Distinct()){try{if(LegendService.RefreshMask(doc.Database,id))updated++;else created++;}catch(System.Exception ex){failed++;doc.Editor.WriteMessage("\nMr.T WIPEOUT bỏ qua bảng "+id.Handle+": "+ex.Message);}}
                doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T WIPEOUT: tạo "+created+", cập nhật "+updated+", bỏ qua "+failed+" bảng. Bảng và vùng che đã đưa lên trên đối tượng hiện có. Dùng UNDO để hoàn tác; sau resize/đối tượng mới, chạy lại nếu cần.\n");
            }catch(System.Exception ex){Report(doc,ex);}
        }
        [CommandMethod("MRTRESTORERAW",CommandFlags.Modal)]
        public void RestoreReadableNames()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;try
            {
                var catalog=LegacyMapping.Read(doc.Database);if(catalog.Rows.Count==0){doc.Editor.WriteMessage("\nMr.T: Không có nét R=0 tên mã đang dùng trên các đường trực tiếp để đối chiếu.\n");return;}
                Func<IList<LegacyMapRow>,bool> manual=rows=>
                {
                    var plan=LegacyMapping.Inspect(doc.Database,rows,catalog.Fingerprint);using(var review=new BatchReviewForm(plan.Preview,plan.Summary,true))
                    {
                        if(AcApp.ShowModalDialog(review)!=System.Windows.Forms.DialogResult.OK)return false;using(var locked=doc.LockDocument()){int n=LegacyMapping.Apply(doc.Database,rows,plan);doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T: Đã ánh xạ "+n+" đường. UNDO để hoàn tác.\n");}return true;
                    }
                };
                Func<bool> automatic=()=>
                {
                    var plan=LegacyRNames.Read(doc.Database);var rows=plan.Select(r=>new BatchPreviewRow{LayerBefore="Giữ nguyên",LayerAfter="Giữ nguyên",TypeBefore=r.Before,TypeAfter=r.After??"Chưa xác định",TextBefore=r.Label,TextAfter=r.Label,Detail=r.State,Blocked=!r.Ready}).ToList();string summary="Khôi phục tự động theo Layer: hợp lệ "+plan.Count(r=>r.Ready)+"; bỏ qua "+plan.Count(r=>!r.Ready)+". Sao chép định nghĩa R hiện tại vào tên gốc; mọi đối tượng dùng nét gốc cùng thay đổi. Giữ tên Layer. Không xóa nét mã.";using(var review=new BatchReviewForm(rows,summary,plan.Any(r=>r.Ready))){if(AcApp.ShowModalDialog(review)!=System.Windows.Forms.DialogResult.OK)return false;using(var locked=doc.LockDocument()){int n=LegacyRNames.Apply(doc.Database,plan);doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T: Đã khôi phục "+n+" tên nét gốc. UNDO để hoàn tác.\n");}return true;}
                };
                using(var form=new LegacyMapForm(catalog,manual,automatic)){form.ErrorHandler=ex=>Report(doc,ex);AcApp.ShowModalDialog(form);}
            }catch(System.Exception ex){Report(doc,ex);}
        }
        void Legend(bool edit)
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;try
            {
                ObjectId target=ObjectId.Null;LegendForm form=null;var spec=new LegendSpec{DatabaseKey=LegendService.DatabaseKey(doc.Database),DrawingFactor=Convert.ToInt32(AcApp.GetSystemVariable("CVPORT"))==1?1:100,UnitMode=Convert.ToInt32(AcApp.GetSystemVariable("CVPORT"))==1?3:1};var first=Preselected(doc);
                Func<ObjectId,LegendSpec> load=id=>{id=LegendService.ResolveLegend(doc.Database,id);if(id.IsNull)throw new InvalidOperationException("Không xác định được bảng Mr.T từ đối tượng đã chọn.");var s=LegendService.Read(doc.Database,id);target=id;return s;};
                if(edit){ObjectId id=ObjectId.Null;if(first.Length>0){var tables=first.Select(x=>LegendService.ResolveLegend(doc.Database,x)).Where(x=>!x.IsNull).Distinct().ToArray();if(tables.Length==1)id=tables[0];}if(id.IsNull){var p=new PromptEntityOptions("\nChọn bảng ký hiệu hoặc vùng che Mr.T để sửa: ");p.SetRejectMessage("\nChọn TABLE, WIPEOUT hoặc block bảng Mr.T cũ.");p.AddAllowedClass(typeof(Table),true);p.AddAllowedClass(typeof(BlockReference),true);p.AddAllowedClass(typeof(Wipeout),true);var r=doc.Editor.GetEntity(p);if(r.Status!=PromptStatus.OK)return;id=r.ObjectId;}spec=load(id);}
                else if(first.Length>0){var tables=first.Select(x=>LegendService.ResolveLegend(doc.Database,x)).Where(x=>!x.IsNull).Distinct().ToArray();if(tables.Length==1)spec=load(tables[0]);}
                Func<LegendSpec> open=()=>{using(var interaction=doc.Editor.StartUserInteraction(form)){var p=new PromptEntityOptions("\nChọn bảng ký hiệu hoặc vùng che Mr.T: ");p.SetRejectMessage("\nChọn TABLE, WIPEOUT hoặc block bảng Mr.T cũ.");p.AddAllowedClass(typeof(Table),true);p.AddAllowedClass(typeof(BlockReference),true);p.AddAllowedClass(typeof(Wipeout),true);var r=doc.Editor.GetEntity(p);return r.Status==PromptStatus.OK?load(r.ObjectId):null;}};
                Func<int,IList<LegendRow>> capture=mode=>
                {
                    using(var interaction=doc.Editor.StartUserInteraction(form))
                    {
                        var rows=new List<LegendRow>();
                        if(mode==3){var options=new PromptEntityOptions("\nChọn lại MỘT Line/Polyline mẫu ống: ");options.SetRejectMessage("\nChọn Line hoặc Polyline 2D.");options.AddAllowedClass(typeof(Line),true);options.AddAllowedClass(typeof(Polyline),true);var result=doc.Editor.GetEntity(options);if(result.Status==PromptStatus.OK)rows.Add(LegendService.Capture(doc.Database,new[]{result.ObjectId},true));}
                        else if(mode==0){var options=new PromptSelectionOptions{MessageForAdding="\nChọn các Line/Polyline làm mẫu ống, mỗi đối tượng một dòng: "};var filter=new SelectionFilter(new[]{new TypedValue((int)DxfCode.Start,"LINE,LWPOLYLINE")});var result=doc.Editor.GetSelection(options,filter);if(result.Status==PromptStatus.OK)using(result.Value){foreach(var id in result.Value.GetObjectIds())rows.Add(LegendService.Capture(doc.Database,new[]{id},true));}}
                        else{var result=doc.Editor.GetSelection(new PromptSelectionOptions{MessageForAdding=mode==2?"\nChọn các hình để gộp làm MỘT ký hiệu: ":"\nChọn các block/hình, mỗi đối tượng thành một dòng: "});if(result.Status!=PromptStatus.OK)return rows;using(result.Value){var ids=result.Value.GetObjectIds();if(mode==2)rows.Add(LegendService.Capture(doc.Database,ids,false));else foreach(var id in ids)rows.Add(LegendService.Capture(doc.Database,new[]{id},false));}}return rows;
                    }
                };
                Func<LegendSpec,bool> write=current=>
                {
                    using(var interaction=doc.Editor.StartUserInteraction(form))
                    {
                        var position=Autodesk.AutoCAD.Geometry.Point3d.Origin;if(target.IsNull){var point=doc.Editor.GetPoint("\nChọn góc trên trái của bảng ký hiệu: ");if(point.Status!=PromptStatus.OK)return false;position=point.Value.TransformBy(doc.Editor.CurrentUserCoordinateSystem);}var ucs=doc.Editor.CurrentUserCoordinateSystem.CoordinateSystem3d;
                        using(var locked=doc.LockDocument()){target=LegendService.Write(doc.Database,current,position,ucs.Zaxis,ucs.Xaxis,target);doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T: Đã cập nhật TABLE, WIPEOUT và GROUP. MRTLEGEDIT để sửa; UNDO để hoàn tác.\n");}return true;
                    }
                };
                using(form=new LegendForm(spec,capture,write,open,()=>target=ObjectId.Null)){form.ErrorHandler=ex=>Report(doc,ex);AcApp.ShowModalDialog(form);}
            }catch(System.Exception ex){Report(doc,ex);}
        }
    }
}
