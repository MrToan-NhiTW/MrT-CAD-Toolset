using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using MedGas2024.Core;

namespace MedGas2024.Engine
{
    public sealed class DensityPlan
    {
        public DensityOptions Options;public string Signature;public BatchEdit Edit;public BatchPlan Batch;public List<DensityRow> Rows=new List<DensityRow>();public Dictionary<string,string> Checks=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);public double GlobalScale;
        public List<DensityOverride> Overrides=new List<DensityOverride>();
        public int Ready{get{return (Batch==null?0:Batch.Items.Count)+Overrides.Count;}}
    }
    public sealed class DensityOverride{public DensityRow Row;public string NewName;public double Dash;public List<string> Handles;public bool FromLayer;}
    public static class DensityService
    {
        static bool Curve(Entity e){return e is Line||e is Polyline||e is Polyline2d||e is Polyline3d||e is Arc||e is Spline;}
        static string N(double n){return n.ToString("0.#####",CultureInfo.InvariantCulture);}
        static DensityRow OverrideGroup(Dictionary<string,DensityRow> groups,Dictionary<string,List<string>> states,DensityRow parent,LinetypeTableRecord type,Transaction tr)
        {
            string key=parent.Layer+"|OVR|"+type.Handle;DensityRow row;if(!groups.TryGetValue(key,out row)){row=new DensityRow{Key=key,IsOverride=true,Layer=parent.Layer,Type=type.Name,Source=parent.Source};try{row.PeriodCompatible=InspectType(type,tr,row);}catch(Autodesk.AutoCAD.Runtime.Exception){row.TypeHandle=type.Handle.ToString();row.TypeFingerprint="unsupported:"+type.Handle;row.PeriodCompatible=false;}groups.Add(key,row);states.Add(key,new List<string>());}return row;
        }
        static bool InspectType(LinetypeTableRecord type,Transaction tr,DensityRow row)
        {
            row.TypeHandle=type.Handle.ToString();var s=new System.Text.StringBuilder(type.Handle+"|"+type.PatternLength.ToString("R",CultureInfo.InvariantCulture)+"|"+type.NumDashes+"|"+type.IsScaledToFit);
            for(int i=0;i<type.NumDashes;i++){s.Append('|').Append(type.DashLengthAt(i).ToString("R",CultureInfo.InvariantCulture));var sid=type.ShapeStyleAt(i);s.Append('|').Append(sid.IsNull?"none":sid.Handle.ToString());if(!sid.IsNull){s.Append('|').Append(type.ShapeNumberAt(i)).Append('|').Append(type.ShapeScaleAt(i)).Append('|').Append(type.ShapeOffsetAt(i)).Append('|').Append(type.ShapeRotationAt(i)).Append('|').Append(type.ShapeIsUcsOrientedAt(i));if(type.ShapeNumberAt(i)==0)s.Append('|').Append(type.TextAt(i));var st=(TextStyleTableRecord)tr.GetObject(sid,OpenMode.ForRead);s.Append('|').Append(st.FileName).Append('|').Append(st.TextSize).Append('|').Append(st.XScale).Append('|').Append(st.ObliquingAngle).Append('|').Append(st.IsVertical).Append('|').Append(st.Font.TypeFace).Append('|').Append(st.Font.Bold).Append('|').Append(st.Font.Italic);}}
            row.TypeFingerprint=Profile.Hash(s.ToString());
            if(type.NumDashes!=3||type.IsScaledToFit||type.DashLengthAt(0)<=0||type.DashLengthAt(1)>=0||type.DashLengthAt(2)>=0||!type.ShapeStyleAt(0).IsNull||type.ShapeStyleAt(1).IsNull||!type.ShapeStyleAt(2).IsNull||type.ShapeNumberAt(1)!=0||type.ShapeIsUcsOrientedAt(1)||Math.Abs(type.ShapeRotationAt(1))>1e-10||type.ShapeScaleAt(1)<=0)return false;
            var style=(TextStyleTableRecord)tr.GetObject(type.ShapeStyleAt(1),OpenMode.ForRead);if(style.IsVertical||Math.Abs(style.TextSize)>1e-10)return false;
            row.Text=type.TextAt(1);if(String.IsNullOrEmpty(row.Text))return false;row.Dash=type.DashLengthAt(0);row.Gap=-type.DashLengthAt(1)-type.DashLengthAt(2);row.Period=row.Dash+row.Gap;
            row.PeriodCompatible=Profile.Finite(type.PatternLength)&&type.PatternLength>0&&Math.Abs(type.PatternLength-row.Period)<=Math.Max(1e-9,type.PatternLength*1e-8);return row.PeriodCompatible;
        }
        public static List<DensityRow> Scan(Database db)
        {
            var source=DrawingService.Read(db);
            var rows=source.ToDictionary(x=>x.LayerName,x=>new DensityRow{Key=x.LayerName+"|BASE",Layer=x.LayerName,Type=x.LinetypeName,Text=x.Text,Period=x.Dash+x.Before+x.After,Dash=x.Dash,Gap=x.Before+x.After,Source=x},StringComparer.OrdinalIgnoreCase);var overrides=new Dictionary<string,DensityRow>(StringComparer.OrdinalIgnoreCase);var overrideStates=new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);
            var states=rows.Keys.ToDictionary(k=>k,k=>new List<string>(),StringComparer.OrdinalIgnoreCase);
            using(var tr=db.TransactionManager.StartOpenCloseTransaction()){
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);foreach(var r in rows.Values){var layer=(LayerTableRecord)tr.GetObject(layers[r.Layer],OpenMode.ForRead);var type=(LinetypeTableRecord)tr.GetObject(layer.LinetypeObjectId,OpenMode.ForRead);r.TypeHandle=type.Handle.ToString();r.PeriodCompatible=Profile.Finite(type.PatternLength)&&type.PatternLength>0&&Math.Abs(type.PatternLength-r.Period)<=Math.Max(1e-9,type.PatternLength*1e-8);}
                var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var model=blocks[BlockTableRecord.ModelSpace];
                foreach(ObjectId bid in blocks){var b=(BlockTableRecord)tr.GetObject(bid,OpenMode.ForRead);if(b.IsFromExternalReference||b.IsFromOverlayReference)continue;
                    foreach(ObjectId id in b){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)continue;DensityRow row;if(!rows.TryGetValue(e.Layer,out row))continue;
                        string state=id.Handle+"|"+e.LinetypeId.Handle+"|"+e.LinetypeScale.ToString("R",CultureInfo.InvariantCulture)+"|"+bid.Handle+"|"+e.GetType().Name;states[row.Layer].Add(state);
                        if(!Curve(e)){row.NonCurve++;continue;}var layerType=((LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead)).LinetypeObjectId;bool other=e.LinetypeId!=db.ByLayerLinetype&&e.LinetypeId!=layerType;
                        if(!b.IsLayout){row.Nested++;if(other&&e.LinetypeId!=db.ByBlockLinetype){var nestedType=tr.GetObject(e.LinetypeId,OpenMode.ForRead) as LinetypeTableRecord;if(nestedType!=null&&DensityMath.IsMrT(nestedType.Name)){var group=OverrideGroup(overrides,overrideStates,row,nestedType,tr);group.Nested++;overrideStates[group.Key].Add(state);}}continue;}
                        if(other){row.OtherOverride++;if(e.LinetypeId!=db.ByBlockLinetype){var type=tr.GetObject(e.LinetypeId,OpenMode.ForRead) as LinetypeTableRecord;if(type!=null&&DensityMath.IsMrT(type.Name)){row.MrTOverride++;var group=OverrideGroup(overrides,overrideStates,row,type,tr);group.Direct++;group.MatchingOverride++;if(bid!=model)group.Layout++;group.MinScale=Math.Min(group.MinScale,e.LinetypeScale);group.MaxScale=Math.Max(group.MaxScale,e.LinetypeScale);group.Handles.Add(id.Handle.ToString());group.Members.Add(new DensityMember{Handle=id.Handle.ToString(),Scale=e.LinetypeScale});overrideStates[group.Key].Add(state);}}continue;}
                        row.Direct++;if(e.LinetypeId!=db.ByLayerLinetype)row.MatchingOverride++;if(bid!=model)row.Layout++;
                        row.MinScale=Math.Min(row.MinScale,e.LinetypeScale);row.MaxScale=Math.Max(row.MaxScale,e.LinetypeScale);row.Members.Add(new DensityMember{Handle=id.Handle.ToString(),Scale=e.LinetypeScale});
                    }}
            }
            foreach(var r in rows.Values){r.Fingerprint=Profile.Hash(r.Source.Fingerprint+"|"+r.PeriodCompatible+"|"+String.Join(";",states[r.Layer].OrderBy(x=>x,StringComparer.Ordinal)));r.State=!DensityMath.IsMrT(r.Type)?"Bỏ: linetype không mang tiền tố Mr.T":!r.Source.Editable?"Bỏ: cấu trúc nét chưa hỗ trợ":!r.PeriodCompatible?"Bỏ: chu kỳ nét không khớp các đoạn":!r.Source.Assignable?"Bỏ: layer khóa/Xref":r.Direct==0?"Bỏ: không có đường trực tiếp khớp nét layer":"Có thể xem trước";r.Selected=r.State=="Có thể xem trước";}
            foreach(var r in overrides.Values){r.Fingerprint=Profile.Hash(r.Source.Fingerprint+"|"+r.TypeFingerprint+"|"+String.Join(";",overrideStates[r.Key].OrderBy(x=>x,StringComparer.Ordinal)));r.State=!r.Source.Assignable?"Bỏ: layer khóa/Xref":!r.PeriodCompatible?"Bỏ: cấu trúc nét chưa hỗ trợ":r.Direct==0?"Bỏ: chỉ có trong block dùng chung":"Có thể xem trước";r.Selected=r.State=="Có thể xem trước";}
            return rows.Values.Where(x=>DensityMath.IsMrT(x.Layer)||DensityMath.IsMrT(x.Type)).Concat(overrides.Values).OrderBy(x=>x.Layer,StringComparer.CurrentCultureIgnoreCase).ThenBy(x=>x.IsOverride).ThenBy(x=>x.Type,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        public static DensityPlan Preview(Database db,IEnumerable<string> selected,DensityOptions options)
        {
            options.Validate();var names=new HashSet<string>(selected,StringComparer.OrdinalIgnoreCase);if(names.Count==0)throw new ArgumentException("Tích ít nhất một layer Mr.T.");
            var all=Scan(db);var chosen=all.Where(x=>names.Contains(x.Key)).ToList();if(chosen.Count!=names.Count)throw new InvalidOperationException("Danh sách layer/nét đã thay đổi; đóng và mở lại công cụ.");
            var result=new DensityPlan{Options=options,Signature=options.Signature,GlobalScale=db.Ltscale};var ready=new List<DrawingLayerInfo>();var scaleGroups=new Dictionary<string,List<DensityScaleGroup>>(StringComparer.OrdinalIgnoreCase);
            foreach(var row in chosen){result.Checks[row.Key]=row.Fingerprint;row.Before=row.Period>0?N(row.Period):"—";
                try{if(row.State!="Có thể xem trước")throw new ArgumentException(row.State);var groups=options.PaperMode?DensityMath.ScaleGroups(row.Members):new List<DensityScaleGroup>{new DensityScaleGroup{Scale=row.MinScale,Handles=row.Members.Select(x=>x.Handle).ToList()}};if(groups.Count==0)throw new ArgumentException("Bỏ: không có đường trực tiếp để xử lý.");
                    var dashes=groups.Select(g=>DensityMath.DashFor(options,row.Period,row.Gap,g.Scale,result.GlobalScale)).ToList();scaleGroups[row.Key]=groups;double dash=dashes[0],period=dash+row.Gap;
                    row.After=groups.Count==1?N(period):"Theo scale ("+groups.Count+")";row.Result="Nét chính="+N(dash)+"; giữ chữ/khoảng chữ và scale từng đường. Khớp="+row.Direct+(row.IsOverride?"; chỉ sửa đường gán riêng trực tiếp; bỏ qua trong block="+row.Nested+".":", trong block="+row.Nested+", đối tượng khác="+row.NonCurve+"; sửa nét layer ảnh hưởng ByLayer.");
                    if(options.PaperMode){row.Result+=" Ước tính Model: "+N(DensityMath.EstimatePaper(row.Period,groups[0].Scale,result.GlobalScale,options.Denominator))+" → "+N(options.PaperMm)+" mm giấy.";if(groups.Count>1)row.Result+=" Scale khác nhau: "+String.Join(", ",groups.Select(g=>N(g.Scale)+" ("+g.Handles.Count+" đường)"))+(row.IsOverride?". Mỗi scale được gán nét riêng; giữ nguyên cỡ chữ.":". Nhóm scale phổ biến dùng nét layer; các scale còn lại gán nét riêng để giữ nguyên cỡ chữ.");}
                    if(!row.IsOverride)ready.Add(row.Source);
                }catch(ArgumentException ex){row.After="—";row.Result=ex.Message;}result.Rows.Add(row);
            }
            if(ready.Count>0){result.Edit=new BatchEdit{ChangeDash=true,Dash=1,RebindOldOverrides=true};result.Batch=BatchService.Inspect(db,ready,result.Edit);}
            using(var tr=db.TransactionManager.StartOpenCloseTransaction()){var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if(result.Batch!=null){foreach(var item in result.Batch.Items){var r=result.Rows.Single(x=>!x.IsOverride&&String.Equals(x.Layer,item.Expected.LayerName,StringComparison.OrdinalIgnoreCase));if(item.Preview.Blocked){r.Result="Bỏ: "+item.Preview.Detail;r.After="—";continue;}var groups=scaleGroups[r.Key];string name=Fresh(types,used,r);item.Preview.TypeAfter=name;item.Dash=DensityMath.DashFor(options,r.Period,r.Gap,groups[0].Scale,result.GlobalScale);r.Result+=" Nét layer: "+name+".";foreach(var g in groups.Skip(1)){var variant=Fresh(types,used,r);result.Overrides.Add(new DensityOverride{Row=r,NewName=variant,Dash=DensityMath.DashFor(options,r.Period,r.Gap,g.Scale,result.GlobalScale),Handles=g.Handles,FromLayer=true});r.Result+=" Scale "+N(g.Scale)+": "+g.Handles.Count+" đường gán nét "+variant+".";}item.Preview.AfterState="Chu kỳ "+N(r.Period)+" → "+N(item.Dash+r.Gap)+"; "+r.Result;}result.Batch.Items.RemoveAll(item=>item.Preview.Blocked);}
                foreach(var r in result.Rows.Where(x=>x.IsOverride&&x.After!="—")){foreach(var g in scaleGroups[r.Key]){string name=Fresh(types,used,r);result.Overrides.Add(new DensityOverride{Row=r,NewName=name,Dash=DensityMath.DashFor(options,r.Period,r.Gap,g.Scale,result.GlobalScale),Handles=g.Handles});r.Result+=" Scale "+N(g.Scale)+": "+g.Handles.Count+" đường gán nét "+name+".";}}
            }return result;
        }
        static string Fresh(LinetypeTable types,HashSet<string> used,DensityRow row){string stem=DensityMath.ReadableStem(row.Layer,row.Type,row.Text,row.IsOverride);for(int i=1;i<1000000;i++){string name=stem+"_SP_"+i.ToString(CultureInfo.InvariantCulture);if(!types.Has(name)&&used.Add(name))return name;}throw new InvalidOperationException("Không còn tên nét mật độ khả dụng.");}
        public static int Apply(Database db,DensityPlan plan,DensityOptions current,IEnumerable<string> names)
        {
            if(plan==null||plan.Ready==0||plan.Signature!=current.Signature)throw new InvalidOperationException("Cần xem trước lại sau khi thay đổi thông số.");
            var set=new HashSet<string>(names,StringComparer.OrdinalIgnoreCase);if(set.Count!=plan.Checks.Count||plan.Checks.Keys.Any(x=>!set.Contains(x)))throw new InvalidOperationException("Lựa chọn đã đổi; xem trước lại.");
            if(Math.Abs(db.Ltscale-plan.GlobalScale)>1e-10)throw new InvalidOperationException("LTSCALE đã đổi; xem trước lại.");var now=Scan(db).ToDictionary(x=>x.Key,StringComparer.OrdinalIgnoreCase);
            foreach(var pair in plan.Checks){DensityRow row;if(!now.TryGetValue(pair.Key,out row)||row.Fingerprint!=pair.Value)throw new InvalidOperationException("DWG/scale đã đổi sau xem trước: "+pair.Key+"; hãy xem trước lại.");}
            using(var outer=db.TransactionManager.StartTransaction()){
                int count=0;if(plan.Batch!=null&&plan.Batch.Items.Count>0)count=BatchService.Apply(db,plan.Batch,plan.Edit);var countedOverrides=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var types=(LinetypeTable)outer.GetObject(db.LinetypeTableId,OpenMode.ForWrite);
                foreach(var item in plan.Overrides){if(types.Has(item.NewName))throw new InvalidOperationException("Tên nét mới vừa bị chiếm; xem trước lại.");var oldId=db.GetObjectId(false,new Handle(Int64.Parse(item.Row.TypeHandle,NumberStyles.HexNumber,CultureInfo.InvariantCulture)),0);var original=(LinetypeTableRecord)outer.GetObject(oldId,OpenMode.ForRead);var next=(LinetypeTableRecord)original.Clone();next.Name=item.NewName;types.Add(next);outer.AddNewlyCreatedDBObject(next,true);next.SetDashLengthAt(0,item.Dash);next.PatternLength=item.Dash+item.Row.Gap;
                    foreach(var handle in item.Handles){var id=db.GetObjectId(false,new Handle(Int64.Parse(handle,NumberStyles.HexNumber,CultureInfo.InvariantCulture)),0);var entity=outer.GetObject(id,OpenMode.ForWrite) as Entity;if(entity==null||!String.Equals(entity.Layer,item.Row.Layer,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Đường gán nét đã thay đổi; xem trước lại.");var layer=(LayerTableRecord)outer.GetObject(entity.LayerId,OpenMode.ForRead);bool valid=item.FromLayer?(entity.LinetypeId==db.ByLayerLinetype||entity.LinetypeId==oldId||entity.LinetypeId==layer.LinetypeObjectId):entity.LinetypeId==oldId;if(!valid)throw new InvalidOperationException("Nét của đường đã thay đổi; xem trước lại.");entity.LinetypeId=next.ObjectId;}if(!item.FromLayer&&countedOverrides.Add(item.Row.Key))count++;}
                outer.Commit();return count;
            }
        }
    }
}
