using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MedGas2024.Core;
namespace MedGas2024.Engine
{
    public sealed class DirectionLabel
    {
        public Point3d Center;public double Rotation;
    }
    public sealed class DirectionItem
    {
        public ObjectId Id,OriginalType,Style;public string State,Text;public double Height,CenterX,CenterY;
        public bool PlainOnly;public readonly List<DirectionLabel> Labels=new List<DirectionLabel>();
    }
    public sealed class DirectionPlan
    {
        public string OptionsSignature;public readonly List<DirectionItem> Items=new List<DirectionItem>();public readonly List<string> Skipped=new List<string>();public int ArcSegments,ShortSegments;
        public string Summary(DirectionOptions options){return (options.Restore?"KHÔI PHỤC NÉT GỐC":"SỬA HƯỚNG CHỮ: ngang trái → phải; dọc trên → dưới")+"\r\nTuyến hợp lệ: "+Items.Count+"; nhãn mới: "+Items.Sum(i=>i.Labels.Count)+"; bỏ qua: "+Skipped.Count+".\r\nTuyến không có chữ (chỉ tắt generation): "+Items.Count(i=>i.PlainOnly)+".\r\nĐoạn cong không đặt nhãn: "+ArcSegments+"; đoạn ngắn thiếu chỗ: "+ShortSegments+".\r\nLinetype generation = Disabled; PLINEGEN=0 cho polyline vẽ tiếp.\r\nGiữ nguyên polyline/vertex/bulge/width/Layer/màu/Scale và định nghĩa nét dùng chung.\r\n"+(options.Restore?"Xóa đúng nhãn liên kết của các tuyến chọn, trả nét đối tượng về giá trị trước khi sửa.":"Nét đối tượng đổi sang Continuous, nhãn MText có nền che nét được liên kết với tuyến.\r\nĐây là nhãn theo đơn vị Model; kiểm tra kích thước/Plot. Sau STRETCH/JOIN/COPY chạy lại tool để cập nhật.")+"\r\n"+String.Join("\r\n",Skipped.Take(12));}
    }
    public static class DirectionService
    {
        private const string RouteKey="MRT_DIRECTION_ROUTE_V1",LabelKey="MRT_DIRECTION_LABEL_V1",Mark="MrT.Direction:1";
        private sealed class Link{public ObjectId Original;public readonly List<ObjectId> Labels=new List<ObjectId>();}
        private sealed class Source{public ObjectId Original,Effective,Style;public string Text,Fingerprint;public double Height,Period;}
        private sealed class Piece{public Point3d A,B;}
        public static bool GenerationOn(Entity e){var p=e as Polyline;if(p!=null)return p.Plinegen;var old=e as Polyline2d;return old!=null&&old.LinetypeGenerationOn;}
        public static void DisableGeneration(Entity e){var p=e as Polyline;if(p!=null)p.Plinegen=false;var old=e as Polyline2d;if(old!=null)old.LinetypeGenerationOn=false;}
        private static TypedValue[] Data(DBObject obj,Transaction tr,string key)
        {
            if(obj.ExtensionDictionary.IsNull)return null;var d=(DBDictionary)tr.GetObject(obj.ExtensionDictionary,OpenMode.ForRead);if(!d.Contains(key))return null;var x=tr.GetObject(d.GetAt(key),OpenMode.ForRead) as Xrecord;if(x==null)return null;using(var r=x.Data){if(r==null)return null;var values=r.AsArray();return values.Length>0&&Convert.ToString(values[0].Value)==Mark?values:null;}
        }
        private static Link ReadLink(Entity e,Transaction tr)
        {
            var data=Data(e,tr,RouteKey);if(data==null)return null;var link=new Link();foreach(var v in data.Skip(1))if(v.Value is ObjectId){if(v.TypeCode==340)link.Original=(ObjectId)v.Value;else if(v.TypeCode==330)link.Labels.Add((ObjectId)v.Value);}return link;
        }
        private static bool OwnedLabel(ObjectId id,ObjectId owner,Database db,Transaction tr)
        {
            if(id.IsNull||!id.IsValid||id.IsErased||id.Database.UnmanagedObject!=db.UnmanagedObject)return false;var label=tr.GetObject(id,OpenMode.ForRead) as MText;if(label==null)return false;var data=Data(label,tr,LabelKey);return data!=null&&data.Any(v=>v.TypeCode==330&&v.Value is ObjectId&&(ObjectId)v.Value==owner);
        }
        public static bool HasLinked(Entity e,Transaction tr){return ReadLink(e,tr)!=null;}
        public static ObjectId NativeSource(Entity e,Database db,Transaction tr){var link=ReadLink(e,tr);return link!=null&&e.LinetypeId==db.ContinuousLinetype?link.Original:e.LinetypeId;}
        public static string LinkedState(Entity e,Database db,Transaction tr)
        {
            var link=ReadLink(e,tr);if(link==null)return "";var values=new List<string>{link.Original.Handle.ToString()};foreach(var id in link.Labels)if(OwnedLabel(id,e.ObjectId,db,tr)){var label=(MText)tr.GetObject(id,OpenMode.ForRead);var layer=(LayerTableRecord)tr.GetObject(label.LayerId,OpenMode.ForRead);values.Add(id.Handle+":"+label.LayerId.Handle+":"+layer.IsLocked);if(layer.IsLocked)throw new InvalidOperationException("Nhãn liên kết nằm trên Layer khóa.");}return Profile.Hash(String.Join("|",values));
        }
        public static void RemoveForNative(Entity e,Database db,Transaction tr)
        {
            var link=ReadLink(e,tr);if(link==null)return;LinkedState(e,db,tr);foreach(var id in link.Labels)if(OwnedLabel(id,e.ObjectId,db,tr))tr.GetObject(id,OpenMode.ForWrite).Erase();RemoveLink(e,tr);
        }
        private static void WriteData(DBObject obj,Transaction tr,string key,IEnumerable<TypedValue> values)
        {
            if(obj.ExtensionDictionary.IsNull)obj.CreateExtensionDictionary();var d=(DBDictionary)tr.GetObject(obj.ExtensionDictionary,OpenMode.ForWrite);Xrecord x;if(d.Contains(key))x=(Xrecord)tr.GetObject(d.GetAt(key),OpenMode.ForWrite);else{x=new Xrecord();d.SetAt(key,x);tr.AddNewlyCreatedDBObject(x,true);}using(var r=new ResultBuffer(values.ToArray()))x.Data=r;
        }
        private static void RemoveLink(Entity e,Transaction tr)
        {
            if(e.ExtensionDictionary.IsNull)return;var d=(DBDictionary)tr.GetObject(e.ExtensionDictionary,OpenMode.ForRead);if(d.Contains(RouteKey)){d.UpgradeOpen();var id=d.GetAt(RouteKey);d.Remove(RouteKey);tr.GetObject(id,OpenMode.ForWrite).Erase();}
        }
        private static Source ReadSource(Database db,Entity e,Transaction tr)
        {
            var link=ReadLink(e,tr);ObjectId original=e.LinetypeId;if(link!=null&&e.LinetypeId==db.ContinuousLinetype)original=link.Original;
            if(original.IsNull||!original.IsValid||original.IsErased||original.Database.UnmanagedObject!=db.UnmanagedObject)throw new InvalidOperationException("Không còn tham chiếu nét gốc.");
            ObjectId type=original==db.ByLayerLinetype?((LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead)).LinetypeObjectId:original;
            var lt=(LinetypeTableRecord)tr.GetObject(type,OpenMode.ForRead);var source=new Source{Original=original,Effective=type,Period=lt.PatternLength};int texts=0;var sig=new StringBuilder(type.Handle+"|"+lt.NumDashes+"|"+lt.PatternLength);
            for(int i=0;i<lt.NumDashes;i++)
            {
                var style=lt.ShapeStyleAt(i);sig.Append('|').Append(Profile.N(lt.DashLengthAt(i)));if(style.IsNull)continue;if(lt.ShapeNumberAt(i)!=0)throw new InvalidOperationException("Nét có hình SHX; chưa chuyển sang nhãn chữ.");
                string text;try{text=lt.TextAt(i);}catch(Autodesk.AutoCAD.Runtime.Exception ex){if(ex.ErrorStatus!=Autodesk.AutoCAD.Runtime.ErrorStatus.NotApplicable)throw;continue;}if(String.IsNullOrEmpty(text))continue;
                var st=(TextStyleTableRecord)tr.GetObject(style,OpenMode.ForRead);if(st.IsVertical)throw new InvalidOperationException("Style chữ xếp dọc chưa hỗ trợ.");texts++;source.Text=text;source.Style=style;source.Height=lt.ShapeScaleAt(i)*(st.TextSize>0?st.TextSize:1);sig.Append('|').Append(text).Append('|').Append(Profile.N(source.Height)).Append('|').Append(st.FileName).Append('|').Append(st.Font.TypeFace).Append('|') .Append(st.Font.Bold).Append(st.Font.Italic).Append(st.TextSize).Append('|').Append(st.XScale).Append('|').Append(st.ObliquingAngle);
            }
            if(texts>1)throw new InvalidOperationException("Nét chứa nhiều cụm chữ; chưa áp dụng.");if(texts==0)source.Text=null;if(source.Text!=null&&(!Profile.Finite(source.Height)||source.Height<=0||!Profile.Finite(source.Period)||source.Period<=0))throw new InvalidOperationException("Kích thước nét gốc không hợp lệ.");source.Fingerprint=Profile.Hash(sig.ToString());return source;
        }
        private static List<Piece> Pieces(Entity e,Transaction tr,DirectionPlan plan)
        {
            var result=new List<Piece>();var line=e as Line;if(line!=null){if(Math.Abs(line.StartPoint.Z-line.EndPoint.Z)>1e-7)throw new InvalidOperationException("Đường 3D nghiêng mặt phẳng XY.");result.Add(new Piece{A=line.StartPoint,B=line.EndPoint});return result;}
            var p=e as Polyline;if(p!=null){if(Math.Abs(Math.Abs(p.Normal.Z)-1)>1e-8)throw new InvalidOperationException("Polyline không nằm trong mặt phẳng XY.");int n=p.Closed?p.NumberOfVertices:p.NumberOfVertices-1;for(int i=0;i<n;i++){if(Math.Abs(p.GetBulgeAt(i))>1e-12){plan.ArcSegments++;continue;}result.Add(new Piece{A=p.GetPoint3dAt(i),B=p.GetPoint3dAt((i+1)%p.NumberOfVertices)});}return result;}
            var old=e as Polyline2d;if(old!=null){if(Math.Abs(Math.Abs(old.Normal.Z)-1)>1e-8||old.PolyType!=Poly2dType.SimplePoly)throw new InvalidOperationException("Polyline 2D fit/nghiêng chưa hỗ trợ.");var vertices=new List<Vertex2d>();foreach(ObjectId id in old)vertices.Add((Vertex2d)tr.GetObject(id,OpenMode.ForRead));int n=old.Closed?vertices.Count:vertices.Count-1;for(int i=0;i<n;i++){if(Math.Abs(vertices[i].Bulge)>1e-12){plan.ArcSegments++;continue;}result.Add(new Piece{A=old.GetPointAtParameter(i),B=old.GetPointAtParameter((i+1)%vertices.Count)});}return result;}throw new InvalidOperationException("Chỉ nhận LINE/polyline 2D trực tiếp.");
        }
        private static string State(Database db,Entity e,Transaction tr,Source source)
        {
            var s=new StringBuilder(Profile.N(db.Ltscale)+"|"+e.OwnerId.Handle+"|"+e.LayerId.Handle+"|"+e.LinetypeId.Handle+"|"+e.LinetypeScale+"|"+e.Color.ColorMethod+"|"+e.Color.ColorIndex+"|"+e.Color.Red+","+e.Color.Green+","+e.Color.Blue+"|"+GenerationOn(e)+"|"+source.Fingerprint+"|"+ScaleService.WidthState(e,tr));
            var dummy=new DirectionPlan();foreach(var piece in Pieces(e,tr,dummy))s.Append('|').Append(piece.A).Append('|').Append(piece.B);var p=e as Polyline;if(p!=null)s.Append(p.Closed).Append(p.Normal);if(p!=null)for(int i=0;i<p.NumberOfVertices;i++)s.Append('|').Append(p.GetPoint3dAt(i)).Append('|').Append(p.GetBulgeAt(i));var old=e as Polyline2d;if(old!=null)foreach(ObjectId id in old){var v=(Vertex2d)tr.GetObject(id,OpenMode.ForRead);s.Append('|').Append(v.Position).Append('|').Append(v.Bulge);}var link=ReadLink(e,tr);if(link!=null)foreach(var id in link.Labels)s.Append('|').Append(id.Handle).Append('|').Append(OwnedLabel(id,e.ObjectId,db,tr));return Profile.Hash(s.ToString());
        }
        private static string Escape(string text){return text.Replace("\\","\\\\").Replace("{","\\{").Replace("}","\\}");}
        private static Extents3d Measure(Database source,ObjectId style,string text,double height,double padding)
        {
            var previous=HostApplicationServices.WorkingDatabase;using(var scratch=new Database(true,true))try
            {
                ObjectId mapped;using(var map=new IdMapping()){source.WblockCloneObjects(new ObjectIdCollection(new[]{style}),scratch.TextStyleTableId,map,DuplicateRecordCloning.Replace,false);mapped=map[style].Value;}HostApplicationServices.WorkingDatabase=scratch;using(var tr=scratch.TransactionManager.StartTransaction()){var bt=(BlockTable)tr.GetObject(scratch.BlockTableId,OpenMode.ForRead);var space=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);using(var t=new MText()){t.SetDatabaseDefaults(scratch);space.AppendEntity(t);tr.AddNewlyCreatedDBObject(t,true);Configure(t,mapped,text,height,padding);return t.GeometricExtents;}}
            }finally{HostApplicationServices.WorkingDatabase=previous;}
        }
        private static void Configure(MText t,ObjectId style,string text,double height,double padding){t.TextStyleId=style;t.Contents=Escape(text);t.TextHeight=height;t.Width=0;t.Attachment=AttachmentPoint.MiddleCenter;t.Location=Point3d.Origin;t.Normal=Vector3d.ZAxis;t.BackgroundFill=true;t.UseBackgroundColor=true;t.BackgroundScaleFactor=1+2*padding;}
        public static DirectionPlan Inspect(Database db,IEnumerable<ObjectId> ids,DirectionOptions options)
        {
            options.Validate();var plan=new DirectionPlan{OptionsSignature=options.Signature};using(var tr=db.TransactionManager.StartOpenCloseTransaction())foreach(var id in ids.Distinct())
            {
                try
                {
                    if(id.IsNull||!id.IsValid||id.IsErased||id.Database.UnmanagedObject!=db.UnmanagedObject)throw new InvalidOperationException("Đối tượng không còn hợp lệ.");var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)throw new InvalidOperationException("Không phải đường.");var owner=tr.GetObject(e.OwnerId,OpenMode.ForRead) as BlockTableRecord;var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);if(owner==null||!owner.IsLayout||layer.IsDependent||layer.IsLocked||DrawingSearch.Preview(e.Layer))throw new InvalidOperationException("Block/Xref/Layer khóa/đường mẫu.");
                    var pieces=Pieces(e,tr,plan);var link=ReadLink(e,tr);if(options.Restore&&link==null)throw new InvalidOperationException("Chưa có nhãn liên kết để khôi phục.");if(link!=null)foreach(var labelId in link.Labels)if(OwnedLabel(labelId,id,db,tr)){var label=(MText)tr.GetObject(labelId,OpenMode.ForRead);if(((LayerTableRecord)tr.GetObject(label.LayerId,OpenMode.ForRead)).IsLocked)throw new InvalidOperationException("Nhãn liên kết nằm trên Layer khóa.");}
                    var source=ReadSource(db,e,tr);var item=new DirectionItem{Id=id,OriginalType=source.Original,State=State(db,e,tr,source),Text=source.Text,Style=source.Style};
                    if(options.Restore){plan.Items.Add(item);continue;}if(String.IsNullOrEmpty(source.Text)){item.PlainOnly=true;plan.Items.Add(item);continue;}
                    if(pieces.Count==0)throw new InvalidOperationException("Không có đoạn thẳng để đặt nhãn.");
                    item.Height=options.HeightOverride>0?options.HeightOverride:source.Height*db.Ltscale*e.LinetypeScale*options.ViewFactor;if(!Profile.Finite(item.Height)||item.Height<=0||item.Height>1e9)throw new InvalidOperationException("Cao chữ tính ra ngoài phạm vi; nhập cao chữ thủ công.");
                    var ext=Measure(db,source.Style,source.Text,item.Height,options.PaddingRatio);double w=ext.MaxPoint.X-ext.MinPoint.X;if(w<=0)throw new InvalidOperationException("Không đo được kích thước chữ.");item.CenterX=(ext.MinPoint.X+ext.MaxPoint.X)/2;item.CenterY=(ext.MinPoint.Y+ext.MaxPoint.Y)/2;
                    foreach(var piece in pieces){var v=piece.B-piece.A;double length=v.Length;if(length<=1e-9)continue;var positions=DirectionRules.Centers(length,w,item.Height,source.Period*db.Ltscale*e.LinetypeScale*options.ViewFactor,options);if(positions.Count==0)plan.ShortSegments++;foreach(double distance in positions)item.Labels.Add(new DirectionLabel{Center=piece.A+v*(distance/length),Rotation=DirectionRules.Rotation(v.X,v.Y)});}
                    plan.Items.Add(item);if(plan.Items.Sum(i=>i.Labels.Count)>10000)throw new ArgumentException("Phạm vi tạo quá 10000 nhãn; chọn ít tuyến hơn hoặc tăng nhịp.");
                }
                catch(InvalidOperationException ex){plan.Skipped.Add((id.IsNull?"?":id.Handle.ToString())+": "+ex.Message);}
            }return plan;
        }
        public static int Apply(Database db,DirectionPlan plan,DirectionOptions options,bool injectFailure=false)
        {
            options.Validate();if(plan.OptionsSignature!=options.Signature)throw new InvalidOperationException("Thông số đã đổi; kiểm tra lại.");int count=0;using(var tr=db.TransactionManager.StartTransaction())
            {
                foreach(var item in plan.Items)
                {
                    var e=(Entity)tr.GetObject(item.Id,OpenMode.ForRead);var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);var source=ReadSource(db,e,tr);if(layer.IsLocked||State(db,e,tr,source)!=item.State)throw new InvalidOperationException("Tuyến/nét đã đổi sau khi xem phạm vi; chưa áp dụng.");var link=ReadLink(e,tr);if(link!=null)foreach(var id in link.Labels)if(OwnedLabel(id,e.ObjectId,db,tr)){var label=(MText)tr.GetObject(id,OpenMode.ForRead);if(((LayerTableRecord)tr.GetObject(label.LayerId,OpenMode.ForRead)).IsLocked)throw new InvalidOperationException("Nhãn liên kết bị khóa.");label.UpgradeOpen();label.Erase();}
                    e.UpgradeOpen();DisableGeneration(e);
                    if(options.Restore){if(e.LinetypeId==db.ContinuousLinetype)e.LinetypeId=item.OriginalType;RemoveLink(e,tr);count++;continue;}
                    if(item.PlainOnly){if(link!=null){if(e.LinetypeId==db.ContinuousLinetype)e.LinetypeId=item.OriginalType;RemoveLink(e,tr);}count++;continue;}e.LinetypeId=db.ContinuousLinetype;var owner=(BlockTableRecord)tr.GetObject(e.OwnerId,OpenMode.ForWrite);var labels=new List<ObjectId>();
                    foreach(var position in item.Labels)
                    {
                        var label=new MText();label.SetDatabaseDefaults(db);owner.AppendEntity(label);tr.AddNewlyCreatedDBObject(label,true);Configure(label,item.Style,item.Text,item.Height,options.PaddingRatio);label.LayerId=e.LayerId;label.Color=e.Color;label.LineWeight=e.LineWeight;label.LinetypeId=db.ContinuousLinetype;label.Rotation=position.Rotation;double c=Math.Cos(position.Rotation),s=Math.Sin(position.Rotation);label.Location=position.Center-new Vector3d(c*item.CenterX-s*item.CenterY,s*item.CenterX+c*item.CenterY,0);WriteData(label,tr,LabelKey,new[]{new TypedValue(1000,Mark),new TypedValue(330,e.ObjectId)});labels.Add(label.ObjectId);
                    }
                    if(labels.Count>0){var order=(DrawOrderTable)tr.GetObject(owner.DrawOrderTableId,OpenMode.ForWrite);order.MoveToTop(new ObjectIdCollection(labels.ToArray()));}
                    var data=new List<TypedValue>{new TypedValue(1000,Mark),new TypedValue(340,item.OriginalType)};data.AddRange(labels.Select(id=>new TypedValue(330,id)));WriteData(e,tr,RouteKey,data);count++;if(injectFailure)throw new InvalidOperationException("Injected direction rollback.");
                }tr.Commit();
            }if(plan.Items.Count>0)db.Plinegen=false;return count;
        }
    }
}
