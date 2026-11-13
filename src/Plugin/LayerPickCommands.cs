using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;
namespace MedGas2024.Plugin
{
    public sealed partial class Commands
    {
        private static ObjectId[] ForeignLayerObjects(Autodesk.AutoCAD.ApplicationServices.Document doc,IEnumerable<ObjectId> seeds,ObjectId spaceId)
        {
            var layers=new HashSet<ObjectId>();var found=new List<ObjectId>();
            using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach(var id in seeds.Distinct())
                {
                    if(id.IsNull||!id.IsValid||id.IsErased)continue;var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||e.OwnerId!=spaceId)continue;
                    var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);if(!layer.Name.StartsWith("Mr.T",StringComparison.OrdinalIgnoreCase))layers.Add(e.LayerId);
                }
                var space=(BlockTableRecord)tr.GetObject(spaceId,OpenMode.ForRead);
                foreach(ObjectId id in space){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e!=null&&layers.Contains(e.LayerId))found.Add(id);}
            }
            return found.ToArray();
        }
        [CommandMethod("MRTSELLAYER",CommandFlags.Modal|CommandFlags.UsePickSet|CommandFlags.Redraw|CommandFlags.NoUndoMarker)]
        public void PickByLayers()
        {
            var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc==null)return;
            try
            {
                var seeds=Preselected(doc);
                if(seeds.Length==0){var selection=doc.Editor.GetSelection(new PromptSelectionOptions{MessageForAdding="\nChọn đối tượng mẫu; sẽ pick mọi đối tượng cùng Layer: "});if(selection.Status!=PromptStatus.OK)return;using(selection.Value)seeds=selection.Value.GetObjectIds();}
                var layers=new HashSet<ObjectId>();var found=new List<ObjectId>();int hidden=0;
                using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction())
                {
                    ObjectId spaceId=PickSpace(doc);
                    foreach(var id in seeds.Distinct()){if(id.IsNull||!id.IsValid||id.IsErased)continue;var entity=tr.GetObject(id,OpenMode.ForRead) as Entity;if(entity!=null&&entity.OwnerId==spaceId)layers.Add(entity.LayerId);}
                    if(layers.Count==0){doc.Editor.WriteMessage("\nMr.T: Không có đối tượng mẫu trực tiếp trong không gian hiện hành.\n");return;}
                    foreach(var layerId in layers){var layer=(LayerTableRecord)tr.GetObject(layerId,OpenMode.ForRead);if(layer.IsOff||layer.IsFrozen)hidden++;}
                    var space=(BlockTableRecord)tr.GetObject(spaceId,OpenMode.ForRead);foreach(ObjectId id in space){var entity=tr.GetObject(id,OpenMode.ForRead) as Entity;if(entity!=null&&layers.Contains(entity.LayerId))found.Add(id);}
                }
                doc.Editor.SetImpliedSelection(found.ToArray());
                doc.Editor.WriteMessage("\nMr.T: Đã đưa "+found.Count+" đối tượng trên "+layers.Count+" Layer vào lựa chọn trong không gian hiện hành. Không mở block/Xref; Layer tắt/đóng băng: "+hidden+".\n");
            }
            catch(System.Exception ex){Report(doc,ex);}
        }
    }
}
