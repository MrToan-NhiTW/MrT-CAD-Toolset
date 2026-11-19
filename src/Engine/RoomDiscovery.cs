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
        public ObjectId Label;
        public Matrix3d ToWorld=Matrix3d.Identity;
        public string Address {get{return String.IsNullOrEmpty(Path)?Id.Handle.ToString():Path+"/"+Id.Handle;}}
    }

    public sealed class RoomApplyResult
    {
        public int Adopted,Labels;public string HiddenLayer;
        public int Total{get{return Adopted+Labels;}}
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
            if(!layers.Has(layer)&&(layer==RoomCatalog.DefaultLayer||layer==RoomCatalog.NewLayer))
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
                Layer=effective,StoredLayer=e.Layer,OnSourceLayer=onSource,Text=RoomText(e),SingleLine=e is DBText,NeedsLabel=shared};
            hit.Place=hit.Address;
            if(existing!=null)
            {
                try{var tag=RoomTag.Parse(existing);hit.Tag=tag.Id;hit.Linked=tag.Source;hit.Blocked="Đã gắn ID";}
                catch(System.Runtime.Serialization.SerializationException){hit.Tag="";hit.Blocked="Dữ liệu ID không đọc được";}
                catch(ArgumentException){hit.Tag="";hit.Blocked="Dữ liệu ID không hợp lệ";}
            }
            else if(!available||Unavailable(tr,e))hit.Blocked="Chữ hoặc block bị ẩn, khóa hay đóng băng";
            else if(!shared&&!WritableRoomText(e,hit.Text))hit.Blocked="Chữ có field, định dạng riêng hoặc ký tự đặc biệt";
            hit.Status=hit.Blocked;
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
            return ApplyMatches(db,spaceId,selected,newMode,layer,style,false,null).Total;
        }

        // Applies reviewed matches in one transaction (one UNDO step): free text and
        // instance attributes are adopted in place; text inside a shared block gets a
        // separate MTEXT label at that instance's position. Optionally turns the
        // source layer off afterwards.
        public static RoomApplyResult ApplyMatches(Database db,ObjectId spaceId,IEnumerable<RoomHit> selected,bool newMode,
            string layer,string style,bool mask,string hideLayer)
        {
            var result=new RoomApplyResult();
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var target=ResolveRoomTarget(db,tr,layer,style);
                foreach(var hit in selected)
                {
                    if(hit.NeedsLabel){CreateLabel(db,tr,spaceId,hit,newMode,target,mask);result.Labels++;}
                    else{AdoptOne(db,tr,spaceId,hit,newMode,target);result.Adopted++;}
                }
                if(!String.IsNullOrEmpty(hideLayer))
                {
                    var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                    if(!layers.Has(hideLayer))throw new InvalidOperationException("Không tìm thấy layer "+hideLayer+".");
                    var record=(LayerTableRecord)tr.GetObject(layers[hideLayer],OpenMode.ForWrite);
                    if(!record.IsOff){record.IsOff=true;result.HiddenLayer=hideLayer;}
                }
                tr.Commit();
            }
            return result;
        }

        static void AdoptOne(Database db,Transaction tr,ObjectId spaceId,RoomHit hit,bool newMode,Tuple<ObjectId,ObjectId> target)
        {
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
                }
        }

        // Recomputes the instance transform from the INSERT handles in the address, so a
        // label is never placed from a stale preview (moved, erased or re-nested insert).
        static Matrix3d PathTransform(Database db,Transaction tr,ObjectId spaceId,RoomHit hit,Entity e)
        {
            var transform=Matrix3d.Identity;var owner=spaceId;var last=ObjectId.Null;
            var parts=(hit.Path??"").Split(new[]{'/'},StringSplitOptions.RemoveEmptyEntries);
            string changed="Block chứa chữ "+hit.Text+" đã thay đổi. Hãy rà soát lại.";
            for(int i=0;i<parts.Length;i++)
            {
                ObjectId id;long value;
                if(!Int64.TryParse(parts[i],System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out value)||
                    !db.TryGetObjectId(new Handle(value),out id)||id.IsErased)throw new InvalidOperationException(changed);
                var br=tr.GetObject(id,OpenMode.ForRead) as BlockReference;
                if(br==null||br.OwnerId!=owner)throw new InvalidOperationException(changed);
                // An attribute lives in the space that holds its INSERT, so the last
                // INSERT of an attribute address does not transform it.
                if(hit.AttributeOwner.IsNull||i<parts.Length-1)transform=transform*br.BlockTransform;
                owner=br.BlockTableRecord;last=br.ObjectId;
            }
            bool inside=hit.AttributeOwner.IsNull?e.OwnerId==owner:(e.OwnerId==hit.AttributeOwner&&last==hit.AttributeOwner);
            if(!inside)throw new InvalidOperationException("Chữ "+hit.Text+" không còn trong block đã rà soát. Hãy rà soát lại.");
            return transform;
        }

        static void CreateLabel(Database db,Transaction tr,ObjectId spaceId,RoomHit hit,bool newMode,Tuple<ObjectId,ObjectId> target,bool mask)
        {
            var value=RoomRules.Desired(hit.Entry,newMode);
            if(String.IsNullOrWhiteSpace(value))throw new InvalidOperationException("Tên cần hiển thị của "+hit.Entry.Id+" đang trống.");
            if(hit.Id.IsErased)throw new InvalidOperationException("Chữ "+hit.Text+" đã bị xóa. Hãy rà soát lại.");
            var e=(Entity)tr.GetObject(hit.Id,OpenMode.ForRead);
            var transform=PathTransform(db,tr,spaceId,hit,e);
            if(RoomText(e)!=hit.Text||!transform.IsEqualTo(hit.ToWorld,new Tolerance(1e-9,1e-7)))
                throw new InvalidOperationException("Chữ "+hit.Text+" đã thay đổi sau xem trước. Hãy rà soát lại.");
            Point3d center;Vector3d direction,normal;double height;
            TextFrame(e,out center,out direction,out normal,out height);
            var up=normal.CrossProduct(direction);
            var worldCenter=center.TransformBy(transform);
            var worldDirection=direction.TransformBy(transform);
            var worldUp=up.TransformBy(transform);
            if(worldDirection.Length<1e-9||worldUp.Length<1e-9)throw new InvalidOperationException("Block chứa chữ "+hit.Text+" có tỷ lệ bằng 0.");
            var worldNormal=worldDirection.CrossProduct(worldUp);
            if(worldNormal.Length<1e-12)worldNormal=Vector3d.ZAxis;
            worldNormal=worldNormal.GetNormal();
            // A mirrored insert flips the plane; keep the label readable from above.
            if(worldNormal.Z<0)worldNormal=worldNormal.Negate();
            worldDirection=(worldDirection-worldNormal*worldDirection.DotProduct(worldNormal)).GetNormal();
            double worldHeight=height*worldUp.Length;
            if(!Profile.Finite(worldHeight)||worldHeight<=0)throw new InvalidOperationException("Không xác định được độ cao chữ "+hit.Text+".");
            var space=(BlockTableRecord)tr.GetObject(spaceId,OpenMode.ForWrite);
            var m=new MText();m.SetDatabaseDefaults(db);m.LayerId=target.Item1;m.TextStyleId=target.Item2;
            m.Normal=worldNormal;m.Location=worldCenter;m.Direction=worldDirection;m.Attachment=AttachmentPoint.MiddleCenter;
            m.TextHeight=worldHeight;m.Width=0;m.Contents=Literal(value);
            if(mask){m.BackgroundFill=true;m.UseBackgroundColor=true;m.BackgroundScaleFactor=1.15;}
            space.AppendEntity(m);tr.AddNewlyCreatedDBObject(m,true);
            Save(m,tr,new RoomTag{Id=hit.Entry.Id,Original=hit.Entry.OldName,OldName=hit.Entry.OldName,NewName=hit.Entry.NewName,
                LastText=value,ShowingNew=newMode,Source=hit.Address});
            hit.Label=m.ObjectId;
        }

        // Centre, reading direction, plane and character height of a text in the
        // coordinates of the block definition that holds it.
        static void TextFrame(Entity e,out Point3d center,out Vector3d direction,out Vector3d normal,out double height)
        {
            var m=e as MText;
            if(m!=null){normal=m.Normal;direction=m.Direction;height=m.TextHeight;}
            else
            {
                var t=(DBText)e;normal=t.Normal;height=t.Height;
                direction=new Vector3d(Math.Cos(t.Rotation),Math.Sin(t.Rotation),0).TransformBy(Matrix3d.PlaneToWorld(normal));
            }
            if(normal.Length<1e-12)normal=Vector3d.ZAxis;
            normal=normal.GetNormal();direction=direction.Length<1e-12?Vector3d.XAxis:direction.GetNormal();
            var extents=e.GeometricExtents;
            center=extents.MinPoint+(extents.MaxPoint-extents.MinPoint)*0.5;
        }
    }
}
