using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using MedGas2024.Core;

namespace MedGas2024.Engine
{
    public sealed class RoomHit:RoomNameMatch
    {
        public ObjectId Id;
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
            return m==null?((DBText)text).TextString:m.Text;
        }
        static bool WritableRoomText(Entity entity,string desired)
        {
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
                foreach(ObjectId id in space)
                {
                    var e=tr.GetObject(id,OpenMode.ForRead) as Entity;
                    if(!(e is MText)&&(e==null||e.GetType()!=typeof(DBText)))continue;
                    var existing=Read(e,tr);
                    if(existing==null&&layer!=null&&!String.Equals(e.Layer,layer,StringComparison.OrdinalIgnoreCase))continue;
                    var hit=new RoomHit{Id=id,SingleLine=e is DBText,Text=RoomText(e),Layer=e.Layer};
                    if(existing!=null)
                    {
                        try
                        {
                            hit.Tag=RoomTag.Parse(existing).Id;
                            hit.Status="Đã gắn ID";
                        }
                        catch(System.Runtime.Serialization.SerializationException){hit.Tag="";hit.Status="Dữ liệu ID không đọc được";}
                        catch(ArgumentException){hit.Tag="";hit.Status="Dữ liệu ID không hợp lệ";}
                    }
                    else
                    {
                        var l=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);
                        if(l.IsLocked||l.IsFrozen||l.IsOff||l.IsDependent||!e.Visible)hit.Status="Chữ ẩn hoặc layer không sửa được";
                        else if(!WritableRoomText(e,hit.Text))hit.Status="Chữ có field, định dạng riêng hoặc ký tự đặc biệt";
                    }
                    result.Add(hit);
                }
            }
            return result;
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
                    if(e.OwnerId!=spaceId||e.Layer!=hit.Layer||!e.Visible||from.IsLocked||from.IsOff||from.IsFrozen||
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
