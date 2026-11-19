using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using MedGas2024.Core;
using Autodesk.AutoCAD.Geometry;

namespace MedGas2024.Engine
{
    public sealed class RoomHit:RoomNameMatch
    {
        public ObjectId Id,AttributeOwner;
        public string Path,StoredLayer;
        public bool OnSourceLayer,Shared;
        public Matrix3d ToWorld=Matrix3d.Identity;
        public string Address {get{return String.IsNullOrEmpty(Path)?Id.Handle.ToString():Path+"/"+Id.Handle;}}
    }

    public static partial class RoomService
    {
        public static string[] Layers(Database db)
        {
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var table=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                return table.Cast<ObjectId>().Select(id=>(LayerTableRecord)tr.GetObject(id,OpenMode.ForRead))
                    .Where(l=>!l.IsDependent&&!l.IsLocked&&!l.IsFrozen&&!l.IsOff)
                    .Select(l=>l.Name).OrderBy(n=>n,StringComparer.CurrentCultureIgnoreCase).ToArray();
            }
        }

        public static string[] Styles(Database db)
        {
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var table=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForRead);
                return table.Cast<ObjectId>().Select(id=>(TextStyleTableRecord)tr.GetObject(id,OpenMode.ForRead))
                    .Where(s=>!s.IsDependent&&!s.IsShapeFile&&!s.IsVertical).Select(s=>s.Name)
                    .OrderBy(n=>n,StringComparer.CurrentCultureIgnoreCase).ToArray();
            }
        }

        static Tuple<ObjectId,ObjectId> ResolveRoomTarget(Database db,Transaction tr,string layer,string style)
        {
            var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
            if(!layers.Has(layer)&&layer==RoomCatalog.DefaultLayer)
            {
                layers.UpgradeOpen();
                var created=new LayerTableRecord{Name=layer};
                layers.Add(created);tr.AddNewlyCreatedDBObject(created,true);
            }
            if(!layers.Has(layer))throw new InvalidOperationException("Layer đích đã đổi. Mở lại công cụ.");
            var target=(LayerTableRecord)tr.GetObject(layers[layer],OpenMode.ForRead);
            if(target.IsDependent||target.IsLocked||target.IsFrozen||target.IsOff)
                throw new InvalidOperationException("Layer đích bị khóa, tắt hoặc đóng băng.");
            var styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForRead);
            if(!styles.Has(style)&&style==RoomCatalog.DefaultStyle)
            {
                styles.UpgradeOpen();
                var created=new TextStyleTableRecord{Name=style,FileName="arial.ttf"};
                styles.Add(created);tr.AddNewlyCreatedDBObject(created,true);
            }
            if(!styles.Has(style))throw new InvalidOperationException("Kiểu chữ đã đổi. Mở lại công cụ.");
            var record=(TextStyleTableRecord)tr.GetObject(styles[style],OpenMode.ForRead);
            if(record.IsDependent||record.IsShapeFile||record.IsVertical)
                throw new InvalidOperationException("Kiểu chữ đã chọn không phù hợp với tên phòng.");
            return Tuple.Create(target.ObjectId,record.ObjectId);
        }

        static string RoomText(Entity text)
        {
            var m=text as MText;
            var attr=text as AttributeReference;
            if(attr!=null&&attr.IsMTextAttribute){using(var value=attr.MTextAttribute)return value.Text;}
            return m==null?((DBText)text).TextString:m.Text;
        }
        static bool WritableRoomText(Entity entity,string desired)
        {
            var attr=entity as AttributeReference;
            if(attr!=null&&attr.IsMTextAttribute)return false;
            if(entity.HasFields||String.IsNullOrWhiteSpace(desired))return false;
            var m=entity as MText;
            if(m!=null)return m.Contents==m.Text||m.Contents==Literal(m.Text);
            return RoomNameMatcher.SingleLineSafe(desired);
        }
        static void SetRoomText(Database db,Entity entity,string desired)
        {
            var m=entity as MText;
            if(m!=null){m.Contents=Literal(desired);return;}
            var t=(DBText)entity;
            t.TextString=desired;
            t.AdjustAlignment(db);
        }

        public static List<RoomHit> ScanNames(Database db,ObjectId spaceId,string layer)
        {
            var result=new List<RoomHit>();
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var space=(BlockTableRecord)tr.GetObject(spaceId,OpenMode.ForRead);
                if(!space.IsLayout)throw new InvalidOperationException("Hãy mở Model hoặc Layout cần gắn tên.");
                ScanRoomEntities(tr,space,layer,Matrix3d.Identity,null,"",new HashSet<ObjectId>(),false,true,result);
            }
            return result;
        }

        static bool Unavailable(Transaction tr,Entity e)
        {
            var l=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);
            return !e.Visible||l.IsLocked||l.IsFrozen||l.IsOff||l.IsDependent;
        }
        static bool LayerMatches(string source,string name)
        {
            return source==null||String.Equals(source,name,StringComparison.OrdinalIgnoreCase);
        }
        static void ScanRoomEntities(Transaction tr,BlockTableRecord space,string source,Matrix3d transform,
            string inherited,string path,HashSet<ObjectId> ancestry,bool parentMatched,bool parentAvailable,List<RoomHit> result)
        {
            if(!ancestry.Add(space.ObjectId))return;
            try
            {
                foreach(ObjectId id in space)
                {
                    var entity=tr.GetObject(id,OpenMode.ForRead) as Entity;
                    if(entity==null)continue;
                    var br=entity as BlockReference;
                    if(br==null)
                    {
                        var def=entity as AttributeDefinition;
                        if(def!=null&&(!def.Constant||def.Invisible||path.Length==0))continue;
                        if(entity is MText||entity.GetType()==typeof(DBText)||def!=null)
                            AddRoomHit(tr,entity,source,transform,inherited,path,parentMatched,parentAvailable,path.Length>0,ObjectId.Null,result);
                        continue;
                    }
                    if(br is MInsertBlock)continue;
                    var block=(BlockTableRecord)tr.GetObject(br.BlockTableRecord,OpenMode.ForRead);
                    if(block.IsFromExternalReference||block.IsFromOverlayReference)continue;
                    string effective=br.Layer=="0"&&inherited!=null?inherited:br.Layer;
                    bool matched=parentMatched||LayerMatches(source,effective)||LayerMatches(source,br.Layer);
                    bool available=parentAvailable&&!Unavailable(tr,br);
                    string next=path.Length==0?br.Handle.ToString():path+"/"+br.Handle;
                    foreach(ObjectId aid in br.AttributeCollection)
                    {
                        var attr=tr.GetObject(aid,OpenMode.ForRead) as AttributeReference;
                        if(attr!=null&&!attr.Invisible)
                            AddRoomHit(tr,attr,source,transform,effective,next,matched,available,path.Length>0,br.ObjectId,result);
                    }
                    ScanRoomEntities(tr,block,source,transform*br.BlockTransform,effective,next,ancestry,matched,available,result);
                }
            }
            finally{ancestry.Remove(space.ObjectId);}
        }

        static void AddRoomHit(Transaction tr,Entity e,string source,Matrix3d transform,string inherited,string path,
            bool parentMatched,bool available,bool shared,ObjectId attrOwner,List<RoomHit> result)
        {
            string effective=e.Layer=="0"&&inherited!=null?inherited:e.Layer;
            bool onSource=parentMatched||LayerMatches(source,e.Layer)||LayerMatches(source,effective);
            string existing=shared?null:Read(e,tr);
            if(!onSource&&existing==null)return;
            var hit=new RoomHit{Id=e.ObjectId,Path=path,ToWorld=transform,Shared=shared,AttributeOwner=attrOwner,
                Layer=effective,StoredLayer=e.Layer,OnSourceLayer=onSource,Text=RoomText(e),SingleLine=e is DBText};
            if(existing!=null)
            {
                try{hit.Tag=RoomTag.Parse(existing).Id;hit.Status="Đã gắn ID";}
                catch(System.Runtime.Serialization.SerializationException){hit.Tag="";hit.Status="Dữ liệu ID không đọc được";}
                catch(ArgumentException){hit.Tag="";hit.Status="Dữ liệu ID không hợp lệ";}
            }
            else if(!available||Unavailable(tr,e))hit.Status="Chữ hoặc block bị ẩn, khóa hay đóng băng";
            else if(shared)hit.Status="Chữ dùng chung trong block — chọn vị trí để gắn nhãn riêng";
            else if(!WritableRoomText(e,hit.Text))hit.Status="Chữ có field, định dạng riêng hoặc ký tự đặc biệt";
            result.Add(hit);
        }

        static bool EditableOwner(Transaction tr,Entity entity,ObjectId space,ObjectId attributeOwner)
        {
            if(attributeOwner.IsNull)return entity.OwnerId==space;
            var br=tr.GetObject(attributeOwner,OpenMode.ForRead) as BlockReference;
            return br!=null&&br.OwnerId==space&&entity.OwnerId==attributeOwner&&!Unavailable(tr,br);
        }

        public static int Adopt(Database db,ObjectId spaceId,IEnumerable<RoomHit> selected,bool newMode,string layer,string style)
        {
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var target=ResolveRoomTarget(db,tr,layer,style);
                int count=0;
                foreach(var hit in selected)
                {
                    var e=(Entity)tr.GetObject(hit.Id,OpenMode.ForWrite);
                    var from=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);
                    var value=RoomRules.Desired(hit.Entry,newMode);
                    if(hit.Shared||!EditableOwner(tr,e,spaceId,hit.AttributeOwner)||e.Layer!=hit.StoredLayer||!e.Visible||from.IsLocked||from.IsOff||from.IsFrozen||
                        Read(e,tr)!=null||RoomText(e)!=hit.Text||!WritableRoomText(e,value))
                        throw new InvalidOperationException("Chữ "+hit.Id.Handle+" đã thay đổi hoặc không thể ghi. Hãy rà soát lại.");
                    e.LayerId=target.Item1;
                    var m=e as MText;
                    if(m!=null)
                    {
                        double height=m.TextHeight;
                        m.TextStyleId=target.Item2;
                        m.TextHeight=height;
                    }
                    else
                    {
                        var t=(DBText)e;
                        double height=t.Height;
                        t.TextStyleId=target.Item2;
                        t.Height=height;
                    }
                    SetRoomText(db,e,value);
                    Save(e,tr,new RoomTag{Id=hit.Entry.Id,Original=hit.Entry.OldName,OldName=hit.Entry.OldName,
                        NewName=hit.Entry.NewName,LastText=value,ShowingNew=newMode});
                    count++;
                }
                tr.Commit();return count;
            }
        }
    }
}
