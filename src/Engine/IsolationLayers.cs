using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
namespace MedGas2024.Engine
{
    // Only layer flags opened by this tool are recorded. Native AutoCAD owns object isolation.
    public static class IsolationLayers
    {
        const string Key="MRT_VIEW_OPEN_LAYERS_V1",Version="MrT.ViewLayers.1";
        static void BlockLayers(Transaction tr,ObjectId recordId,HashSet<ObjectId> layers,HashSet<ObjectId> visited,int depth)
        {
            if(depth>32||!visited.Add(recordId))return;var record=tr.GetObject(recordId,OpenMode.ForRead) as BlockTableRecord;
            if(record==null||record.IsFromExternalReference||record.IsFromOverlayReference)return;
            foreach(ObjectId child in record){var entity=tr.GetObject(child,OpenMode.ForRead) as Entity;if(entity==null)continue;layers.Add(entity.LayerId);var nested=entity as BlockReference;if(nested!=null)BlockLayers(tr,nested.BlockTableRecord,layers,visited,depth+1);}
        }
        static string[] Read(Database db,Transaction tr)
        {
            var nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForRead);if(!nod.Contains(Key))return new string[0];var record=(Xrecord)tr.GetObject(nod.GetAt(Key),OpenMode.ForRead);
            using(var data=record.Data){var values=data==null?new TypedValue[0]:data.AsArray();if(values.Length<1||Convert.ToString(values[0].Value)!=Version)throw new InvalidOperationException("Dữ liệu khôi phục Layer Isolate không hợp lệ.");return values.Skip(1).Select(v=>Convert.ToString(v.Value)).ToArray();}
        }
        static void Write(Database db,Transaction tr,List<string> entries)
        {
            var nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForWrite);Xrecord record=null;
            if(nod.Contains(Key)){record=(Xrecord)tr.GetObject(nod.GetAt(Key),OpenMode.ForWrite);if(entries.Count==0){nod.Remove(Key);record.Erase();return;}}
            if(entries.Count==0)return;if(record==null){record=new Xrecord();nod.SetAt(Key,record);tr.AddNewlyCreatedDBObject(record,true);}
            var values=new List<TypedValue>{new TypedValue(1000,Version)};values.AddRange(entries.Select(s=>new TypedValue(1000,s)));using(var data=new ResultBuffer(values.ToArray()))record.Data=data;
        }
        public static bool HasPending(Database db){using(var tr=db.TransactionManager.StartOpenCloseTransaction())return Read(db,tr).Length>0;}
        public static int Open(Database db,IEnumerable<ObjectId> ids,bool injectFailure=false)
        {
            using(var tr=db.TransactionManager.StartTransaction())
            {
                if(Read(db,tr).Length>0)throw new InvalidOperationException("Còn Layer cần khôi phục. Gõ MRTUNISO; nếu Layer hiện hành trước đó bị đóng băng, chuyển sang Layer khác rồi thử lại.");
                var layers=new HashSet<ObjectId>();var visited=new HashSet<ObjectId>();foreach(var id in ids){if(id.IsNull||!id.IsValid||id.IsErased||id.Database.UnmanagedObject!=db.UnmanagedObject)continue;var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)continue;var owner=tr.GetObject(e.OwnerId,OpenMode.ForRead) as BlockTableRecord;if(owner!=null&&owner.IsLayout){layers.Add(e.LayerId);var block=e as BlockReference;if(block!=null)BlockLayers(tr,block.BlockTableRecord,layers,visited,0);}}
                var entries=new List<string>();foreach(var id in layers){var layer=(LayerTableRecord)tr.GetObject(id,OpenMode.ForRead);if(layer.IsDependent)continue;int flags=(layer.IsOff?1:0)|(layer.IsFrozen?2:0);if(flags==0)continue;entries.Add(id.Handle+"|"+flags);layer.UpgradeOpen();if(layer.IsOff)layer.IsOff=false;if(layer.IsFrozen)layer.IsFrozen=false;}
                Write(db,tr,entries);if(injectFailure)throw new InvalidOperationException("Injected isolation layer rollback.");tr.Commit();return entries.Count;
            }
        }
        public static int Restore(Database db)
        {
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var saved=Read(db,tr);if(saved.Length==0)return 0;var pending=new List<string>();foreach(var value in saved)
                {
                    var parts=value.Split('|');long handle;int flags;if(parts.Length!=2||!Int64.TryParse(parts[0],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out handle)||!Int32.TryParse(parts[1],out flags)||flags<1||flags>3)throw new InvalidOperationException("Dữ liệu khôi phục Layer bị lỗi; chưa thay đổi Layer.");
                    ObjectId id;try{id=db.GetObjectId(false,new Handle(handle),0);}catch(Autodesk.AutoCAD.Runtime.Exception){continue;}if(id.IsNull||!id.IsValid||id.IsErased)continue;var layer=tr.GetObject(id,OpenMode.ForRead) as LayerTableRecord;if(layer==null||layer.IsDependent)continue;layer.UpgradeOpen();if((flags&1)!=0)layer.IsOff=true;
                    if((flags&2)!=0){if(id==db.Clayer){pending.Add(parts[0]+"|2");continue;}layer.IsFrozen=true;}
                }
                Write(db,tr,pending);tr.Commit();return pending.Count;
            }
        }
    }
}
