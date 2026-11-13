using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using MedGas2024.Core;
namespace MedGas2024.Engine
{
    public sealed class NativeDirectionItem { public ObjectId Id; public int Sign; public string State; public int[] SegmentSigns; public bool SplitEachSegment; }
    public sealed class NativeDirectionPlan
    {
        public readonly List<NativeDirectionItem> Items=new List<NativeDirectionItem>();
        public readonly List<string> Skipped=new List<string>();
        public int Ignored;
        public string Summary { get { return "Đã đúng: "+Items.Count(x=>x.Sign>0)+"; cần đảo: "+Items.Count(x=>x.Sign<0)+"; cần tách: "+Items.Count(x=>x.Sign==0)+"; đường bỏ qua: "+Skipped.Count+"; đối tượng không phải đường: "+Ignored+".\r\nTheo WCS: trái → phải; gần thẳng đứng: trên → dưới. Chỉ nhận nét R=0 xác nhận bằng cờ native.\r\nTách thay một polyline bằng nhiều polyline, thay handle và số đối tượng. Polyline bật linetype generation được tách từng đoạn và tắt generation trên các đoạn mới, nên nhịp nét có thể đổi. Giữ nét native và độ rộng, không tạo MText. Đường cong/kín và dữ liệu liên kết chưa hỗ trợ không tự tách.\r\n"+String.Join("\r\n",Skipped.Take(15)); } }
    }
    public static class NativeDirectionService
    {
        // ReverseCurve's width behavior may depend on PLINEREVERSEWIDTHS. Restore
        // segment widths explicitly, so tapered geometry remains in the same place.
        static void ReversePreservingWidth(Curve curve)
        {
            var p=curve as Polyline;if(p==null){curve.ReverseCurve();return;}
            int count=p.NumberOfVertices-1;var starts=new double[count];var ends=new double[count];
            bool variable=false;double first=p.GetStartWidthAt(0);
            for(int i=0;i<count;i++){starts[i]=p.GetStartWidthAt(i);ends[i]=p.GetEndWidthAt(i);if(Math.Abs(starts[i]-first)>1e-10||Math.Abs(ends[i]-first)>1e-10)variable=true;}
            curve.ReverseCurve();
            if(variable){double[] a,b;NativeDirectionRules.ReverseWidths(starts,ends,out a,out b);for(int i=0;i<count;i++){p.SetStartWidthAt(i,a[i]);p.SetEndWidthAt(i,b[i]);}}
        }
        static ObjectId TypeId(Database db,Transaction tr,ObjectId id)
        {
            var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)return ObjectId.Null;var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);return e.LinetypeId==db.ByLayerLinetype?layer.LinetypeObjectId:e.LinetypeId;
        }
        static NativeDirectionItem InspectOne(Database db,Transaction tr,ObjectId id,Dictionary<ObjectId,bool> relative)
        {
            var e=tr.GetObject(id,OpenMode.ForRead) as Entity;
            if(e==null||(!(e is Line)&&!(e is Polyline)))throw new InvalidOperationException("chỉ hỗ trợ LINE và polyline 2D.");
            var owner=tr.GetObject(e.OwnerId,OpenMode.ForRead) as BlockTableRecord;
            var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);
            if(owner==null||!owner.IsLayout||layer.IsDependent||layer.IsLocked)throw new InvalidOperationException("không gian hoặc Layer không thể sửa.");
            if(DirectionService.HasLinked(e,tr))throw new InvalidOperationException("đang có nhãn liên kết cũ.");
            ObjectId typeId=e.LinetypeId==db.ByLayerLinetype?layer.LinetypeObjectId:e.LinetypeId;
            if(typeId.IsNull||!typeId.IsValid||typeId.IsErased||typeId==db.ByBlockLinetype)throw new InvalidOperationException("không xác định nét thực tế.");
            var type=(LinetypeTableRecord)tr.GetObject(typeId,OpenMode.ForRead);
            bool isR;var definition=LabelService.Definition(type,tr);
            if(!relative.TryGetValue(typeId,out isR))throw new InvalidOperationException("không đọc được cờ xoay của linetype "+type.Name+".");
            if(!isR)throw new InvalidOperationException("linetype "+type.Name+" không phải một cụm chữ R; nét U/A hoặc nhiều chữ không tự đảo.");
            int textIndex=-1;
            for(int i=0;i<type.NumDashes;i++)if(!type.ShapeStyleAt(i).IsNull){if(type.ShapeNumberAt(i)!=0||textIndex>=0)throw new InvalidOperationException("linetype có shape hoặc nhiều cụm chữ chưa hỗ trợ.");textIndex=i;}
            if(textIndex<0||type.ShapeIsUcsOrientedAt(textIndex)||Math.Abs(type.ShapeRotationAt(textIndex))>1e-10)throw new InvalidOperationException("chữ trong linetype không có góc quay R=0.");
            var textStyle=(TextStyleTableRecord)tr.GetObject(type.ShapeStyleAt(textIndex),OpenMode.ForRead);if(textStyle.IsVertical)throw new InvalidOperationException("style chữ dọc chưa hỗ trợ.");
            var state=new StringBuilder().Append(e.LayerId.Handle).Append('|').Append(typeId.Handle).Append('|').Append(LabelService.Definition(type,tr).Fingerprint);
            int sign=0; bool splitEach=false;var signs=new List<int>();var line=e as Line;
            if(line!=null){var a=line.StartPoint;var b=line.EndPoint;if(Math.Abs(a.Z-b.Z)>1e-8)throw new InvalidOperationException("đường không nằm trên mặt phẳng XY.");sign=NativeDirectionRules.Sign(b.X-a.X,b.Y-a.Y);state.Append('|').Append(a).Append('|').Append(b);}
            else
            {
                var p=(Polyline)e;if(p.Closed||p.NumberOfVertices<2||Math.Abs(p.Normal.X)>1e-9||Math.Abs(p.Normal.Y)>1e-9||p.Normal.Z<0)throw new InvalidOperationException("polyline kín hoặc không nằm trên mặt phẳng XY.");
                splitEach=ScaleService.GenerationOn(p)&&p.NumberOfVertices>2;state.Append("|generation=").Append(ScaleService.GenerationOn(p));
                for(int i=0;i<p.NumberOfVertices-1;i++)
                {
                    if(Math.Abs(p.GetBulgeAt(i))>1e-12)throw new InvalidOperationException("có đoạn cong.");
                    var a=p.GetPoint3dAt(i);var b=p.GetPoint3dAt(i+1);int part=NativeDirectionRules.Sign(b.X-a.X,b.Y-a.Y);
                    if(part==0)throw new InvalidOperationException("có đoạn độ dài bằng 0.");
                    signs.Add(part);if(sign==0)sign=part;state.Append('|').Append(a).Append('|').Append(b).Append('|').Append(p.GetStartWidthAt(i)).Append('|').Append(p.GetEndWidthAt(i));
                }
                if(signs.Distinct().Count()>1||splitEach){
                    using(var data=p.XData){if(data!=null){var v=data.AsArray();if(!JoinMetadataPolicy.IsSupported(v.Select(t=>(int)t.TypeCode).ToArray(),v.Select(t=>Convert.ToString(t.Value)).ToArray()))throw new InvalidOperationException("cần tách nhưng có XData chưa hỗ trợ; không tự tách.");state.Append("|xdata=").Append(String.Join(",",v.Select(t=>t.TypeCode+":"+Convert.ToString(t.Value)).ToArray()));}else state.Append("|xdata=none");}
                    if(!p.ExtensionDictionary.IsNull||p.GetPersistentReactorIds().Count>0)throw new InvalidOperationException("cần tách nhưng có dữ liệu mở rộng hoặc liên kết; không tự tách.");sign=0;
                }
            }
            if(sign==0&&signs.Count==0)throw new InvalidOperationException("không xác định hướng.");
            return new NativeDirectionItem{Id=id,Sign=sign,State=Profile.Hash(state.ToString()),SegmentSigns=signs.ToArray(),SplitEachSegment=splitEach};
        }
        public static NativeDirectionPlan Inspect(Database db,IEnumerable<ObjectId> ids)
        {
            var plan=new NativeDirectionPlan();var selected=ids.Where(id=>!id.IsNull&&id.IsValid&&!id.IsErased).Distinct().ToArray();var curves=new List<ObjectId>();using(var scan=db.TransactionManager.StartOpenCloseTransaction())foreach(var id in selected){try{var e=scan.GetObject(id,OpenMode.ForRead) as Entity;if(e is Curve)curves.Add(id);else plan.Ignored++;}catch(System.Exception){plan.Ignored++;}}var list=curves.ToArray();var types=new List<ObjectId>();using(var tr=db.TransactionManager.StartOpenCloseTransaction())foreach(var id in list){try{var t=TypeId(db,tr,id);if(!t.IsNull&&t!=db.ByBlockLinetype&&t!=db.ByLayerLinetype)types.Add(t);}catch(System.Exception){}}
            var relative=RelativeTypeProbe.Read(db,types);using(var tr=db.TransactionManager.StartOpenCloseTransaction())foreach(var id in list)
            {try{plan.Items.Add(InspectOne(db,tr,id,relative));}catch(System.Exception ex){plan.Skipped.Add(id.Handle+": "+ex.Message);}}return plan;
        }
        public static int Apply(Database db,NativeDirectionPlan plan,bool splitMixed=false,IDictionary<ObjectId,ObjectId[]> replacements=null)
        {
            var types=new List<ObjectId>();using(var scan=db.TransactionManager.StartOpenCloseTransaction())foreach(var item in plan.Items)types.Add(TypeId(db,scan,item.Id));var relative=RelativeTypeProbe.Read(db,types);
            var pending=new Dictionary<ObjectId,ObjectId[]>();int count=0;using(var tr=db.TransactionManager.StartTransaction())
            {
                foreach(var item in plan.Items){var fresh=InspectOne(db,tr,item.Id,relative);if(fresh.Sign!=item.Sign||fresh.State!=item.State)throw new InvalidOperationException("Đường đã thay đổi sau xem trước. Hãy kiểm tra lại.");}
                foreach(var item in plan.Items.Where(x=>x.Sign<0)){var curve=(Curve)tr.GetObject(item.Id,OpenMode.ForWrite);ReversePreservingWidth(curve);count++;}
                if(splitMixed)foreach(var item in plan.Items.Where(x=>x.Sign==0))
                {
                    var original=(Polyline)tr.GetObject(item.Id,OpenMode.ForWrite);var owner=(BlockTableRecord)tr.GetObject(original.OwnerId,OpenMode.ForWrite);
                    var made=new List<ObjectId>();
                    foreach(var run in NativeDirectionRules.Runs(item.SegmentSigns,item.SplitEachSegment))
                    {
                        var copy=(Polyline)original.Clone();bool added=false;
                        try
                        {
                            while(copy.NumberOfVertices>run.End+1)copy.RemoveVertexAt(copy.NumberOfVertices-1);
                            for(int j=0;j<run.Start;j++)copy.RemoveVertexAt(0);
                            if(item.SplitEachSegment)copy.Plinegen=false;
                            owner.AppendEntity(copy);tr.AddNewlyCreatedDBObject(copy,true);added=true;made.Add(copy.ObjectId);
                            // The supported ETag is copied to each resulting run; unknown XData
                            // and external links were rejected during inspection.
                            using(var data=original.XData){if(data!=null)using(var x=new ResultBuffer(data.AsArray()))copy.XData=x;}
                            if(run.Sign<0)ReversePreservingWidth(copy);
                        }
                        finally{if(!added)copy.Dispose();}
                    }
                    original.Erase();pending[item.Id]=made.ToArray();count++;
                }
                tr.Commit();
            }if(replacements!=null)foreach(var pair in pending)replacements[pair.Key]=pair.Value;return count;
        }
    }
}
