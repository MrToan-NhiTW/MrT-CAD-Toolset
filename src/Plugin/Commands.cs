using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using MedGas2024.Core;
using MedGas2024.Engine;
using MedGas2024.UI;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(MedGas2024.Plugin.Commands))]
[assembly: CommandClass(typeof(MedGas2024.Plugin.Commands))]

namespace MedGas2024.Plugin
{
    public sealed partial class Commands : IExtensionApplication
    {
        public void Initialize()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;
            if(doc!=null) doc.Editor.WriteMessage("\nMr.T CAD tool set 2.0.1.7 đã nạp. Gõ MRTTOOLS hoặc chọn tab Mr.T CAD tool set.\n");RibbonHost.Start();IsolationHost.Start();
        }
        public void Terminate() { IsolationHost.Stop();RibbonHost.Stop(); }
        
        private ManagerForm BuildLibrary(Document doc,Profile profile)
        {
            string info="Bản vẽ: "+doc.Name+" | INSUNITS="+doc.Database.Insunits+" | LTSCALE="+doc.Database.Ltscale;
            ManagerForm form=null;
            form=new ManagerForm(profile,info,p=>SafeCreation.Plan(doc.Database,p),
                p=>{using(var locked=doc.LockDocument()){int n=SafeCreation.Apply(doc.Database,p);doc.Editor.Regen();return "Đã tạo mới "+n+" cặp Layer/Linetype; tên đã có được giữ nguyên.\r\n"+SafeCreation.Plan(doc.Database,p);}},
                p=>{var owner=form.TopLevel?form:form.TopLevelControl as System.Windows.Forms.Form;using(var interaction=doc.Editor.StartUserInteraction(owner??form)){var point=doc.Editor.GetPoint("\nChọn điểm đặt bảng mẫu: ");if(point.Status!=PromptStatus.OK)return;using(var locked=doc.LockDocument()){SafeCreation.Preview(doc.Database,p,point.Value.TransformBy(doc.Editor.CurrentUserCoordinateSystem));doc.Editor.Regen();}}},
                p=>SyncService.FitTextSpacing(doc.Database,p),ChooseColor);
            form.ErrorHandler=ex=>Report(doc,ex);return form;
        }
        private void OpenLibrary()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;using(var form=BuildLibrary(doc,new Profile{UnitTag="BASE",AutoFitOnSync=true}))AcApp.ShowModalDialog(form);
        }
        private static ObjectId[] Preselected(Document doc)
        {
            var selection=doc.Editor.SelectImplied();
            if(selection.Status!=PromptStatus.OK)return new ObjectId[0];
            using(selection.Value)return selection.Value.GetObjectIds();
        }
        [CommandMethod("MRTLTYPE",CommandFlags.Modal|CommandFlags.UsePickSet)]
        public void OpenManager()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;var selected=Preselected(doc);RibbonHost.Restore(false);
            try
            {
                using(var form=new DrawingManagerForm(DrawingService.Read(doc.Database),()=>DrawingService.Read(doc.Database),(row,edit)=>
                {
                    DrawingService.PrepareEdit(doc.Database,row,edit);
                    string plan=DrawingService.EditPlan(row,edit);
                    if(System.Windows.Forms.MessageBox.Show(plan+"\r\n\r\nCập nhật định nghĩa này vào DWG?","Xem thay đổi / phạm vi ảnh hưởng",System.Windows.Forms.MessageBoxButtons.YesNo,System.Windows.Forms.MessageBoxIcon.Question)!=System.Windows.Forms.DialogResult.Yes)return;
                    using(var locked=doc.LockDocument()){DrawingService.Update(doc.Database,row,edit);doc.Editor.Regen();}
                },(rows,edit)=>
                {
                    using(var locked=doc.LockDocument())
                    {
                        var plan=BatchService.Inspect(doc.Database,rows,edit);using(var review=new BatchReviewForm(plan.Rows,plan.Summary,plan.CanApply))
                        {
                            if(review.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return false;
                            int count=BatchService.Apply(doc.Database,plan,edit);doc.Editor.Regen();AutoSyncLegends(doc);doc.Editor.WriteMessage("\nMr.T: Đã xử lý "+count+" Layer. Giữ hình học, Scale và Width.\n");return true;
                        }
                    }
                },ChooseColor))
                {
                    AcApp.ShowModalDialog(form);
                    if(form.DialogResult==System.Windows.Forms.DialogResult.Abort)OpenLibrary();
                    else if(form.DialogResult==System.Windows.Forms.DialogResult.Retry)RunAssignment(doc,selected);
                    else if(form.DialogResult==System.Windows.Forms.DialogResult.Yes)RunScale(doc,selected);
                    else if(form.DialogResult==System.Windows.Forms.DialogResult.No)RunDirection(doc,selected);
                }
            }
            catch(System.Exception ex){Report(doc,ex);}
        }
        private static bool ApplyLabels(Document doc,LabelScope scope,LabelEdit edit)
        {
            using(var locked=doc.LockDocument())
            {
                var plan=LabelService.Inspect(doc.Database,scope,edit);var rows=plan.Items.Select(x=>new LabelChangeView{Handle=x.Handle,Label=x.Label,Layer=x.Layer,Before=x.Before,After=x.After}).ToList();
                using(var review=new LabelReviewForm(plan.Summary,rows))
                {
                    if(review.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return false;
                    int count=LabelService.Apply(doc.Database,plan,edit);doc.Editor.Regen();AutoSyncLegends(doc);doc.Editor.WriteMessage("\nMr.T: Đã áp dụng cho "+count+" đường theo cụm nhãn; bỏ qua "+plan.Skipped.Count+".\n");return true;
                }
            }
        }
        private void OpenLabelManager()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;RibbonHost.Restore(false);string focus=null;bool system=false;var first=Preselected(doc);var draft=new Profile{UnitTag="BASE",AutoFitOnSync=true};if(first.Length==1)try{focus=LabelService.KeyFromPipe(doc.Database,first[0],false);}catch(InvalidOperationException){}
            try
            {
                while(true)
                {
                    using(var form=new LabelManagerForm(LabelService.Read(doc.Database),()=>LabelService.Read(doc.Database),(scope,edit)=>ApplyLabels(doc,scope,edit),ChooseColor,ScaleSettings(doc),focus,system,()=>BuildLibrary(doc,draft)))
                    {
                        AcApp.ShowModalDialog(form);system=form.SystemMode;if(form.CreationDraft!=null)draft=form.CreationDraft;
                        if(form.DialogResult==System.Windows.Forms.DialogResult.OK&&form.RequestedPickScope!=null){var pick=PipeSelection.FromLabels(doc.Database,form.RequestedPickScope,PickSpace(doc));if(form.IsolateRequested)IsolationHost.Isolate(doc,pick.Ids,PickSpace(doc));else SetPicked(doc,pick);break;}
                        if(form.DialogResult==System.Windows.Forms.DialogResult.Ignore){AddFamilySizes(doc,first);first=new ObjectId[0];continue;}
                        if(form.DialogResult==System.Windows.Forms.DialogResult.Retry){var picked=doc.Editor.GetEntity("\nChọn một ống để tìm toàn bộ đường cùng nhãn: ");if(picked.Status==PromptStatus.OK)try{focus=LabelService.KeyFromPipe(doc.Database,picked.ObjectId,system);}catch(InvalidOperationException ex){System.Windows.Forms.MessageBox.Show(ex.Message,"Chọn nhãn từ ống");}continue;}
                        if(form.DialogResult==System.Windows.Forms.DialogResult.Abort){OpenManager();focus=null;continue;}
                        if(form.DialogResult==System.Windows.Forms.DialogResult.Yes){OpenLibrary();focus=null;continue;}break;
                    }
                }
            }catch(System.Exception ex){Report(doc,ex);}
        }
        private static ObjectId PickSpace(Document doc)
        {
            if(Convert.ToInt32(AcApp.GetSystemVariable("CVPORT"))==1)return doc.Database.CurrentSpaceId;
            using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction())return ((BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead))[BlockTableRecord.ModelSpace];
        }
        private static void SetPicked(Document doc,PipePick result){doc.Editor.SetImpliedSelection(result.Ids);doc.Editor.WriteMessage("\nMr.T: "+result+"\n");}
        private static void PickFromSource(Document doc,ObjectId[] first)
        {
            ObjectId source=first.Length==1?first[0]:ObjectId.Null;if(source.IsNull){var pick=doc.Editor.GetEntity("\nChọn 1 ống mẫu để pick mọi ống cùng hệ: ");if(pick.Status!=PromptStatus.OK)return;source=pick.ObjectId;}
            SetPicked(doc,PipeSelection.FromSource(doc.Database,source,PickSpace(doc)));
        }
        [CommandMethod("MRTSEL",CommandFlags.Modal|CommandFlags.UsePickSet|CommandFlags.Redraw|CommandFlags.NoUndoMarker)]
        public void PickSameSystem(){var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;try{SelectCombined(doc,Preselected(doc));}catch(System.Exception ex){Report(doc,ex);}}
        [CommandMethod("MRTPICKSYSTEM",CommandFlags.Modal|CommandFlags.UsePickSet|CommandFlags.Redraw|CommandFlags.NoUndoMarker)]
        public void PickSameSystemAlias(){var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc!=null)try{PickFromSource(doc,Preselected(doc));}catch(System.Exception ex){Report(doc,ex);}}
        private static void SelectSimilar(Document doc,ObjectId[] first)
        {
            if(first.Length==0){var result=doc.Editor.GetSelection(new PromptSelectionOptions{MessageForAdding="\nChọn đối tượng mẫu cho Select Similar: "});if(result.Status!=PromptStatus.OK)return;using(result.Value)first=result.Value.GetObjectIds();}
            doc.Editor.SetImpliedSelection(new ObjectId[0]);using(var set=SelectionSet.FromObjectIds(first))doc.Editor.Command("_.SELECTSIMILAR",set,"");doc.Editor.WriteMessage("\nMr.T: Select Similar theo thiết lập chọn giống hiện hành của AutoCAD. Dùng MRTISO để cô lập lựa chọn.\n");
        }
        private static void SelectCombined(Document doc,ObjectId[] first)
        {
            if(first.Length==0){var input=doc.Editor.GetSelection(new PromptSelectionOptions{MessageForAdding="\nChọn một hoặc nhiều mẫu cho Cùng hệ + Select Similar: "});if(input.Status!=PromptStatus.OK)return;using(input.Value)first=input.Value.GetObjectIds();}
            ObjectId space=PickSpace(doc);var original=PipeSelection.InSpace(doc.Database,first,space);if(original.Length==0)return;PipePick family=null;int similarCount=0;
            try
            {
                var union=PickCombination.Expand(original,seeds=>{family=PipeSelection.FromSources(doc.Database,seeds,space);return family.Ids;},seeds=>{var ids=PipeSelection.InSpace(doc.Database,ReadSimilar(doc,seeds),space);similarCount=ids.Length;return ids;});
                var foreign=ForeignLayerObjects(doc,original,space);var combined=PipeSelection.InSpace(doc.Database,union.Concat(foreign),space);doc.Editor.SetImpliedSelection(combined);doc.Editor.WriteMessage("\nMr.T: Chọn hỗn hợp "+combined.Length+" đối tượng duy nhất. Cùng hệ: "+family.Ids.Length+"; Select Similar: "+similarCount+"; cùng Layer mẫu ngoại lai: "+foreign.Length+" (các nhóm có thể trùng). "+family.Scope+". Dùng MRTISO để cô lập.\n");
            }
            catch{try{doc.Editor.SetImpliedSelection(original);}catch(System.Exception restore){doc.Editor.WriteMessage("\nMr.T: Chưa khôi phục được lựa chọn mẫu: "+restore.Message+"\n");}throw;}
        }
        private static ObjectId[] ReadSimilar(Document doc,ObjectId[] samples)
        {
            bool completed=false,failed=false;CommandEventHandler ended=(sender,e)=>{if(String.Equals(e.GlobalCommandName.TrimStart('_','.'),"SELECTSIMILAR",StringComparison.OrdinalIgnoreCase))completed=true;};CommandEventHandler stopped=(sender,e)=>{if(String.Equals(e.GlobalCommandName.TrimStart('_','.'),"SELECTSIMILAR",StringComparison.OrdinalIgnoreCase))failed=true;};
            doc.CommandEnded+=ended;doc.CommandCancelled+=stopped;doc.CommandFailed+=stopped;
            try{doc.Editor.SetImpliedSelection(new ObjectId[0]);using(var set=SelectionSet.FromObjectIds(samples))doc.Editor.Command("_.SELECTSIMILAR",set,"");if(failed||!completed)throw new InvalidOperationException("Select Similar chưa hoàn tất; chưa gộp lựa chọn.");var result=doc.Editor.SelectImplied();if(result.Status!=PromptStatus.OK)result=doc.Editor.SelectPrevious();if(result.Status!=PromptStatus.OK)throw new InvalidOperationException("Select Similar chưa trả về lựa chọn; chưa gộp.");using(result.Value)return result.Value.GetObjectIds();}
            finally{doc.CommandEnded-=ended;doc.CommandCancelled-=stopped;doc.CommandFailed-=stopped;}
        }
        private static void AddFamilySizes(Document doc,ObjectId[] first)
        {
            try{ObjectId seed=first.Length==1?first[0]:ObjectId.Null;if(seed.IsNull){var pick=doc.Editor.GetEntity("\nChọn một ống chuẩn để thêm cỡ cùng hệ: ");if(pick.Status!=PromptStatus.OK)return;seed=pick.ObjectId;}var source=FamilyService.Read(doc.Database,seed);
                using(var form=new FamilySizesForm(source,r=>FamilyService.Inspect(doc.Database,source,r),(r,p)=>{using(var locked=doc.LockDocument()){int count=FamilyService.Apply(doc.Database,source,r,p);doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T: Đã thêm "+count+" cỡ cùng hệ "+source.Label+". Dùng MRTPIPE để gán; ống mẫu và các ống cũ giữ nguyên.\n");return count;}})){form.ErrorHandler=ex=>LogError(ex);AcApp.ShowModalDialog(form);}
            }catch(System.Exception ex){Report(doc,ex);}
        }
        [CommandMethod("MRTADDSIZES",CommandFlags.Modal|CommandFlags.UsePickSet)]public void AddSizesCommand(){var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc!=null)AddFamilySizes(doc,Preselected(doc));}
        [CommandMethod("MRTISO",CommandFlags.Modal|CommandFlags.UsePickSet|CommandFlags.Redraw)]
        public void IsolatePicked(){var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;try{var ids=Preselected(doc);if(ids.Length==0){var result=doc.Editor.GetSelection(new PromptSelectionOptions{MessageForAdding="\nChọn đối tượng để Isolate: "});if(result.Status!=PromptStatus.OK)return;using(result.Value)ids=result.Value.GetObjectIds();}IsolationHost.Isolate(doc,ids,PickSpace(doc));}catch(System.Exception ex){Report(doc,ex);}}
        [CommandMethod("MRTUNISO",CommandFlags.Modal)]
        public void EndIsolation(){var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc!=null)try{IsolationHost.End(doc);}catch(System.Exception ex){Report(doc,ex);}}
        [CommandMethod("MRTGROUP",CommandFlags.Modal|CommandFlags.UsePickSet|CommandFlags.Redraw)]public void Groups(){OpenLabelManager();}
        [CommandMethod("MRTR0",CommandFlags.Modal)]public void AllR0()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;try
            {
                var catalog=LabelService.Read(doc.Database);if(catalog.Members.Count==0){System.Windows.Forms.MessageBox.Show("Chưa có đường chứa nhãn linetype để chuyển.");return;}
                using(var dialog=new LabelEditForm(catalog.Members,catalog.Definitions,ChooseColor,ScaleSettings(doc),"R=0 CHO TOÀN BỘ NHÃN TRONG DWG",true))
                    while(AcApp.ShowModalDialog(dialog)==System.Windows.Forms.DialogResult.OK){if(ApplyLabels(doc,new LabelScope{All=true,Fingerprint=catalog.Fingerprint},dialog.Value))break;}
            }catch(System.Exception ex){Report(doc,ex);}
        }
        [CommandMethod("MRTPIPE",CommandFlags.Modal|CommandFlags.UsePickSet|CommandFlags.Redraw)]
        public void AssignPipe(){var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc!=null)try{RunAssignment(doc,Preselected(doc));}catch(System.Exception ex){Report(doc,ex);}}
        private static void RunAssignment(Document doc,ObjectId[] selected)
        {
            using(var form=new PipePickerForm(DrawingService.Read(doc.Database),selected.Length,ScaleSettings(doc)))
            {
                AcApp.ShowModalDialog(form);
                if(form.DialogResult!=System.Windows.Forms.DialogResult.OK)return;
                ObjectId[] ids=selected;
                if(ids.Length==0)
                {
                    var opts=new PromptSelectionOptions{MessageForAdding="\nChọn đường ống để gán "+form.Selected.Display+": "};
                    var filter=new SelectionFilter(new[]{new TypedValue((int)DxfCode.Start,"LINE,LWPOLYLINE,POLYLINE,ARC,SPLINE")});
                    var selection=doc.Editor.GetSelection(opts,filter);if(selection.Status!=PromptStatus.OK)return;
                    using(selection.Value)ids=selection.Value.GetObjectIds();
                }
                var isolationSpace=PickSpace(doc);var retainedIsolation=IsolationHost.BeforeAssignment(doc,isolationSpace);
                AssignmentResult assigned;
                using(var locked=doc.LockDocument())
                {
                    assigned=DrawingService.Assign(doc.Database,ids,form.Selected.LayerName,form.ColorByLayer,form.KeepScale,form.EntityScale,expected:form.Selected,globalWidth:form.GlobalWidth);
                    doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T: "+assigned+"\n");
                }
                if(assigned.Changed>0)AutoSyncLegends(doc);
                if(form.IsolateAfterAssign&&assigned.AssignedIds.Count>0)
                {
                    try{var pick=PipeSelection.FromSource(doc.Database,assigned.AssignedIds[0],isolationSpace);IsolationHost.AfterAssignment(doc,retainedIsolation,pick.Ids,assigned.AssignedIds,isolationSpace);}
                    catch(System.Exception ex){doc.Editor.WriteMessage("\nMr.T: Gán đã thành công. Chưa Isolate được hệ: "+ex.Message+" Có thể chọn lại và gõ MRTISO.\n");}
                }
                else doc.Editor.SetImpliedSelection(new ObjectId[0]);
            }
        }
        private static ScaleContext ScaleSettings(Document doc)
        {
            int units=(int)doc.Database.Insunits;double mm=1; // User confirmed Model mm; INSUNITS alone must not trigger a /1000 conversion.
            bool layout=Convert.ToInt32(AcApp.GetSystemVariable("TILEMODE"))==0&&Convert.ToInt32(AcApp.GetSystemVariable("PSLTSCALE"))==1;
            return new ScaleContext{UnitMm=mm,GlobalLtscale=doc.Database.Ltscale,LayoutNormalized=layout,Note="Mặc định Model mm theo xác nhận; INSUNITS="+units+" không tự đổi đơn vị. Có thể chọn m/cm riêng. "+" MSLTSCALE="+AcApp.GetSystemVariable("MSLTSCALE")+"; kiểm tra hiển thị Model theo annotation và bản Plot."};
        }
        [CommandMethod("MRTSCALE",CommandFlags.Modal|CommandFlags.UsePickSet)]
        public void ScaleCurves(){var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc!=null)try{RunScale(doc,Preselected(doc));}catch(System.Exception ex){Report(doc,ex);}}
        private static void RunScale(Document doc,ObjectId[] selected)
        {
            var rows=DrawingService.Read(doc.Database);DrawingLayerInfo preferred=null;using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction()){foreach(var id in selected){if(id.IsNull||!id.IsValid||id.IsErased)continue;var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)continue;preferred=rows.FirstOrDefault(r=>r.LayerName==e.Layer&&r.NativeTextHeight>0);if(preferred!=null)break;}}
            using(var form=new ScaleForm(rows,selected.Length,ScaleSettings(doc),false,preferred))
            {
                AcApp.ShowModalDialog(form);if(form.DialogResult!=System.Windows.Forms.DialogResult.OK)return;
                ObjectId[] ids=form.UseLayers?ScaleService.OnLayers(doc.Database,form.LayerNames):selected;
                if(!form.UseLayers&&ids.Length==0){var result=doc.Editor.GetSelection(new PromptSelectionOptions{MessageForAdding="\nChọn đường cần đồng bộ Scale/Width: "});if(result.Status!=PromptStatus.OK)return;using(result.Value)ids=result.Value.GetObjectIds();}
                using(var locked=doc.LockDocument())
                {
                    var plan=ScaleService.Inspect(doc.Database,ids,form.Value);if(plan.States.Count==0){System.Windows.Forms.MessageBox.Show("Không có đường hợp lệ để đồng bộ.","Mr.T Scale / Width");return;}
                    if(System.Windows.Forms.MessageBox.Show(plan.Summary(form.Value)+"\r\n\r\nĐồng bộ phạm vi này?","Xem phạm vi / Scale và Width",System.Windows.Forms.MessageBoxButtons.YesNo,System.Windows.Forms.MessageBoxIcon.Question)!=System.Windows.Forms.DialogResult.Yes)return;
                    int count=ScaleService.Apply(doc.Database,plan,form.Value);doc.Editor.Regen();doc.Editor.SetImpliedSelection(new ObjectId[0]);doc.Editor.WriteMessage("\nMr.T: Đã đồng bộ Scale/Width cho "+count+" đường. Giữ nguyên Layer, Linetype và màu.\n");
                }
            }
        }
        [CommandMethod("MRTFIXTEXT",CommandFlags.Modal|CommandFlags.UsePickSet)]
        public void FixDirection(){var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc!=null)try{RunDirection(doc,Preselected(doc));}catch(System.Exception ex){Report(doc,ex);}}
        private static void RunDirection(Document doc,ObjectId[] selected)
        {
            double factor=1;
            // Estimate Model display size; these labels remain ordinary Model objects.
            if(Convert.ToInt32(AcApp.GetSystemVariable("TILEMODE"))==1&&Convert.ToInt32(AcApp.GetSystemVariable("MSLTSCALE"))==1)
                using(var annotation=doc.Database.Cannoscale)factor=annotation.DrawingUnits/annotation.PaperUnits;
            using(var form=new DirectionForm(selected.Length,factor))
            {
                AcApp.ShowModalDialog(form);if(form.DialogResult!=System.Windows.Forms.DialogResult.OK)return;
                ObjectId[] ids=selected;
                if(ids.Length==0)
                {
                    var selection=doc.Editor.GetSelection(new PromptSelectionOptions{MessageForAdding="\nChọn các tuyến cần sửa hướng chữ: "},new SelectionFilter(new[]{new TypedValue((int)DxfCode.Start,"LINE,LWPOLYLINE,POLYLINE")}));
                    if(selection.Status!=PromptStatus.OK)return;using(selection.Value)ids=selection.Value.GetObjectIds();
                }
                using(var locked=doc.LockDocument())
                {
                    var plan=DirectionService.Inspect(doc.Database,ids,form.Value);
                    if(plan.Items.Count==0){System.Windows.Forms.MessageBox.Show(plan.Summary(form.Value),"Không có tuyến phù hợp");return;}
                    if(System.Windows.Forms.MessageBox.Show(plan.Summary(form.Value)+"\r\n\r\nÁp dụng cho phạm vi này?","Mr.T Sửa hướng chữ tuyến",System.Windows.Forms.MessageBoxButtons.YesNo,System.Windows.Forms.MessageBoxIcon.Question)!=System.Windows.Forms.DialogResult.Yes)return;
                    int count=DirectionService.Apply(doc.Database,plan,form.Value);doc.Editor.Regen();doc.Editor.SetImpliedSelection(new ObjectId[0]);doc.Editor.WriteMessage("\nMr.T: Đã xử lý "+count+" tuyến. Ngang trái sang phải; dọc trên xuống dưới. Linetype generation Disabled. Sau sửa hình tuyến, chạy lại MRTFIXTEXT.\n");
                }
            }
        }
        private static void LogError(System.Exception ex)
        {
            string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MrT","CADToolset","Logs");
            try{Directory.CreateDirectory(folder);File.AppendAllText(Path.Combine(folder,"drawing-errors.log"),DateTimeOffset.Now+" | "+ex.Data["MrT.Stage"]+"\r\n"+ex+"\r\n");}catch{}
        }
        private static void Report(Document doc,System.Exception ex)
        {
            LogError(ex);doc.Editor.WriteMessage("\nMr.T: "+ex.Message+"\n");System.Windows.Forms.MessageBox.Show(ex.Message,"Mr.T — chưa thực hiện thay đổi");
        }
        [CommandMethod("MRTRIBBON",CommandFlags.Modal)]
        public void ShowRibbon() { RibbonHost.Restore(true); }
        [CommandMethod("MRTTOOLS",CommandFlags.Modal|CommandFlags.UsePickSet|CommandFlags.Redraw)]
        public void OpenToolset() { OpenLabelManager(); }
        [CommandMethod("MRTHELP",CommandFlags.Modal)]
        public void Help() { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"Help","index.html")){UseShellExecute=true}); }
        [CommandMethod("KHIYTE",CommandFlags.Modal|CommandFlags.UsePickSet|CommandFlags.Redraw)]
        public void Alias() { OpenLabelManager(); }
        private static int? ChooseColor(int initial)
        {
            var dialog=new Autodesk.AutoCAD.Windows.ColorDialog();
            dialog.IncludeByBlockByLayer=false;
            dialog.SetDialogTabs(Autodesk.AutoCAD.Windows.ColorDialog.ColorTabs.ACITab);
            dialog.Color=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci,(short)initial);
            if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK) return null;
            int value=dialog.Color.ColorIndex;
            if(value<1||value>255) throw new InvalidDataException("Chọn màu ACI từ 1 đến 255 cho Layer.");
            return value;
        }
        private static string PlanText(Database db,Profile p)
        {
            var s=new StringBuilder(); var plan=SyncService.Inspect(db,p);
            s.AppendLine("Font: "+SyncService.ResolvedFont(db,p)+" | Đậm: "+p.FontBold+" | Style riêng: "+p.StyleName+" | Tiền tố: "+p.LayerPrefix);
            s.AppendLine(SyncService.FontSummary(db,p));
            s.AppendLine("KẾ HOẠCH: LAYER ← LINETYPE | LINETYPE / LAYER | CHI TIẾT");
            foreach(var row in plan) s.AppendLine(row.LayerName+" ← "+row.Name+" | "+row.Linetype+" / "+row.Layer+" | "+row.Detail);
            return s.ToString();
        }
    }
}
