using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;
using Win=System.Windows.Forms;
namespace MedGas2024.Plugin
{
    public sealed partial class Commands
    {
        [CommandMethod("MRTVIEW",CommandFlags.Modal)]
        public void ToggleIsolationContext()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;
            try{IsolationHost.Toggle(doc,PickSpace(doc));}catch(System.Exception ex){Report(doc,ex);}
        }
        [CommandMethod("MRTISODIAG",CommandFlags.Modal)]
        public void DiagnoseIsolation()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;
            try{string text=IsolationHost.Diagnose(doc,PickSpace(doc));doc.Editor.WriteMessage("\nMr.T CHẨN ĐOÁN ISOLATE:\n"+text+"\n");Win.MessageBox.Show(text.Replace("\n","\r\n"),"Mr.T chẩn đoán Isolate / bối cảnh");}catch(System.Exception ex){Report(doc,ex);}
        }
        [CommandMethod("MRTISOADD",CommandFlags.Modal|CommandFlags.UsePickSet)]
        public void AddToIsolation()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;try
            {
                var space=PickSpace(doc);if(!IsolationHost.HasSession(doc,space))throw new InvalidOperationException("Hãy bắt đầu bằng MRTISO trong không gian hiện hành trước.");
                var selected=Preselected(doc);
                {
                    var mode=Win.MessageBox.Show("Có: chọn một hoặc nhiều Layer từ danh sách.\r\nKhông: pick thêm đối tượng đang hiện.\r\nHủy: giữ nguyên.","Bổ sung vào Isolate",Win.MessageBoxButtons.YesNoCancel,Win.MessageBoxIcon.Question);
                    if(mode==Win.DialogResult.Cancel)return;
                    if(mode==Win.DialogResult.No){if(selected.Length==0){var pick=doc.Editor.GetSelection(new PromptSelectionOptions{MessageForAdding="\nChọn thêm đối tượng đang hiện: "});if(pick.Status!=PromptStatus.OK)return;using(pick.Value)selected=pick.Value.GetObjectIds();}}
                    else
                    {
                        var choices=new List<LayerChoice>();using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction()){var layers=(LayerTable)tr.GetObject(doc.Database.LayerTableId,OpenMode.ForRead);var btr=(BlockTableRecord)tr.GetObject(space,OpenMode.ForRead);var counts=new Dictionary<ObjectId,int>();foreach(ObjectId id in btr){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)continue;int n;counts.TryGetValue(e.LayerId,out n);counts[e.LayerId]=n+1;}foreach(ObjectId id in layers){var l=(LayerTableRecord)tr.GetObject(id,OpenMode.ForRead);if(l.IsDependent)continue;int n;counts.TryGetValue(id,out n);choices.Add(new LayerChoice{Name=l.Name,Info="("+n+" đối tượng trực tiếp)"+(l.IsOff?" Tắt":"")+(l.IsFrozen?" Đóng băng":"")});}}
                        string[] names;using(var f=new LayerMultiDialog("Thêm Layer vào vùng đang xem",choices)){if(AcApp.ShowModalDialog(f)!=Win.DialogResult.OK)return;names=f.SelectedNames;}var ids=new List<ObjectId>();var keys=new HashSet<string>(names,StringComparer.OrdinalIgnoreCase);using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction()){var btr=(BlockTableRecord)tr.GetObject(space,OpenMode.ForRead);foreach(ObjectId id in btr){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)continue;var block=e as BlockReference;if(keys.Contains(e.Layer)||(block!=null&&ContainsLayers(tr,block.BlockTableRecord,keys,new HashSet<ObjectId>(),0)))ids.Add(id);}}selected=ids.ToArray();
                    }
                }
                IsolationHost.Add(doc,selected,space);
            }catch(System.Exception ex){Report(doc,ex);}
        }
    }
}
