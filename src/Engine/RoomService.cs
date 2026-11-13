using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MedGas2024.Core;

namespace MedGas2024.Engine
{
    public sealed class RoomChange
    {
        public ObjectId Id;public string RoomId,Current,After,Reason,Space,ExpectedTag;public RoomEntry Source;public bool Ready;
    }
    public sealed class RoomPlan
    {
        public bool NewMode;public readonly List<RoomChange> Rows=new List<RoomChange>();
    }
    public static class RoomService
    {
        const string Key="MRT_ROOM_V1";
        static string Literal(string s){return (s??"").Replace("\\","\\\\").Replace("{","\\{").Replace("}","\\}").Replace("%","\\U+0025").Replace("\r","").Replace("\n","\\P");}
        static string Read(Entity e,Transaction tr)
        {
            if(e.ExtensionDictionary.IsNull)return null;var d=(DBDictionary)tr.GetObject(e.ExtensionDictionary,OpenMode.ForRead);if(!d.Contains(Key))return null;var x=(Xrecord)tr.GetObject(d.GetAt(Key),OpenMode.ForRead);using(var data=x.Data)return String.Concat(data.AsArray().Select(v=>Convert.ToString(v.Value)));
        }
        static void Save(Entity e,Transaction tr,RoomTag tag)
        {
            if(e.ExtensionDictionary.IsNull)e.CreateExtensionDictionary();var d=(DBDictionary)tr.GetObject(e.ExtensionDictionary,OpenMode.ForWrite);Xrecord x;if(d.Contains(Key))x=(Xrecord)tr.GetObject(d.GetAt(Key),OpenMode.ForWrite);else{x=new Xrecord();d.SetAt(Key,x);tr.AddNewlyCreatedDBObject(x,true);}
            var json=tag.Serialize();var values=new List<TypedValue>();for(int i=0;i<json.Length;i+=200)values.Add(new TypedValue((int)DxfCode.Text,json.Substring(i,Math.Min(200,json.Length-i))));using(var data=new ResultBuffer(values.ToArray()))x.Data=data;
        }
        public static ObjectId Place(Database db,RoomEntry row,Point3d point,Vector3d normal,Vector3d direction,double height,bool newMode)
        {
            if(row==null||String.IsNullOrWhiteSpace(row.Id)||String.IsNullOrWhiteSpace(row.OldName))throw new ArgumentException("Dòng cần ID và tên cũ.");if(!Profile.Finite(height)||height<=0)throw new ArgumentException("Chiều cao chữ phải lớn hơn 0.");string text=RoomRules.Desired(row,newMode);if(String.IsNullOrWhiteSpace(text))throw new ArgumentException("Tên ở chế độ đang chọn bị trống.");
            using(var tr=db.TransactionManager.StartTransaction()){
                var layer=(LayerTableRecord)tr.GetObject(db.Clayer,OpenMode.ForRead);if(layer.IsLocked||layer.IsFrozen||layer.IsOff)throw new InvalidOperationException("Layer hiện hành khóa/tắt/đóng băng.");
                var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);var m=new MText();m.SetDatabaseDefaults(db);m.Location=point;m.Normal=normal;m.Direction=direction;m.Attachment=AttachmentPoint.MiddleCenter;m.TextHeight=height;m.Width=0;m.Contents=Literal(text);space.AppendEntity(m);tr.AddNewlyCreatedDBObject(m,true);
                var tag=new RoomTag{Id=RoomCatalog.Key(row.Id),Original=row.OldName,OldName=row.OldName,NewName=row.NewName,LastText=text,ShowingNew=newMode};Save(m,tr,tag);var id=m.ObjectId;tr.Commit();return id;
            }
        }
        public static RoomPlan Inspect(Database db,RoomCatalog catalog,bool newMode)
        {
            var plan=new RoomPlan{NewMode=newMode};using(var tr=db.TransactionManager.StartOpenCloseTransaction()){
                var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);foreach(ObjectId bid in blocks){var space=(BlockTableRecord)tr.GetObject(bid,OpenMode.ForRead);if(!space.IsLayout||space.IsFromExternalReference||space.IsFromOverlayReference)continue;
                    foreach(ObjectId id in space){var m=tr.GetObject(id,OpenMode.ForRead) as MText;if(m==null)continue;var json=Read(m,tr);if(json==null)continue;var c=new RoomChange{Id=id,Current=m.Text,Space=space.Name,ExpectedTag=json};plan.Rows.Add(c);
                        try{var tag=RoomTag.Parse(json);c.RoomId=tag.Id;var source=catalog==null?new RoomEntry{Id=tag.Id,OldName=tag.OldName,NewName=tag.NewName}:catalog.Find(tag.Id);c.Source=source;string reason;
                            if(!RoomRules.CanUpdate(tag,c.Current,source,newMode,out reason)){c.Reason=reason;continue;}if(((LayerTableRecord)tr.GetObject(m.LayerId,OpenMode.ForRead)).IsLocked){c.Reason="Layer đang khóa.";continue;}c.After=RoomRules.Desired(source,newMode);c.Ready=true;c.Reason=c.Current==c.After?"Đã đúng; sẽ cập nhật dữ liệu nếu cần.":"Sẵn sàng";
                        }catch(Exception ex){c.Reason="Dữ liệu phòng không hợp lệ: "+ex.Message;}
                    }
                }
            }return plan;
        }
        public static int Apply(Database db,RoomPlan plan)
        {
            if(plan==null)throw new ArgumentNullException("plan");int count=0;using(var tr=db.TransactionManager.StartTransaction()){
                foreach(var c in plan.Rows.Where(x=>x.Ready)){
                    if(c.Id.IsErased||!c.Id.IsValid)throw new InvalidOperationException("Đối tượng "+c.Id.Handle+" đã xóa; cần xem trước lại.");var m=(MText)tr.GetObject(c.Id,OpenMode.ForWrite);if(Read(m,tr)!=c.ExpectedTag||m.Text!=c.Current||((LayerTableRecord)tr.GetObject(m.LayerId,OpenMode.ForRead)).IsLocked)throw new InvalidOperationException("Phòng "+c.RoomId+" đã đổi sau xem trước; cần xem trước lại.");
                    var tag=RoomTag.Parse(c.ExpectedTag);tag.OldName=c.Source.OldName;tag.NewName=c.Source.NewName;tag.ShowingNew=plan.NewMode;tag.LastText=c.After;m.Contents=Literal(c.After);Save(m,tr,tag);count++;
                }tr.Commit();return count;
            }
        }
    }
}
