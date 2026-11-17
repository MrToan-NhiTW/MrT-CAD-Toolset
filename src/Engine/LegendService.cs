using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using FontDescriptor=Autodesk.AutoCAD.GraphicsInterface.FontDescriptor;
using MedGas2024.Core;
using AcColor=Autodesk.AutoCAD.Colors.Color;

namespace MedGas2024.Engine
{
    public static class LegendService
    {
        const string Key="MRT_LEGEND_V1", MaskKey="MRT_LEGEND_MASK_V1", BlockMaskKey="MRT_LEGEND_BLOCK_MASK_V1";
        const string LegendLayer="Mr.T Bảng ký hiệu";
        static string ShortName(BlockTable blocks,string prefix){string name;do{name=prefix+Guid.NewGuid().ToString("N").Substring(0,8);}while(blocks.Has(name));return name;}
        static string AvailableName(BlockTable blocks,string requested){var name=requested.Trim();try{SymbolUtilityServices.ValidateSymbolName(name,false);}catch(Autodesk.AutoCAD.Runtime.Exception){throw new ArgumentException("Tên block bảng không hợp lệ trong AutoCAD: "+name);}if(!blocks.Has(name))return name;for(int i=2;i<Int32.MaxValue;i++){string candidate=name+"_"+i.ToString(CultureInfo.InvariantCulture);if(!blocks.Has(candidate))return candidate;}throw new InvalidOperationException("Không tìm được tên block chưa sử dụng.");}
        static ObjectId EnsureLegendLayer(Database db,Transaction tr){var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);ObjectId id;if(layers.Has(LegendLayer))id=layers[LegendLayer];else{layers.UpgradeOpen();var layer=new LayerTableRecord{Name=LegendLayer,Color=AcColor.FromColorIndex(ColorMethod.ByAci,7)};id=layers.Add(layer);tr.AddNewlyCreatedDBObject(layer,true);}var result=(LayerTableRecord)tr.GetObject(id,OpenMode.ForRead);if(result.IsLocked||result.IsFrozen||result.IsOff||result.IsDependent)throw new InvalidOperationException("Layer Mr.T Bảng ký hiệu đang khóa, tắt, đóng băng hoặc phụ thuộc Xref. Hãy mở layer trước khi tạo bảng.");return id;}
        static ObjectId Resolve(Database db,string h){return db.GetObjectId(false,new Handle(Int64.Parse(h,NumberStyles.HexNumber,CultureInfo.InvariantCulture)),0);}
        public static string DatabaseKey(Database db){return db.FingerprintGuid.ToString();}
        static string Literal(string text){return (text??"").Replace("\\","\\\\").Replace("{","\\{").Replace("}","\\}").Replace("%","\\U+0025").Replace("\r","").Replace("\n","\\P");}
        static string Plain(string text){using(var m=new MText()){m.Contents=text??"";return m.Text;}}
        static ObjectId EffectiveType(Database db,Entity e,Transaction tr,ObjectId parent){var id=e.LinetypeId;if(id==db.ByLayerLinetype)id=e.LayerId==db.LayerZero&&!parent.IsNull?parent:((LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead)).LinetypeObjectId;if(id==db.ByBlockLinetype)id=parent.IsNull?db.ContinuousLinetype:parent;return id;}
        static AcColor EffectiveColor(Entity e,Transaction tr,AcColor parent){var c=e.Color;if(c.ColorMethod==ColorMethod.ByLayer)c=e.Layer=="0"&&parent!=null?parent:((LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead)).Color;return c.ColorMethod==ColorMethod.ByBlock?(parent??AcColor.FromColorIndex(ColorMethod.ByAci,7)):c;}
        // Selection order is unrelated to DRAWORDER. Append samples back-to-front.
        static IEnumerable<ObjectId> DrawOrder(IEnumerable<ObjectId> ids,Transaction tr)
        {
            var selected=ids.Distinct().ToArray();
            foreach(var group in selected.GroupBy(id=>tr.GetObject(id,OpenMode.ForRead).OwnerId))
            {
                var owner=tr.GetObject(group.Key,OpenMode.ForRead) as BlockTableRecord;
                var wanted=new HashSet<ObjectId>(group);
                if(owner!=null&&!owner.DrawOrderTableId.IsNull)
                {
                    var order=(DrawOrderTable)tr.GetObject(owner.DrawOrderTableId,OpenMode.ForRead);
                    foreach(ObjectId id in order.GetFullDrawOrder(0))if(wanted.Remove(id))yield return id;
                }
                foreach(var id in group)if(wanted.Remove(id))yield return id;
            }
        }
        static List<Entity> Geometry(Database db,IEnumerable<ObjectId> ids,Transaction tr)
        {
            var entities=new List<Entity>();try{var source=ids.ToArray();foreach(var id in source)if(id.IsNull||!id.IsValid||id.IsErased||id.Database.UnmanagedObject!=db.UnmanagedObject)throw new InvalidOperationException("Mẫu không còn tồn tại trong DWG này.");foreach(var id in DrawOrder(source,tr)){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)throw new InvalidOperationException("Mẫu không phải đối tượng hình học.");Flatten(db,e,tr,entities,null,ObjectId.Null,0);}if(entities.Count==0)throw new InvalidOperationException("Mẫu không có hình 2D hiển thị. POINT ở PDMODE=1 đang bị ẩn.");var bounds=Bounds(entities);if(Math.Abs(bounds.MaxPoint.Z-bounds.MinPoint.Z)>1e-5)throw new InvalidOperationException("Mẫu có hình 3D; dùng hình chiếu 2D trên mặt phẳng XY trước.");return entities;}catch{foreach(var e in entities)e.Dispose();throw;}
        }
        static bool HasMask(BlockTableRecord btr,Transaction tr,int depth)
        {
            if(depth>12)return true;
            foreach(ObjectId id in btr){var entity=tr.GetObject(id,OpenMode.ForRead) as Entity;if(entity==null||!entity.Visible)continue;if(entity is Wipeout)return true;var nested=entity as BlockReference;if(nested!=null){var record=(BlockTableRecord)tr.GetObject(nested.BlockTableRecord,OpenMode.ForRead);if(!record.IsFromExternalReference&&!record.IsFromOverlayReference&&HasMask(record,tr,depth+1))return true;}}
            return false;
        }
        static void Flatten(Database db,Entity e,Transaction tr,List<Entity> output,AcColor parent,ObjectId parentType,int depth)
        {
            if(!e.Visible)return;if(depth>12||output.Count>10000)throw new InvalidOperationException("Ký hiệu quá phức tạp; chọn một hình 2D gọn hơn.");
            var color=EffectiveColor(e,tr,parent);var type=EffectiveType(db,e,tr,parentType);var block=e as BlockReference;
            if(block!=null)
            {
                if(e is Table)throw new InvalidOperationException("Không dùng cả bảng làm một ký hiệu.");var btr=(BlockTableRecord)tr.GetObject(block.BlockTableRecord,OpenMode.ForRead);if(btr.IsFromExternalReference||btr.IsFromOverlayReference)throw new InvalidOperationException("Chọn block nội bộ; Xref chưa hỗ trợ trong bản thử.");
                // Traverse the evaluated definition in display order, including nested masks.
                // Exploding a whole block loses the correspondence with its sortents table.
                if(HasMask(btr,tr,depth))
                {
                    int start=output.Count;
                    foreach(var id in DrawOrder(btr.Cast<ObjectId>(),tr))Flatten(db,(Entity)tr.GetObject(id,OpenMode.ForRead),tr,output,color,type,depth+1);
                    for(int i=start;i<output.Count;i++)
                    {
                        try{output[i].TransformBy(block.BlockTransform);}
                        catch(Autodesk.AutoCAD.Runtime.Exception ex){throw new InvalidOperationException("Không biến đổi được mẫu block chứa WIPEOUT (có thể scale không đều). Dùng bản sao 2D đã EXPLODE. "+ex.ErrorStatus,ex);}
                    }
                }
                else {var parts=new DBObjectCollection();try{block.Explode(parts);foreach(DBObject part in parts){var child=part as Entity;if(child!=null)Flatten(db,child,tr,output,color,type,depth+1);}}finally{foreach(DBObject part in parts)part.Dispose();}}
                foreach(ObjectId aid in block.AttributeCollection){var a=(AttributeReference)tr.GetObject(aid,OpenMode.ForRead);if(a.Invisible)continue;DBText text;if(a.IsMTextAttribute){var mt=a.MTextAttribute;try{Flatten(db,mt,tr,output,color,type,depth+1);}finally{mt.Dispose();}continue;}text=new DBText{TextString=a.TextString,Position=a.Position,Height=a.Height,Rotation=a.Rotation,WidthFactor=a.WidthFactor,Oblique=a.Oblique,Normal=a.Normal};text.TextStyleId=a.TextStyleId;text.Color=EffectiveColor(a,tr,color);text.LayerId=db.LayerZero;output.Add(text);}return;
            }
            var ad=e as AttributeDefinition;if(ad!=null&&!ad.Constant)return;
            if(e is Dimension||e is MLeader)
            {
                var parts=new DBObjectCollection();int start=output.Count;
                try{e.Explode(parts);foreach(DBObject part in parts){var child=part as Entity;if(child!=null)Flatten(db,child,tr,output,color,type,depth+1);}}
                finally{foreach(DBObject part in parts)part.Dispose();}
                if(output.Count==start)throw new InvalidOperationException("Không lấy được hình hiển thị của "+e.GetType().Name+". Dùng bản sao 2D đã EXPLODE.");return;
            }
            if(e is DBPoint){var point=(DBPoint)e;double size=db.Pdsize>0?db.Pdsize:1;foreach(var stroke in PointGlyph.Create(db.Pdmode)){var glyph=new Polyline();glyph.SetDatabaseDefaults(db);for(int i=0;i<stroke.Points.Length/2;i++)glyph.AddVertexAt(i,new Point2d(stroke.Points[i*2]*size,stroke.Points[i*2+1]*size),0,0,0);glyph.Color=color;glyph.LinetypeId=db.ContinuousLinetype;glyph.LayerId=db.LayerZero;glyph.TransformBy(Matrix3d.PlaneToWorld(point.Normal));glyph.TransformBy(Matrix3d.Displacement(point.Position-Point3d.Origin));output.Add(glyph);}return;}
            if(!(e is Curve||e is DBText||e is MText||e is Solid||e is Hatch||e is Wipeout))throw new InvalidOperationException("Đối tượng chưa hỗ trợ trong bảng ký hiệu: "+e.GetType().Name+" ("+e.GetRXClass().DxfName+"). Nhận đường/chữ 2D, Hatch/Solid, WIPEOUT, POINT, block nội bộ, DIMENSION và MLEADER. Ảnh, PDF/DGN/DWF underlay, Xref, OLE, proxy và mô hình 3D chưa hỗ trợ; dùng bản sao hình chiếu 2D.");
            // Hatch.Associative writes reactors and therefore cannot be changed on a
            // transient clone. Keep the clone untouched during read-only capture; its
            // association is removed only after the sample belongs to the database.
            var copy=(Entity)e.Clone();try{copy.Color=color;copy.LinetypeId=type;copy.LayerId=db.LayerZero;if(copy.LineWeight==LineWeight.ByLayer)copy.LineWeight=((LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead)).LineWeight;if(copy.LineWeight==LineWeight.ByBlock)copy.LineWeight=LineWeight.LineWeight000;var ex=copy.GeometricExtents;if(Math.Abs(ex.MaxPoint.Z-ex.MinPoint.Z)>1e-5)throw new InvalidOperationException("Mẫu có hình 3D; dùng hình chiếu 2D trước.");output.Add(copy);}catch{copy.Dispose();throw;}
        }
        static Extents3d Bounds(IEnumerable<Entity> entities){Extents3d? bounds=null;foreach(var e in entities){var ex=e.GeometricExtents;if(bounds.HasValue){var b=bounds.Value;b.AddExtents(ex);bounds=b;}else bounds=ex;}if(!bounds.HasValue)throw new InvalidOperationException("Không đo được phạm vi mẫu.");return bounds.Value;}
        static List<LegendStroke> Strokes(List<Entity> entities)
        {
            var result=new List<LegendStroke>();var bounds=Bounds(entities);double w=Math.Max(1e-6,bounds.MaxPoint.X-bounds.MinPoint.X),h=Math.Max(1e-6,bounds.MaxPoint.Y-bounds.MinPoint.Y),size=Math.Max(w,h);
            foreach(var e in entities)
            {
                var wipe=e as Wipeout;var curve=e as Curve;if(wipe==null&&curve==null)continue;
                try
                {
                    var vertices=new List<Point3d>();
                    if(wipe!=null){foreach(Point2d p in wipe.GetClipBoundary())vertices.Add(new Point3d(p.X,p.Y,0).TransformBy(wipe.PixelToModelTransform));}
                    else {int n=e is Line?1:48;for(int i=0;i<=n;i++)vertices.Add(curve.GetPointAtParameter(curve.StartParam+(curve.EndParam-curve.StartParam)*i/n));}
                    var points=new List<double>();foreach(var p in vertices){points.Add((p.X-bounds.MinPoint.X-w/2)/size);points.Add((p.Y-bounds.MinPoint.Y-h/2)/size);}
                    result.Add(new LegendStroke{Points=points.ToArray(),Mask=wipe!=null});if(result.Count>=300)break;
                }catch(Autodesk.AutoCAD.Runtime.Exception){/* Preview only. Native sample still retains the original geometry. */}
            }return result;
        }
        public static LegendRow Capture(Database db,ObjectId[] ids,bool pipe)
        {
            if(ids==null||ids.Length==0)throw new ArgumentException("Chọn đối tượng mẫu.");using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var first=(Entity)tr.GetObject(ids[0],OpenMode.ForRead);if(pipe&&(!(first is Line||first is Polyline)||ids.Length!=1))throw new InvalidOperationException("Mẫu nét ống: chọn một Line hoặc Polyline 2D.");
                var row=new LegendRow{Kind=pipe?"Nét ống":"Hình 2D",Sources=ids.Select(id=>id.Handle.ToString()).ToArray(),Label=first.Layer};var br=first as BlockReference;if(br!=null)row.Label=((BlockTableRecord)tr.GetObject(br.IsDynamicBlock?br.DynamicBlockTableRecord:br.BlockTableRecord,OpenMode.ForRead)).Name;
                if(pipe){row.PipeSourceLayer=first.Layer;row.PipeSourceHandle=first.Handle.ToString();row.PipeSourceDatabaseKey=DatabaseKey(db);var type=(LinetypeTableRecord)tr.GetObject(EffectiveType(db,first,tr,ObjectId.Null),OpenMode.ForRead);var labels=new List<string>();for(int i=0;i<type.NumDashes;i++)if(!type.ShapeStyleAt(i).IsNull){try{string t=type.TextAt(i);if(!String.IsNullOrWhiteSpace(t))labels.Add(t);}catch(Autodesk.AutoCAD.Runtime.Exception){}}row.Label=labels.Count==0?type.Name:String.Join(" ",labels);}
                if(first is DBPoint){row.Kind="POINT";row.Label="POINT (PDMODE="+db.Pdmode+")";if(db.Pdmode==0)row.SampleScale=.12;}if(ids.Length==1&&first is Wipeout){row.Kind="WIPEOUT";row.Label="WIPEOUT (vùng che)";}var parts=Geometry(db,ids,tr);try{row.Strokes=Strokes(parts);}finally{foreach(var p in parts)p.Dispose();}return row;
            }
        }
        static ObjectId SnapshotType(Database db,ObjectId id,Transaction tr,Dictionary<ObjectId,ObjectId> cache)
        {
            // Legend samples follow the existing named definition; never create opaque Linetype copies.
            return id;
        }
        static bool Near(double a,double b){return Math.Abs(a-b)<=Math.Max(1e-9,Math.Max(Math.Abs(a),Math.Abs(b))*1e-9);}
        static bool SamePipeSample(BlockTableRecord record,Transaction tr,Polyline expected,ObjectId legendLayer)
        {
            var ids=record.Cast<ObjectId>().ToArray();if(ids.Length!=1)return false;var old=tr.GetObject(ids[0],OpenMode.ForRead) as Polyline;if(old==null||old.NumberOfVertices!=2||old.Closed||old.LayerId!=legendLayer)return false;
            var a=old.GetPoint2dAt(0);var b=old.GetPoint2dAt(1);var c=old.Color;var d=expected.Color;
            return Near(a.Y,0)&&Near(b.Y,0)&&Near(a.X+b.X,0)&&b.X>a.X&&Near(old.GetBulgeAt(0),0)&&Near(old.Length,expected.Length)&&Near(old.ConstantWidth,expected.ConstantWidth)&&Near(old.LinetypeScale,expected.LinetypeScale)&&old.LinetypeId==expected.LinetypeId&&old.LineWeight==expected.LineWeight&&c.ColorMethod==d.ColorMethod&&c.ColorIndex==d.ColorIndex&&c.Red==d.Red&&c.Green==d.Green&&c.Blue==d.Blue;
        }
        static ObjectId Sample(Database db,LegendRow row,LegendSpec spec,Transaction tr,Dictionary<ObjectId,ObjectId> types,ObjectId legendLayer)
        {
            BlockTableRecord priorSample=null;if(!String.IsNullOrEmpty(row.SampleBlock)){var cached=Resolve(db,row.SampleBlock);var record=tr.GetObject(cached,OpenMode.ForRead) as BlockTableRecord;if(record==null)throw new InvalidOperationException("Mẫu lưu trong bảng không hợp lệ.");if(record.Name.StartsWith("Mr.T_LG_",StringComparison.Ordinal)&&row.Kind!="Nét ống"&&record.Cast<ObjectId>().All(id=>((Entity)tr.GetObject(id,OpenMode.ForRead)).LayerId==legendLayer))return cached;if(record.Name.StartsWith("Mr.T_LG_",StringComparison.Ordinal)&&row.Kind=="Nét ống")priorSample=record;row.Sources=record.Cast<ObjectId>().Select(x=>x.Handle.ToString()).ToArray();row.SampleBlock=null;
                // MRTLEGEDIT used to rebuild a pipe sample from the old sample itself, so edits never
                // picked up linetype/colour changes. Prefer the remembered source pipe while it exists.
                if(row.Kind=="Nét ống"&&row.PipeSourceDatabaseKey==DatabaseKey(db)&&!String.IsNullOrEmpty(row.PipeSourceHandle)){try{var live=Resolve(db,row.PipeSourceHandle);var src=live.IsNull||live.IsErased?null:tr.GetObject(live,OpenMode.ForRead) as Entity;if(src is Line||src is Polyline)row.Sources=new[]{row.PipeSourceHandle};}catch(System.Exception){}}}
            if(row.Sources==null||row.Sources.Length==0)return ObjectId.Null;
            var ids=row.Sources.Select(s=>Resolve(db,s)).ToArray();var entities=Geometry(db,ids,tr);
            try
            {
                if(row.Kind=="Nét ống")
                {
                    foreach(var p in entities)p.Dispose();entities.Clear();var source=(Entity)tr.GetObject(ids[0],OpenMode.ForRead);var lt=(LinetypeTableRecord)tr.GetObject(EffectiveType(db,source,tr,ObjectId.Null),OpenMode.ForRead);double sourceWidth=0;var sourcePolyline=source as Polyline;if(sourcePolyline!=null&&sourcePolyline.NumberOfVertices>1){double w=sourcePolyline.GetStartWidthAt(0);bool constant=true;for(int v=0;v<sourcePolyline.NumberOfVertices-1;v++)if(Math.Abs(sourcePolyline.GetStartWidthAt(v)-w)>1e-8||Math.Abs(sourcePolyline.GetEndWidthAt(v)-w)>1e-8)constant=false;if(constant)sourceWidth=w;}if(!row.PipeBaseline){row.PipeBaseScale=source.LinetypeScale;row.PipeBaseWidth=sourceWidth;row.PipeBaseline=true;}double pipeScale,pipeWidth;spec.PipeValues(row.PipeBaseScale,row.PipeBaseWidth,row,out pipeScale,out pipeWidth);double length=spec.SymbolWidth*spec.DrawingFactor;if(!Profile.Finite(length)||length<=0)throw new InvalidOperationException("Chiều dài mẫu ống không hợp lệ.");var line=new Polyline();line.SetDatabaseDefaults(db);line.AddVertexAt(0,new Point2d(0,0),0,0,0);line.AddVertexAt(1,new Point2d(length,0),0,0,0);line.Color=EffectiveColor(source,tr,null);line.LinetypeId=lt.ObjectId;line.LinetypeScale=pipeScale;line.LineWeight=source.LineWeight==LineWeight.ByLayer?((LayerTableRecord)tr.GetObject(source.LayerId,OpenMode.ForRead)).LineWeight:source.LineWeight;line.ConstantWidth=pipeWidth;entities.Add(line);
                }
                if(priorSample!=null&&entities.Count==1&&entities[0] is Polyline&&SamePipeSample(priorSample,tr,(Polyline)entities[0],legendLayer)){row.SampleBlock=priorSample.Handle.ToString();return priorSample.ObjectId;}
                var bounds=Bounds(entities);var centre=new Point3d((bounds.MinPoint.X+bounds.MaxPoint.X)/2,(bounds.MinPoint.Y+bounds.MaxPoint.Y)/2,bounds.MinPoint.Z);var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);var sample=new BlockTableRecord{Name=ShortName(blocks,"Mr.T_LG_"),Origin=Point3d.Origin};blocks.Add(sample);tr.AddNewlyCreatedDBObject(sample,true);
                foreach(var e in entities){e.TransformBy(Matrix3d.Displacement(Point3d.Origin-centre));e.LinetypeId=SnapshotType(db,e.LinetypeId,tr,types);e.LayerId=legendLayer;sample.AppendEntity(e);tr.AddNewlyCreatedDBObject(e,true);var hatch=e as Hatch;if(hatch!=null&&hatch.Associative)hatch.Associative=false;}row.SampleBlock=sample.Handle.ToString();return sample.ObjectId;
            }
            finally{foreach(var e in entities)e.Dispose();}
        }
        static ObjectId FontStyle(Database db,Transaction tr,string family,bool bold)
        {
            var styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForWrite);string name="Mr.T_LG_TX_"+Profile.Hash(family+"|"+bold).Substring(0,12);if(styles.Has(name))return styles[name];var style=new TextStyleTableRecord{Name=name};styles.Add(style);tr.AddNewlyCreatedDBObject(style,true);style.TextSize=0;style.XScale=1;style.Font=new FontDescriptor(family,bold,false,0,0);return style.ObjectId;
        }
        static bool ReuseTableStyle(Table old,Transaction tr,ObjectId font,LegendSpec spec)
        {
            if(old==null||old.TableStyle.IsNull)return false;try{var s=tr.GetObject(old.TableStyle,OpenMode.ForRead) as TableStyle;if(s==null||s.IsTitleSuppressed||s.IsHeaderSuppressed||!Near(s.HorizontalCellMargin,2*spec.DrawingFactor)||!Near(s.VerticalCellMargin,spec.DrawingFactor))return false;
                foreach(RowType row in new[]{RowType.DataRow,RowType.HeaderRow,RowType.TitleRow}){if(s.TextStyle(row)!=font||!Near(s.TextHeight(row),spec.BodyHeight*spec.DrawingFactor)||s.Color(row).ColorMethod!=ColorMethod.ByBlock)return false;foreach(GridLineType g in new[]{GridLineType.HorizontalTop,GridLineType.HorizontalInside,GridLineType.HorizontalBottom,GridLineType.VerticalLeft,GridLineType.VerticalInside,GridLineType.VerticalRight})if(s.GridColor(g,row).ColorMethod!=ColorMethod.ByBlock)return false;}return true;
            }catch(Autodesk.AutoCAD.Runtime.Exception){return false;}
        }
        internal static void SaveSpec(Table table,Transaction tr,LegendSpec spec){Save(table,tr,spec);}
        static void Save(Table table,Transaction tr,LegendSpec spec){if(table.ExtensionDictionary.IsNull)table.CreateExtensionDictionary();var dict=(DBDictionary)tr.GetObject(table.ExtensionDictionary,OpenMode.ForWrite);Xrecord r;if(dict.Contains(Key))r=(Xrecord)tr.GetObject(dict.GetAt(Key),OpenMode.ForWrite);else{r=new Xrecord();dict.SetAt(Key,r);tr.AddNewlyCreatedDBObject(r,true);}string json=spec.Serialize();var values=new List<TypedValue>();for(int i=0;i<json.Length;i+=200)values.Add(new TypedValue((int)DxfCode.Text,json.Substring(i,Math.Min(200,json.Length-i))));using(var data=new ResultBuffer(values.ToArray()))r.Data=data;}
        static void SaveBlockMask(Table table,Wipeout mask,Transaction tr){var dict=(DBDictionary)tr.GetObject(table.ExtensionDictionary,OpenMode.ForWrite);var record=new Xrecord();dict.SetAt(BlockMaskKey,record);tr.AddNewlyCreatedDBObject(record,true);using(var data=new ResultBuffer(new TypedValue((int)DxfCode.SoftPointerId,mask.ObjectId)))record.Data=data;}
        static Wipeout FindBlockMask(Table table,BlockTableRecord definition,Transaction tr)
        {
            if(!table.ExtensionDictionary.IsNull){var dict=(DBDictionary)tr.GetObject(table.ExtensionDictionary,OpenMode.ForRead);if(dict.Contains(BlockMaskKey)){var record=tr.GetObject(dict.GetAt(BlockMaskKey),OpenMode.ForRead) as Xrecord;if(record!=null){TypedValue[] values;using(var data=record.Data)values=data==null?new TypedValue[0]:data.AsArray();if(values.Length==1&&values[0].Value is ObjectId){var id=(ObjectId)values[0].Value;if(!id.IsNull&&id.IsValid&&!id.IsErased){var mask=tr.GetObject(id,OpenMode.ForRead) as Wipeout;if(mask!=null&&mask.OwnerId==definition.ObjectId)return mask;}}}}}
            // Legacy blocks did not store a mask id. Only replace a uniquely matching
            // rectangle; unknown components remain untouched.
            var x=table.Direction.GetNormal();var z=table.Normal.GetNormal();var y=z.CrossProduct(x).GetNormal();var corners=new[]{table.Position,table.Position+x*table.Width,table.Position-y*table.Height,table.Position+x*table.Width-y*table.Height};double minX=corners.Min(p=>p.X),maxX=corners.Max(p=>p.X),minY=corners.Min(p=>p.Y),maxY=corners.Max(p=>p.Y),minZ=corners.Min(p=>p.Z),maxZ=corners.Max(p=>p.Z);double tolerance=Math.Max(1e-6,Math.Max(table.Width,table.Height)*1e-7);var matches=new List<Wipeout>();
            foreach(ObjectId id in definition){var wipe=tr.GetObject(id,OpenMode.ForRead) as Wipeout;if(wipe==null)continue;try{var ext=wipe.GeometricExtents;if(Math.Abs(ext.MinPoint.X-minX)<=tolerance&&Math.Abs(ext.MaxPoint.X-maxX)<=tolerance&&Math.Abs(ext.MinPoint.Y-minY)<=tolerance&&Math.Abs(ext.MaxPoint.Y-maxY)<=tolerance&&Math.Abs(ext.MinPoint.Z-minZ)<=tolerance&&Math.Abs(ext.MaxPoint.Z-maxZ)<=tolerance)matches.Add(wipe);}catch(Autodesk.AutoCAD.Runtime.Exception){}}
            return matches.Count==1?matches[0]:null;
        }
        static void AddMask(Database db,Table table,BlockTableRecord space,Transaction tr)
        {
            var x=table.Direction.GetNormal();var z=table.Normal.GetNormal();var y=z.CrossProduct(x).GetNormal();double width=table.Width,height=table.Height;if(width<=0||height<=0)throw new InvalidOperationException("Không xác định được kích thước bảng để tạo vùng che.");
            var points=new Point2dCollection(new[]{new Point2d(0,0),new Point2d(width,0),new Point2d(width,-height),new Point2d(0,-height),new Point2d(0,0)});var wipe=new Wipeout();wipe.SetDatabaseDefaults(db);wipe.LayerId=table.LayerId;wipe.SetFrom(points,Vector3d.ZAxis);wipe.TransformBy(Matrix3d.AlignCoordinateSystem(Point3d.Origin,Vector3d.XAxis,Vector3d.YAxis,Vector3d.ZAxis,table.Position,x,y,z));space.AppendEntity(wipe);tr.AddNewlyCreatedDBObject(wipe,true);
            var groups=(DBDictionary)tr.GetObject(db.GroupDictionaryId,OpenMode.ForWrite);var group=new Group("Mr.T bảng ký hiệu và vùng che",true);string name;do{name="MRT_LG_"+Guid.NewGuid().ToString("N").Substring(0,8);}while(groups.Contains(name));groups.SetAt(name,group);tr.AddNewlyCreatedDBObject(group,true);group.Append(new ObjectIdCollection(new[]{wipe.ObjectId,table.ObjectId}));
            if(table.ExtensionDictionary.IsNull)table.CreateExtensionDictionary();var dict=(DBDictionary)tr.GetObject(table.ExtensionDictionary,OpenMode.ForWrite);Xrecord record;if(dict.Contains(MaskKey))record=(Xrecord)tr.GetObject(dict.GetAt(MaskKey),OpenMode.ForWrite);else{record=new Xrecord();dict.SetAt(MaskKey,record);tr.AddNewlyCreatedDBObject(record,true);}using(var data=new ResultBuffer(new TypedValue((int)DxfCode.SoftPointerId,wipe.ObjectId),new TypedValue((int)DxfCode.SoftPointerId,group.ObjectId)))record.Data=data;
            var order=(DrawOrderTable)tr.GetObject(space.DrawOrderTableId,OpenMode.ForWrite);order.MoveToTop(new ObjectIdCollection(new[]{wipe.ObjectId}));order.MoveToTop(new ObjectIdCollection(new[]{table.ObjectId}));
        }
        static void EraseOldMask(Table table,Transaction tr)
        {
            var candidates=new List<Tuple<Group,Wipeout>>();var groups=(DBDictionary)tr.GetObject(table.Database.GroupDictionaryId,OpenMode.ForRead);
            foreach(DBDictionaryEntry entry in groups){if(entry.Value.IsNull||entry.Value.IsErased)continue;var group=tr.GetObject(entry.Value,OpenMode.ForRead) as Group;if(group==null||group.Description!="Mr.T bảng ký hiệu và vùng che")continue;var members=group.GetAllEntityIds();if(members.Length!=2||!members.Contains(table.ObjectId))continue;var other=members.Single(x=>x!=table.ObjectId);if(other.IsNull||!other.IsValid||other.IsErased)continue;var wipe=tr.GetObject(other,OpenMode.ForRead) as Wipeout;if(wipe!=null&&wipe.OwnerId==table.OwnerId)candidates.Add(Tuple.Create(group,wipe));}
            if(candidates.Count>1)throw new InvalidOperationException("Có nhiều nhóm vùng che cùng liên kết bảng; không thể xác định duy nhất. Kiểm tra trước khi sửa.");
            if(candidates.Count==1){var pair=candidates[0];pair.Item1.UpgradeOpen();pair.Item2.UpgradeOpen();pair.Item1.Erase();pair.Item2.Erase();return;}
            if(table.ExtensionDictionary.IsNull)return;var dict=(DBDictionary)tr.GetObject(table.ExtensionDictionary,OpenMode.ForRead);if(!dict.Contains(MaskKey))return;var record=tr.GetObject(dict.GetAt(MaskKey),OpenMode.ForRead) as Xrecord;if(record==null)return;TypedValue[] values;using(var data=record.Data)values=data==null?new TypedValue[0]:data.AsArray();if(values.Length==2&&values[0].Value is ObjectId){var id=(ObjectId)values[0].Value;if(!id.IsNull&&id.IsValid&&!id.IsErased){var wipe=tr.GetObject(id,OpenMode.ForRead) as Wipeout;if(wipe!=null&&wipe.OwnerId==table.OwnerId)throw new InvalidOperationException("Vùng che cũ không còn nhóm xác thực; không tự xóa để tránh tác động bảng khác. Hãy kiểm tra liên kết vùng che.");}}
        }
        // Refresh the owned mask only; never delete unrelated groups or masks.
        // The entire replacement and draw-order change share one transaction.
        public static bool RefreshMask(Database db,ObjectId tableId)
        {
            using(var tr=db.TransactionManager.StartTransaction()){
                if(tableId.IsNull||!tableId.IsValid||tableId.IsErased||tableId.Database.UnmanagedObject!=db.UnmanagedObject)throw new InvalidOperationException("Bảng không còn tồn tại trong bản vẽ hiện tại.");
                var table=tr.GetObject(tableId,OpenMode.ForWrite) as Table;if(table==null)throw new InvalidOperationException("Chọn đối tượng TABLE.");
                var layer=(LayerTableRecord)tr.GetObject(table.LayerId,OpenMode.ForRead);var space=tr.GetObject(table.OwnerId,OpenMode.ForWrite) as BlockTableRecord;
                if(layer.IsLocked||layer.IsDependent||space==null||!space.IsLayout)throw new InvalidOperationException("Bảng trên Layer khóa/Xref hoặc ở trong định nghĩa block; không thể tạo vùng che.");
                bool replacing=false;
                if(!table.ExtensionDictionary.IsNull){var dict=(DBDictionary)tr.GetObject(table.ExtensionDictionary,OpenMode.ForRead);if(dict.Contains(MaskKey)){
                    var record=tr.GetObject(dict.GetAt(MaskKey),OpenMode.ForRead) as Xrecord;if(record==null)throw new InvalidOperationException("Liên kết vùng che cũ không hợp lệ; kiểm tra thủ công trước.");
                    TypedValue[] v;using(var data=record.Data){v=data==null?new TypedValue[0]:data.AsArray();}
                    if(v.Length!=2||!(v[0].Value is ObjectId)||!(v[1].Value is ObjectId))throw new InvalidOperationException("Liên kết vùng che cũ không hợp lệ; kiểm tra thủ công trước.");
                    var mask=(ObjectId)v[0].Value;var groupId=(ObjectId)v[1].Value;
                    if(mask.IsNull||groupId.IsNull||!mask.IsValid||!groupId.IsValid||mask.IsErased||groupId.IsErased)throw new InvalidOperationException("Vùng che hoặc nhóm cũ bị thiếu; kiểm tra thủ công trước để tránh tạo trùng.");
                    var wipe=tr.GetObject(mask,OpenMode.ForRead) as Wipeout;var group=tr.GetObject(groupId,OpenMode.ForRead) as Group;var members=group==null?new ObjectId[0]:group.GetAllEntityIds();
                    if(wipe==null||wipe.OwnerId!=table.OwnerId||members.Length!=2||!members.Contains(tableId)||!members.Contains(mask))throw new InvalidOperationException("Nhóm/vùng che cũ đã thay đổi; kiểm tra thủ công trước.");
                    EraseOldMask(table,tr);replacing=true;
                }}
                AddMask(db,table,space,tr);tr.Commit();return replacing;
            }
        }
        static Table LegendTable(Transaction tr,ObjectId id,out BlockReference wrapper,out double factor)
        {
            wrapper=null;factor=1;var obj=tr.GetObject(id,OpenMode.ForRead);var direct=obj as Table;
            if(direct!=null)return direct;
            wrapper=obj as BlockReference;if(wrapper==null)throw new InvalidOperationException("Chọn bảng ký hiệu Mr.T hoặc block bảng Mr.T.");
            var def=(BlockTableRecord)tr.GetObject(wrapper.BlockTableRecord,OpenMode.ForRead);
            if(def.IsFromExternalReference||def.IsFromOverlayReference)throw new InvalidOperationException("Không sửa bảng trong Xref.");
            var scale=wrapper.ScaleFactors;factor=scale.X;
            if(factor<=0||Math.Abs(scale.Y-factor)>1e-8*Math.Max(1,factor)||Math.Abs(scale.Z-factor)>1e-8*Math.Max(1,factor))throw new InvalidOperationException("Bảng block bị mirror hoặc scale không đều. Hãy đưa về scale đều trước khi sửa.");
            Table found=null;foreach(ObjectId child in def){var table=tr.GetObject(child,OpenMode.ForRead) as Table;if(table==null||table.ExtensionDictionary.IsNull)continue;var d=(DBDictionary)tr.GetObject(table.ExtensionDictionary,OpenMode.ForRead);if(!d.Contains(Key))continue;if(found!=null)throw new InvalidOperationException("Block bảng chứa nhiều bảng Mr.T; không thể tự xác định.");found=table;}
            if(found==null)throw new InvalidOperationException("Block bảng thiếu dữ liệu Mr.T; không thể sửa tự động.");return found;
        }
        public static bool IsLegend(Database db,ObjectId id)
        {
            try{using(var tr=db.TransactionManager.StartOpenCloseTransaction()){BlockReference wrapper;double factor;var table=LegendTable(tr,id,out wrapper,out factor);if(table.ExtensionDictionary.IsNull)return false;return ((DBDictionary)tr.GetObject(table.ExtensionDictionary,OpenMode.ForRead)).Contains(Key);}}catch{return false;}
        }
        public static int ReferenceCount(Database db,ObjectId id)
        {
            using(var tr=db.TransactionManager.StartOpenCloseTransaction()){BlockReference wrapper;double factor;LegendTable(tr,id,out wrapper,out factor);if(wrapper==null)return 0;var definition=(BlockTableRecord)tr.GetObject(wrapper.BlockTableRecord,OpenMode.ForRead);return definition.GetBlockReferenceIds(true,false).Cast<ObjectId>().Count(x=>!x.IsNull&&x.IsValid&&!x.IsErased);}
        }
        public static ObjectId ResolveLegend(Database db,ObjectId picked)
        {
            if(picked.IsNull||!picked.IsValid||picked.IsErased)return ObjectId.Null;if(IsLegend(db,picked))return picked;
            using(var tr=db.TransactionManager.StartOpenCloseTransaction()){if(!(tr.GetObject(picked,OpenMode.ForRead) is Wipeout))return ObjectId.Null;var groups=(DBDictionary)tr.GetObject(db.GroupDictionaryId,OpenMode.ForRead);var found=new List<ObjectId>();foreach(DBDictionaryEntry entry in groups){if(entry.Value.IsNull||entry.Value.IsErased)continue;var group=tr.GetObject(entry.Value,OpenMode.ForRead) as Group;if(group==null||group.Description!="Mr.T bảng ký hiệu và vùng che")continue;var members=group.GetAllEntityIds();if(members.Length!=2||!members.Contains(picked))continue;var id=members.Single(x=>x!=picked);if(id.IsNull||!id.IsValid||id.IsErased)continue;var table=tr.GetObject(id,OpenMode.ForRead) as Table;if(table!=null&&!table.ExtensionDictionary.IsNull&&((DBDictionary)tr.GetObject(table.ExtensionDictionary,OpenMode.ForRead)).Contains(Key))found.Add(id);}return found.Distinct().Count()==1?found[0]:ObjectId.Null;}
        }
        static void ResetTable(Table table,int rows,int columns)
        {
            for(int r=0;r<table.Rows.Count;r++)for(int c=0;c<table.Columns.Count;c++){CellRange range;if(table.IsMergedCell(r,c,out range))table.UnmergeCells(range);}
            table.SetSize(rows,columns);
            for(int r=0;r<rows;r++)for(int c=0;c<columns;c++){if(table.Cells[r,c].Contents.Count>0)table.Cells[r,c].DeleteContent();table.Cells[r,c].ToolTip="";}
        }
        public static LegendSpec Read(Database db,ObjectId tableId)
        {
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                BlockReference wrapper;double factor;var t=LegendTable(tr,tableId,out wrapper,out factor);if(t.ExtensionDictionary.IsNull)throw new InvalidOperationException("Chọn bảng tạo bởi Mr.T Bảng ký hiệu.");var d=(DBDictionary)tr.GetObject(t.ExtensionDictionary,OpenMode.ForRead);if(!d.Contains(Key))throw new InvalidOperationException("Bảng này chưa có cấu hình Mr.T.");var record=(Xrecord)tr.GetObject(d.GetAt(Key),OpenMode.ForRead);LegendSpec spec;using(var data=record.Data)spec=LegendSpec.Parse(String.Concat(data.AsArray().Select(v=>Convert.ToString(v.Value))));spec.DatabaseKey=DatabaseKey(db);if(wrapper!=null)spec.BlockName=((BlockTableRecord)tr.GetObject(wrapper.BlockTableRecord,OpenMode.ForRead)).Name;
                if(t.Columns.Count!=2&&t.Columns.Count!=4)throw new InvalidOperationException("Bản thử nhận bảng 2 hoặc 4 cột. Đổi bố cục bằng cửa sổ Mr.T.");
                LegendMetrics.ReadNative(spec,t.Width*factor,t.Columns[0].Width*factor,t.Cells[0,0].Contents[0].TextHeight*factor,t.Cells[1,1].Contents[0].TextHeight*factor,t.Rows[1].Height*factor);
                var originals=spec.Rows;var current=new List<LegendRow>();int groups=t.Columns.Count/2,firstGroup=0;bool note=t.Rows.Count>2&&t.Cells[t.Rows.Count-1,0].ToolTip=="MRTLG_NOTES";int last=t.Rows.Count-(note?1:0);
                for(int group=0;group<groups;group++)for(int r=2;r<last;r++)
                {
                    int c=group*2;string marker=t.Cells[r,c].ToolTip??"",description=Plain(t.Cells[r,c+1].TextString);ObjectId block=(t.Cells[r,c].ContentTypes&CellContentTypes.Block)!=0?t.Cells[r,c].BlockTableRecordId:ObjectId.Null;
                    var match=originals.FirstOrDefault(z=>marker=="MRTLG:"+z.Id);if(match==null&&block.IsNull&&String.IsNullOrWhiteSpace(description)&&group>0)continue;
                    var row=match??new LegendRow();if(match!=null){var temp=new LegendSpec();temp.Rows.Add(match);row=LegendSpec.Parse(temp.Serialize()).Rows[0];}row.Use=true;row.Description=description;row.MinHeight=t.Rows[r].Height*factor/spec.DrawingFactor;
                    if(!block.IsNull){row.SampleBlock=block.Handle.ToString();row.Sources=new string[0];if(match==null){row.Kind="Hình 2D";row.Label=((BlockTableRecord)tr.GetObject(block,OpenMode.ForRead)).Name;}}
                    else {row.SampleBlock=null;row.Sources=new string[0];row.Kind="Trống";}
                    current.Add(row);if(group==0)firstGroup++;
                }
                // Keep unchecked draft rows, which were intentionally absent from the placed table.
                current.AddRange(originals.Where(z=>!z.Use));spec.Rows=current;spec.Groups=groups;spec.SplitAt=groups==2?firstGroup:0;spec.Width=t.Width*factor/spec.DrawingFactor;spec.SymbolWidth=t.Columns[0].Width*factor/spec.DrawingFactor;spec.Title=Plain(t.Cells[0,0].TextString);spec.Notes=note?Plain(t.Cells[t.Rows.Count-1,0].TextString):"";return spec;
            }
        }
        public static ObjectId Write(Database db,LegendSpec original,Point3d position,Vector3d normal,Vector3d direction,ObjectId replace,bool injectFailure=false)
        {
            var spec=LegendSpec.Parse(original.Serialize());if(spec.DatabaseKey!=DatabaseKey(db))throw new InvalidOperationException("Mẫu thuộc DWG khác; chọn lại mẫu trong DWG hiện tại.");var layout=LegendLayout.Build(spec);if(layout.Items.Count==0)throw new InvalidOperationException("Chọn ít nhất một dòng để chèn bảng.");var previous=HostApplicationServices.WorkingDatabase;try{HostApplicationServices.WorkingDatabase=db;using(var tr=db.TransactionManager.StartTransaction())
            {
                Entity oldEntity=null;Table old=null;BlockReference oldWrapper=null;double oldScale=1;
                if(!replace.IsNull){old=LegendTable(tr,replace,out oldWrapper,out oldScale);oldEntity=(Entity)tr.GetObject(replace,OpenMode.ForWrite);if(((LayerTableRecord)tr.GetObject(oldEntity.LayerId,OpenMode.ForRead)).IsLocked)throw new InvalidOperationException("Layer của bảng đang khóa.");}
                var space=(BlockTableRecord)tr.GetObject(oldEntity==null?db.CurrentSpaceId:oldEntity.OwnerId,OpenMode.ForWrite);if(!space.IsLayout)throw new InvalidOperationException("Chỉ sửa bảng ở Model hoặc Layout.");
                var legendLayer=EnsureLegendLayer(db,tr);
                if(oldWrapper!=null){if(oldWrapper.IsDynamicBlock)throw new InvalidOperationException("Bảng nằm trong block động; không tự chuyển.");var def=(BlockTableRecord)tr.GetObject(oldWrapper.BlockTableRecord,OpenMode.ForRead);var mask=FindBlockMask(old,def,tr);var parts=def.Cast<ObjectId>().ToArray();if(parts.Any(id=>id!=old.ObjectId&&(mask==null||id!=mask.ObjectId)))throw new InvalidOperationException("Block cũ có thêm thành phần hoặc vùng che chưa xác định; không tự tách để tránh mất dữ liệu.");}
                else if(old!=null)EraseOldMask(old,tr);
                var title=FontStyle(db,tr,spec.TitleFont,spec.TitleBold);var body=FontStyle(db,tr,spec.BodyFont,spec.BodyBold);ObjectId tableStyleId;
                if(ReuseTableStyle(old,tr,body,spec))tableStyleId=old.TableStyle;
                else{var style=new TableStyle{IsTitleSuppressed=false,IsHeaderSuppressed=false};var styleDict=(DBDictionary)tr.GetObject(db.TableStyleDictionaryId,OpenMode.ForRead);string styleName;do{styleName="Mr.T_LG_TS_"+Guid.NewGuid().ToString("N").Substring(0,8);}while(styleDict.Contains(styleName));style.PostTableStyleToDatabase(db,styleName);tr.AddNewlyCreatedDBObject(style,true);style.SetTextStyle(body,7);style.SetTextHeight(spec.BodyHeight*spec.DrawingFactor,7);style.SetColor(AcColor.FromColorIndex(ColorMethod.ByBlock,0),7);style.SetGridColor(AcColor.FromColorIndex(ColorMethod.ByBlock,0),63,7);style.HorizontalCellMargin=2*spec.DrawingFactor;style.VerticalCellMargin=1*spec.DrawingFactor;tableStyleId=style.ObjectId;}
                int columns=spec.Groups*2,rows=layout.DataRows+2+(layout.NotesHeight>0?1:0);Table t;
                if(old!=null&&oldWrapper==null){t=old;ResetTable(t,rows,columns);t.TableStyle=tableStyleId;}
                else{t=new Table();t.SetDatabaseDefaults(db);t.TableStyle=tableStyleId;t.SetSize(rows,columns);if(oldWrapper==null){t.Position=position;t.Normal=normal;t.Direction=direction;t.LayerId=legendLayer;t.Color=AcColor.FromColorIndex(ColorMethod.ByLayer,256);}else{var m=oldWrapper.BlockTransform;t.Position=old.Position.TransformBy(m);t.Normal=old.Normal.TransformBy(m).GetNormal();t.Direction=old.Direction.TransformBy(m).GetNormal();t.LayerId=oldWrapper.LayerId;t.Color=oldWrapper.Color;}t.LinetypeId=db.ContinuousLinetype;space.AppendEntity(t);tr.AddNewlyCreatedDBObject(t,true);}t.SetRowHeight(spec.RowHeight*spec.DrawingFactor);
                for(int c=0;c<columns;c++)t.Columns[c].Width=(c%2==0?spec.SymbolWidth:spec.Width/spec.Groups-spec.SymbolWidth)*spec.DrawingFactor;
                t.MergeCells(CellRange.Create(t,0,0,0,columns-1));t.SetTextString(0,0,Literal(spec.Title));t.SetTextStyle(0,0,title);t.SetTextHeight(0,0,spec.TitleHeight*spec.DrawingFactor);t.SetAlignment(0,0,CellAlignment.MiddleCenter);t.Rows[0].Height=layout.TitleHeight*spec.DrawingFactor;t.Rows[1].Height=layout.HeaderHeight*spec.DrawingFactor;
                for(int c=0;c<columns;c++){t.SetTextString(1,c,c%2==0?"KÝ HIỆU":"DIỄN GIẢI");t.SetAlignment(1,c,CellAlignment.MiddleCenter);}
                for(int r=0;r<layout.DataRows;r++){t.Rows[r+2].Height=layout.Heights[r]*spec.DrawingFactor;for(int c=0;c<columns;c++)t.SetAlignment(r+2,c,c%2==0?CellAlignment.MiddleCenter:CellAlignment.MiddleLeft);}
                var types=new Dictionary<ObjectId,ObjectId>();foreach(var slot in layout.Slots){var row=layout.Items[slot.Index];int r=slot.Row+2,c=slot.Column;t.Cells[r,c].ToolTip="MRTLG:"+row.Id;t.SetTextString(r,c+1,Literal(row.Description));var id=Sample(db,row,spec,tr,types,legendLayer);if(!id.IsNull){t.SetBlockTableRecordId(r,c,id,true);t.SetBlockRotation(r,c,row.Rotation*Math.PI/180);var content=t.Cells[r,c].Contents[0];content.IsAutoScale=true;}}
                if(layout.NotesHeight>0){int r=rows-1;t.Cells[r,0].ToolTip="MRTLG_NOTES";t.MergeCells(CellRange.Create(t,r,0,r,columns-1));t.SetTextString(r,0,Literal(spec.Notes));t.SetAlignment(r,0,CellAlignment.MiddleLeft);t.Rows[r].Height=layout.NotesHeight*spec.DrawingFactor;}
                t.GenerateLayout();foreach(var slot in layout.Slots){var row=layout.Items[slot.Index];if(!String.IsNullOrEmpty(row.SampleBlock)&&Math.Abs(row.SampleScale-1)>.00001){var content=t.Cells[slot.Row+2,slot.Column].Contents[0];content.IsAutoScale=false;content.Scale=content.Scale*row.SampleScale;}}
                t.GenerateLayout();spec.NativeWidth=t.Width;spec.NativeSymbol=t.Columns[0].Width;spec.NativeTitle=t.Cells[0,0].Contents[0].TextHeight;spec.NativeBody=t.Cells[1,1].Contents[0].TextHeight;spec.NativeHeader=t.Rows[1].Height;Save(t,tr,spec);
                AddMask(db,t,space,tr);if(injectFailure)throw new InvalidOperationException("Injected legend rollback.");if(oldWrapper!=null)oldEntity.Erase();var result=t.ObjectId;tr.Commit();original.BlockName=spec.BlockName;for(int i=0;i<spec.Rows.Count;i++){original.Rows[i].SampleBlock=spec.Rows[i].SampleBlock;original.Rows[i].PipeBaseline=spec.Rows[i].PipeBaseline;original.Rows[i].PipeBaseScale=spec.Rows[i].PipeBaseScale;original.Rows[i].PipeBaseWidth=spec.Rows[i].PipeBaseWidth;}return result;
            }}finally{HostApplicationServices.WorkingDatabase=previous;}
        }
    }
}
