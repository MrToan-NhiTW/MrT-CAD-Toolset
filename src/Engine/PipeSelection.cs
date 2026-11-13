using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using MedGas2024.Core;
namespace MedGas2024.Engine
{
    public sealed class PipePick
    {
        public ObjectId[] Ids=new ObjectId[0];public int Matched,OtherSpaces,Locked,FamilySeeds,OtherSeeds;public string Scope;
        public override string ToString(){return "Đã pick "+Ids.Length+" đường. "+Scope+". Khớp toàn DWG: "+Matched+"; khác không gian hiện hành: "+OtherSpaces+"; thuộc Layer khóa: "+Locked+". Chỉ tạo lựa chọn, không đổi thuộc tính.";}
    }
    public static class PipeSelection
    {
        static bool Curve(Entity e){return e is Line||e is Polyline||e is Polyline2d||e is Polyline3d||e is Arc||e is Spline;}
        static ObjectId Effective(Database db,Entity e,Transaction tr){ObjectId id=DirectionService.NativeSource(e,db,tr);return id==db.ByLayerLinetype?((LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead)).LinetypeObjectId:id;}
        static ObjectId Id(Database db,string handle){long number;if(!Int64.TryParse(handle,System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out number))throw new InvalidOperationException("Handle không hợp lệ.");return db.GetObjectId(false,new Handle(number),0);}
        public static PipePick FromLabels(Database db,LabelScope scope,ObjectId currentSpace)
        {
            var catalog=LabelService.Read(db);if(scope.Fingerprint!=null&&catalog.Fingerprint!=scope.Fingerprint)throw new InvalidOperationException("Bản vẽ đã đổi; Đọc lại trước khi pick.");var matched=LabelRules.Select(catalog,scope);var result=new PipePick{Matched=matched.Count,Scope="Các cụm đã chọn, lọc tiền tố='"+(scope.LayerPrefix??"")+"', hậu tố='"+(scope.LayerSuffix??"")+"'"};var ids=new List<ObjectId>();
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())foreach(var member in matched){var id=Id(db,member.Handle);if(id.IsErased)continue;var e=(Entity)tr.GetObject(id,OpenMode.ForRead);if(e.OwnerId!=currentSpace){result.OtherSpaces++;continue;}ids.Add(id);if(((LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead)).IsLocked)result.Locked++;}result.Ids=ids.Distinct().ToArray();return result;
        }
        public static ObjectId[] InSpace(Database db,IEnumerable<ObjectId> ids,ObjectId currentSpace)
        {
            var result=new List<ObjectId>();using(var tr=db.TransactionManager.StartOpenCloseTransaction())foreach(var id in ids.Distinct()){if(id.IsNull||!id.IsValid||id.IsErased||id.Database.UnmanagedObject!=db.UnmanagedObject)continue;var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e!=null&&e.OwnerId==currentSpace)result.Add(id);}return result.ToArray();
        }
        public static PipePick FromSources(Database db,IEnumerable<ObjectId> samples,ObjectId currentSpace)
        {
            var catalog=LabelService.Read(db);var byHandle=catalog.Members.ToDictionary(m=>m.Handle,StringComparer.OrdinalIgnoreCase);var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var fallback=new HashSet<Tuple<ObjectId,ObjectId>>();var matched=new HashSet<ObjectId>();var result=new PipePick();
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                foreach(var id in samples.Distinct())
                {
                    if(id.IsNull||!id.IsValid||id.IsErased||id.Database.UnmanagedObject!=db.UnmanagedObject){result.OtherSeeds++;continue;}var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||!Curve(e)){result.OtherSeeds++;continue;}var space=tr.GetObject(e.OwnerId,OpenMode.ForRead) as BlockTableRecord;var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);if(space==null||!space.IsLayout||space.IsFromExternalReference||space.IsFromOverlayReference||layer.IsDependent||DrawingSearch.Preview(layer.Name)){result.OtherSeeds++;continue;}
                    LabelMember member;if(byHandle.TryGetValue(id.Handle.ToString(),out member)){keys.Add(LabelRules.Key(member.Text,true));result.FamilySeeds++;}else{var type=Effective(db,e,tr);if(type.IsNull||!type.IsValid||type.IsErased){result.OtherSeeds++;continue;}fallback.Add(Tuple.Create(e.LayerId,type));result.FamilySeeds++;}
                }
                foreach(var member in catalog.Members)if(keys.Contains(LabelRules.Key(member.Text,true)))matched.Add(Id(db,member.Handle));
                if(fallback.Count>0){var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);foreach(ObjectId blockId in blocks){var space=(BlockTableRecord)tr.GetObject(blockId,OpenMode.ForRead);if(!space.IsLayout||space.IsFromExternalReference||space.IsFromOverlayReference)continue;foreach(ObjectId id in space){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e!=null&&Curve(e)&&fallback.Any(pair=>pair.Item1==e.LayerId)&&fallback.Contains(Tuple.Create(e.LayerId,Effective(db,e,tr))))matched.Add(id);}}}
                result.Matched=matched.Count;var active=new List<ObjectId>();foreach(var id in matched.OrderBy(id=>id.Handle.Value)){var e=(Entity)tr.GetObject(id,OpenMode.ForRead);if(e.OwnerId!=currentSpace){result.OtherSpaces++;continue;}active.Add(id);if(((LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead)).IsLocked)result.Locked++;}result.Ids=active.ToArray();
            }
            result.Scope=result.FamilySeeds+" mẫu đường cho Cùng hệ; "+result.OtherSeeds+" mẫu khác chỉ dùng Select Similar. Nhãn: mọi cỡ; nét không chữ: cùng Layer + nét thực tế";return result;
        }
        public static PipePick FromSource(Database db,ObjectId source,ObjectId currentSpace)
        {
            if(source.IsNull||!source.IsValid||source.IsErased||source.Database.UnmanagedObject!=db.UnmanagedObject)throw new InvalidOperationException("Ống mẫu không hợp lệ.");
            var catalog=LabelService.Read(db);var member=catalog.Members.SingleOrDefault(m=>String.Equals(m.Handle,source.Handle.ToString(),StringComparison.OrdinalIgnoreCase));
            if(member!=null){var result=FromLabels(db,new LabelScope{SystemMode=true,Keys=new[]{LabelRules.Key(member.Text,true)},Fingerprint=catalog.Fingerprint},currentSpace);result.Scope="Cùng hệ '"+LabelRules.System(member.Text)+"', mọi cỡ, mọi tên Layer";return result;}
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var seed=tr.GetObject(source,OpenMode.ForRead) as Entity;if(seed==null||!Curve(seed))throw new InvalidOperationException("Chọn LINE/polyline/ARC/SPLINE trực tiếp.");var owner=tr.GetObject(seed.OwnerId,OpenMode.ForRead) as BlockTableRecord;var layer=(LayerTableRecord)tr.GetObject(seed.LayerId,OpenMode.ForRead);if(owner==null||!owner.IsLayout||layer.IsDependent||DrawingSearch.Preview(layer.Name))throw new InvalidOperationException("Không chọn trong block/Xref/đường mẫu.");ObjectId type=Effective(db,seed,tr);if(type.IsNull||!type.IsValid||type.IsErased)throw new InvalidOperationException("Nét nguồn không còn hợp lệ.");
                var result=new PipePick{Scope="Không có nhãn chữ: chỉ cùng Layer '"+layer.Name+"' và cùng linetype thực tế; không suy tên hệ"};var ids=new List<ObjectId>();var blocks=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
                foreach(ObjectId b in blocks){var space=(BlockTableRecord)tr.GetObject(b,OpenMode.ForRead);if(!space.IsLayout||space.IsFromExternalReference||space.IsFromOverlayReference)continue;foreach(ObjectId id in space){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||!Curve(e)||e.LayerId!=seed.LayerId||Effective(db,e,tr)!=type)continue;result.Matched++;if(b!=currentSpace){result.OtherSpaces++;continue;}ids.Add(id);if(layer.IsLocked)result.Locked++;}}
                result.Ids=ids.ToArray();return result;
            }
        }
    }
}
