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
        static void WalkLayers(Transaction tr,ObjectId recordId,Dictionary<string,int> counts,HashSet<ObjectId> path,List<string> warnings,int depth)
        {
            if(depth>32||!path.Add(recordId)){warnings.Add("Bỏ qua block lồng quá sâu hoặc vòng lặp.");return;}
            var b=tr.GetObject(recordId,OpenMode.ForRead) as BlockTableRecord;
            if(b==null||b.IsFromExternalReference||b.IsFromOverlayReference){warnings.Add("Xref không quét bên trong.");path.Remove(recordId);return;}
            foreach(ObjectId id in b){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)continue;int n;counts.TryGetValue(e.Layer,out n);counts[e.Layer]=n+1;var br=e as BlockReference;if(br!=null)WalkLayers(tr,br.BlockTableRecord,counts,path,warnings,depth+1);}
            path.Remove(recordId);
        }
        static bool ContainsLayers(Transaction tr,ObjectId recordId,HashSet<string> names,HashSet<ObjectId> path,int depth)
        {
            if(depth>32||!path.Add(recordId))return false;var b=tr.GetObject(recordId,OpenMode.ForRead) as BlockTableRecord;if(b==null||b.IsFromExternalReference||b.IsFromOverlayReference){path.Remove(recordId);return false;}
            foreach(ObjectId id in b){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)continue;if(names.Contains(e.Layer)){path.Remove(recordId);return true;}var br=e as BlockReference;if(br!=null&&ContainsLayers(tr,br.BlockTableRecord,names,path,depth+1)){path.Remove(recordId);return true;}}
            path.Remove(recordId);return false;
        }
        [CommandMethod("MRTBLOCKLAY",CommandFlags.Modal|CommandFlags.UsePickSet|CommandFlags.Redraw)]
        public void InspectBlockLayers()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;try
            {
                ObjectId seed=ObjectId.Null;var pre=Preselected(doc);if(pre.Length==1)seed=pre[0];if(seed.IsNull){var choice=doc.Editor.GetEntity("\nChọn một block mẫu để xem Layer bên trong: ");if(choice.Status!=PromptStatus.OK)return;seed=choice.ObjectId;}
                var counts=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);var warnings=new List<string>();string blockName;
                using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction()){var b=tr.GetObject(seed,OpenMode.ForRead) as BlockReference;if(b==null)throw new InvalidOperationException("Hãy chọn block trực tiếp trong bản vẽ.");var record=(BlockTableRecord)tr.GetObject(b.BlockTableRecord,OpenMode.ForRead);blockName=record.Name;WalkLayers(tr,b.BlockTableRecord,counts,new HashSet<ObjectId>(),warnings,0);}
                if(counts.Count==0){Win.MessageBox.Show("Block không có thành phần đọc được. "+String.Join(" ",warnings),"Kiểm tra block");return;}
                string[] selected;using(var f=new LayerMultiDialog("Chọn Layer trong block "+blockName,counts.Select(x=>new LayerChoice{Name=x.Key,Info="("+x.Value+" thành phần trong mẫu)",Chosen=true}))){if(AcApp.ShowModalDialog(f)!=Win.DialogResult.OK)return;selected=f.SelectedNames;}
                if(selected.Length==0)return;var names=new HashSet<string>(selected,StringComparer.OrdinalIgnoreCase);var matched=new List<ObjectId>();int parents=0,direct=0;var space=PickSpace(doc);
                using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction())
                {
                    var btr=(BlockTableRecord)tr.GetObject(space,OpenMode.ForRead);foreach(ObjectId id in btr){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)continue;bool layer=names.Contains(e.Layer);var br=e as BlockReference;bool inside=br!=null&&ContainsLayers(tr,br.BlockTableRecord,names,new HashSet<ObjectId>(),0);if(layer||inside){matched.Add(id);if(inside)parents++;else direct++;}}
                }
                doc.Editor.SetImpliedSelection(matched.ToArray());doc.Editor.WriteMessage("\nMr.T: Pick "+matched.Count+" đối tượng trực tiếp: "+parents+" block có thành phần thuộc Layer chọn, "+direct+" đối tượng khác cùng Layer. Chưa chọn riêng hình bên trong block. "+String.Join(" ",warnings.Distinct())+"\n");
                Win.MessageBox.Show("Đã pick "+matched.Count+" đối tượng trong không gian hiện hành, gồm "+parents+" block chứa Layer được chọn.\r\nBlock được chọn nguyên khối; hình bên trong không được pick riêng.\r\n"+String.Join("\r\n",warnings.Distinct())+"\r\nDùng MRTISO để kiểm tra.","Kết quả kiểm tra block");
            }catch(System.Exception ex){Report(doc,ex);}
        }
        static void GatherMovable(Transaction tr,ObjectId recordId,HashSet<string> names,HashSet<ObjectId> visited,List<ObjectId> entities,List<string> blocked)
        {
            if(!visited.Add(recordId))return;var b=(BlockTableRecord)tr.GetObject(recordId,OpenMode.ForRead);
            if(b.IsFromExternalReference||b.IsFromOverlayReference||b.IsAnonymous||b.IsDynamicBlock){blocked.Add(b.Name+": block động, vô danh hoặc Xref");return;}
            foreach(ObjectId id in b)
            {
                var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)continue;
                if(e is AttributeDefinition){blocked.Add("Có Attribute Definition: chưa tự dồn dữ liệu thuộc tính.");continue;}
                if(names.Contains(e.Layer))
                {
                    var l=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);if(l.IsLocked||l.IsDependent)blocked.Add("Layer khóa/phụ thuộc: "+l.Name);else entities.Add(id);
                }
                var br=e as BlockReference;if(br!=null){if(br.IsDynamicBlock)blocked.Add("Block con động: "+br.Name);else GatherMovable(tr,br.BlockTableRecord,names,visited,entities,blocked);}
            }
        }
        [CommandMethod("MRTBLOCKMERGE",CommandFlags.Modal|CommandFlags.UsePickSet)]
        public void MergeBlockLayers()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;try
            {
                ObjectId seed=ObjectId.Null;var pre=Preselected(doc);if(pre.Length==1)seed=pre[0];if(seed.IsNull){var pick=doc.Editor.GetEntity("\nChọn block mẫu cần dồn Layer: ");if(pick.Status!=PromptStatus.OK)return;seed=pick.ObjectId;}
                var counts=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);var warnings=new List<string>();string[] allLayers;
                using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction())
                {
                    var b=tr.GetObject(seed,OpenMode.ForRead) as BlockReference;if(b==null||b.IsDynamicBlock)throw new InvalidOperationException("Chức năng này chỉ sửa block thường; block động chưa được hỗ trợ.");
                    WalkLayers(tr,b.BlockTableRecord,counts,new HashSet<ObjectId>(),warnings,0);var layers=(LayerTable)tr.GetObject(doc.Database.LayerTableId,OpenMode.ForRead);allLayers=layers.Cast<ObjectId>().Select(id=>(LayerTableRecord)tr.GetObject(id,OpenMode.ForRead)).Where(l=>!l.IsDependent).Select(l=>l.Name).ToArray();
                }
                if(counts.Count==0)throw new InvalidOperationException("Không có Layer thành phần để chọn.");string[] selected;using(var form=new LayerMultiDialog("Tích Layer thành phần muốn dồn",counts.Select(x=>new LayerChoice{Name=x.Key,Info="("+x.Value+" thành phần)",Chosen=false}))){if(AcApp.ShowModalDialog(form)!=Win.DialogResult.OK)return;selected=form.SelectedNames;}if(selected.Length==0)return;
                string target;bool moveParent,moveOutside;using(var form=new BlockTargetDialog(allLayers)){if(AcApp.ShowModalDialog(form)!=Win.DialogResult.OK)return;target=form.TargetName;moveParent=form.IncludeParent;moveOutside=form.IncludeOutside;}
                if(String.IsNullOrWhiteSpace(target))throw new InvalidOperationException("Chưa chọn Layer đích.");SymbolUtilityServices.ValidateSymbolName(target,false);
                using(var locked=doc.LockDocument())
                using(var tr=doc.Database.TransactionManager.StartTransaction())
                {
                    var sample=(BlockReference)tr.GetObject(seed,OpenMode.ForRead);var all=new HashSet<ObjectId>();var entityIds=new List<ObjectId>();var blocked=new List<string>();GatherMovable(tr,sample.BlockTableRecord,new HashSet<string>(selected,StringComparer.OrdinalIgnoreCase),all,entityIds,blocked);
                    var outside=new List<ObjectId>();if(moveOutside){var keys=new HashSet<string>(selected,StringComparer.OrdinalIgnoreCase);var space=(BlockTableRecord)tr.GetObject(PickSpace(doc),OpenMode.ForRead);foreach(ObjectId id in space){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||id==seed||!keys.Contains(e.Layer))continue;var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);if(layer.IsLocked||layer.IsDependent)blocked.Add("Layer đối tượng ngoài block bị khóa/phụ thuộc: "+layer.Name);else outside.Add(id);}}
                    if(blocked.Count>0)throw new InvalidOperationException("Chưa sửa vì có thành phần cần xử lý riêng:\r\n"+String.Join("\r\n",blocked.Distinct().Take(12)));
                    var layers=(LayerTable)tr.GetObject(doc.Database.LayerTableId,OpenMode.ForRead);if(layers.Has(target)){var l=(LayerTableRecord)tr.GetObject(layers[target],OpenMode.ForRead);if(l.IsLocked||l.IsDependent)throw new InvalidOperationException("Layer đích bị khóa/phụ thuộc.");}
                    if(moveParent&&((LayerTableRecord)tr.GetObject(sample.LayerId,OpenMode.ForRead)).IsLocked)throw new InvalidOperationException("Layer của block mẫu đang khóa.");
                    int references=0;var blocks=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);foreach(ObjectId bid in blocks){var space=(BlockTableRecord)tr.GetObject(bid,OpenMode.ForRead);if(space.IsFromExternalReference||space.IsFromOverlayReference)continue;foreach(ObjectId id in space){var br=tr.GetObject(id,OpenMode.ForRead) as BlockReference;if(br!=null&&all.Contains(br.BlockTableRecord))references++;}}
                    string message="Sẽ đổi Layer của "+entityIds.Count+" thành phần trong "+all.Count+" định nghĩa block và "+outside.Count+" đối tượng ngoài block.\r\nÍt nhất "+references+" tham chiếu trực tiếp dùng các định nghĩa đó sẽ bị ảnh hưởng, kể cả ngoài lựa chọn. Block lồng có thể làm phạm vi lớn hơn.\r\nLayer đích: "+target+(moveParent?"; chuyển thêm block mẫu.":"; giữ Layer block mẫu.")+"\r\nThay Layer 0/ByLayer/ByBlock có thể làm đổi màu/nét. Không sửa attribute; không hỗ trợ block động/Xref.\r\n\r\nÁp dụng?";
                    if(Win.MessageBox.Show(message,"Xem ảnh hưởng trước khi dồn Layer",Win.MessageBoxButtons.YesNo,Win.MessageBoxIcon.Warning)!=Win.DialogResult.Yes)return;
                    ObjectId targetId;if(layers.Has(target))targetId=layers[target];else{layers.UpgradeOpen();var l=new LayerTableRecord{Name=target};targetId=layers.Add(l);tr.AddNewlyCreatedDBObject(l,true);}
                    foreach(var id in entityIds.Concat(outside).Distinct()){var e=(Entity)tr.GetObject(id,OpenMode.ForWrite);e.LayerId=targetId;}if(moveParent){sample.UpgradeOpen();sample.LayerId=targetId;}tr.Commit();
                    doc.Editor.Regen();doc.Editor.WriteMessage("\nMr.T: Đã đổi "+entityIds.Count+" thành phần; dùng UNDO để hoàn tác.\n");
                }
            }catch(System.Exception ex){Report(doc,ex);}
        }
    }
}
