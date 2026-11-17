using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MedGas2024.Engine;
using MedGas2024.Core;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;
namespace MedGas2024.Plugin
{
    internal static class IsolationHost
    {
        sealed class Session { public ObjectId Space; public ObjectId[] Ids; public ObjectId[] HiddenBefore,VisibleBefore; public bool Context; public readonly Dictionary<ObjectId,ObjectId[]> Splits=new Dictionary<ObjectId,ObjectId[]>(); public readonly HashSet<ObjectId> Unresolved=new HashSet<ObjectId>(); }
        static readonly Dictionary<Document,Session> sessions=new Dictionary<Document,Session>();
        static readonly HashSet<Document> hooked=new HashSet<Document>(); static bool started,busy;
        public static void Start(){if(started)return;started=true;AcApp.DocumentManager.DocumentCreated+=Created;AcApp.DocumentManager.DocumentToBeDestroyed+=Closing;foreach(Document doc in AcApp.DocumentManager)Hook(doc);}
        public static void Stop(){if(!started)return;AcApp.DocumentManager.DocumentCreated-=Created;AcApp.DocumentManager.DocumentToBeDestroyed-=Closing;foreach(var doc in hooked)doc.CommandEnded-=Ended;hooked.Clear();sessions.Clear();started=false;}
        static void Hook(Document doc){if(!hooked.Add(doc))return;doc.CommandEnded+=Ended;try{if(IsolationLayers.HasPending(doc.Database))doc.Editor.WriteMessage("\nMr.T: Có trạng thái Layer bật tạm của phiên Isolate trước. Gõ MRTUNISO để khôi phục.\n");}catch(System.Exception){}}
        static void Created(object sender,DocumentCollectionEventArgs e){Hook(e.Document);}
        static void Closing(object sender,DocumentCollectionEventArgs e){sessions.Remove(e.Document);if(hooked.Remove(e.Document))e.Document.CommandEnded-=Ended;}
        // Editor.Regen only refreshes the active viewport. Layer thaw and Visible changes
        // must reach every viewport/layout, otherwise context shows stale, incomplete pipes.
        static void RegenAll(Document doc,bool commandContext=true)
        {
            if(commandContext){bool previous=busy;busy=true;try{doc.Editor.Command("_.REGENALL");return;}catch(System.Exception){}finally{busy=previous;}}
            try{doc.Editor.Regen();}catch(System.Exception){}
        }
        static void Restore(Document doc,bool commandContext=true,bool regen=true){int pending;using(var locked=doc.LockDocument())pending=IsolationLayers.Restore(doc.Database);if(regen)RegenAll(doc,commandContext);if(pending>0)doc.Editor.WriteMessage("\nMr.T: "+pending+" Layer chưa đóng băng lại vì đang là Layer hiện hành. Chuyển Layer khác rồi gõ MRTUNISO.\n");}
        static void Ended(object sender,CommandEventArgs e){if(busy||!String.Equals(e.GlobalCommandName.TrimStart('_','.'),"UNISOLATEOBJECTS",StringComparison.OrdinalIgnoreCase))return;var doc=sender as Document;if(doc==null)return;sessions.Remove(doc);try{Restore(doc,false);}catch(System.Exception ex){doc.Editor.WriteMessage("\nMr.T: Chưa khôi phục Layer Isolate: "+ex.Message+" Gõ MRTUNISO để thử lại.\n");}}
        static ObjectId[] Valid(Document doc,IEnumerable<ObjectId> source,ObjectId? space=null)
        {
            var ids=new List<ObjectId>();using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction())foreach(var id in (source??new ObjectId[0]).Distinct()){
                if(id.IsNull||!id.IsValid||id.IsErased||id.Database.UnmanagedObject!=doc.Database.UnmanagedObject)continue;
                var entity=tr.GetObject(id,OpenMode.ForRead) as Entity;if(entity!=null&&(!space.HasValue||entity.OwnerId==space.Value))ids.Add(id);
            }return ids.ToArray();
        }
        // Capture object visibility in all layouts, not only the active layout: native
        // UNISOLATEOBJECTS can also restore objects hidden before this session.
        static ObjectId[] Hidden(Document doc)
        {
            var result=new List<ObjectId>();using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction()){
                var table=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);
                foreach(ObjectId bid in table){var record=tr.GetObject(bid,OpenMode.ForRead) as BlockTableRecord;if(record==null||!record.IsLayout)continue;
                    foreach(ObjectId id in record){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e!=null&&!e.Visible)result.Add(id);}}
            }return result.ToArray();
        }
        static ObjectId[] Visible(Document doc)
        {
            var result=new List<ObjectId>();using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction()){
                var table=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);
                foreach(ObjectId bid in table){var record=tr.GetObject(bid,OpenMode.ForRead) as BlockTableRecord;if(record==null||!record.IsLayout)continue;
                    foreach(ObjectId id in record){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e!=null&&e.Visible)result.Add(id);}}
            }return result.ToArray();
        }
        // Objects that were visible before the session but could not be shown again stay
        // "visible" in the session record, so a later capture never bakes them in as hidden.
        static void Capture(Document doc,Session session)
        {
            var hidden=Hidden(doc);var visible=Visible(doc);
            if(session.Unresolved.Count>0){var stuck=hidden.Where(session.Unresolved.Contains).ToArray();hidden=hidden.Except(stuck).ToArray();visible=visible.Concat(stuck).Distinct().ToArray();}
            session.HiddenBefore=hidden;session.VisibleBefore=visible;
        }
        static ObjectId[] InSpace(Document doc,IEnumerable<ObjectId> ids,ObjectId space){return Valid(doc,ids,space);}
        // A new session may start while objects are still hidden by an earlier native
        // isolation, a lost Mr.T session or a drawing reopened with OBJECTISOLATIONMODE=1.
        // Treating them as intentionally hidden made MRTVIEW keep them hidden forever.
        static void ConfirmPreHidden(Document doc,Session session)
        {
            var hiddenHere=InSpace(doc,session.HiddenBefore,session.Space);if(hiddenHere.Length==0)return;
            var options=new PromptKeywordOptions("\nMr.T: Không gian hiện hành đang có "+hiddenHere.Length+" đối tượng bị ẩn (HIDE/ISOLATE trước đó). Trong bối cảnh MRTVIEW, các đối tượng này nên [Hien/Giu] <Hien>: ","Hien Giu"){AllowNone=true};
            options.Keywords.Default="Hien";string answer="Hien";
            try{var result=doc.Editor.GetKeywords(options);if(result.Status==PromptStatus.OK&&!String.IsNullOrEmpty(result.StringResult))answer=result.StringResult;}catch(System.Exception){}
            if(answer!="Hien"){doc.Editor.WriteMessage("\nMr.T: Giữ ẩn "+hiddenHere.Length+" đối tượng trong bối cảnh. MRTISODIAG để kiểm tra.\n");return;}
            var set=new HashSet<ObjectId>(hiddenHere);session.HiddenBefore=session.HiddenBefore.Where(id=>!set.Contains(id)).ToArray();session.VisibleBefore=(session.VisibleBefore??new ObjectId[0]).Concat(hiddenHere).Distinct().ToArray();
            doc.Editor.WriteMessage("\nMr.T: "+hiddenHere.Length+" đối tượng ẩn sẵn sẽ hiện trong bối cảnh và sau MRTUNISO.\n");
        }
        // Do not expose previously hidden entities. Reconcile only IDs recorded as
        // visible immediately before this isolation. A locked layer is unlocked only
        // inside this transaction and locked again before commit, so its state is unchanged.
        static void ReconcileContext(Document doc,Session session)
        {
            int restored=0,unresolved=0;session.Unresolved.Clear();using(var locked=doc.LockDocument())using(var tr=doc.Database.TransactionManager.StartTransaction()){
                var relock=new HashSet<LayerTableRecord>();
                try{
                    foreach(var id in session.VisibleBefore??new ObjectId[0]){
                        if(id.IsNull||!id.IsValid||id.IsErased||id.Database.UnmanagedObject!=doc.Database.UnmanagedObject)continue;
                        var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||e.Visible)continue;
                        var layer=tr.GetObject(e.LayerId,OpenMode.ForRead) as LayerTableRecord;
                        if(layer==null||layer.IsDependent){unresolved++;session.Unresolved.Add(id);continue;}
                        if(layer.IsLocked&&!relock.Contains(layer)){layer.UpgradeOpen();layer.IsLocked=false;relock.Add(layer);}
                        try{e.UpgradeOpen();e.Visible=true;restored++;}catch(Autodesk.AutoCAD.Runtime.Exception){unresolved++;session.Unresolved.Add(id);}
                    }
                }finally{foreach(var layer in relock)layer.IsLocked=true;}
                tr.Commit();
            }
            if(restored>0)doc.Editor.WriteMessage("\nMr.T: Đã khôi phục Visible của "+restored+" đối tượng từng hiện trước isolate (bản vẽ có thay đổi).\n");
            if(unresolved>0)doc.Editor.WriteMessage("\nMr.T: Còn "+unresolved+" đối tượng từng hiện nhưng chưa khôi phục được; kiểm tra Layer khóa/Xref hoặc đối tượng đặc biệt.\n");
        }
        static ObjectId[] Resolve(Document doc,Session s){var found=IsolationSelection.ResolveSplit(s.Ids,s.Splits,id=>!id.IsNull&&id.IsValid&&!id.IsErased&&id.Database.UnmanagedObject==doc.Database.UnmanagedObject);return Valid(doc,found,s.Space);}
        public static void RecordSplit(Document doc,IDictionary<ObjectId,ObjectId[]> replacements)
        {
            Session s;if(!sessions.TryGetValue(doc,out s)||replacements==null)return;
            foreach(var pair in replacements){s.Splits[pair.Key]=pair.Value;if((s.VisibleBefore??new ObjectId[0]).Contains(pair.Key))s.VisibleBefore=s.VisibleBefore.Concat(pair.Value).Distinct().ToArray();if((s.HiddenBefore??new ObjectId[0]).Contains(pair.Key))s.HiddenBefore=s.HiddenBefore.Concat(pair.Value).Distinct().ToArray();}
            if(replacements.Count>0)doc.Editor.WriteMessage("\nMr.T: Đã ghi nhận các đoạn sau tách cho phiên Isolate; có thể UNDO/REDO.\n");
        }
        static ObjectId[] CurrentlyVisible(Document doc,IEnumerable<ObjectId> ids)
        {
            var result=new List<ObjectId>();using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction())foreach(var id in Valid(doc,ids)){
                var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e!=null&&e.Visible)result.Add(id);
            }return result.ToArray();
        }
        static void NativeReset(Document doc,ObjectId[] hiddenBefore)
        {
            busy=true;try{
                doc.Editor.Command("_.UNISOLATEOBJECTS");
                // Entities whose Visible flag remained false were not unhidden by AutoCAD;
                // do not convert those into native hidden objects.
                var becameVisible=CurrentlyVisible(doc,hiddenBefore);
                if(becameVisible.Length>0){using(var set=SelectionSet.FromObjectIds(becameVisible))doc.Editor.Command("_.HIDEOBJECTS",set,"");}
                var missed=CurrentlyVisible(doc,becameVisible);
                if(missed.Length>0)throw new InvalidOperationException("AutoCAD chưa ẩn lại "+missed.Length+" đối tượng đã ẩn trước đó. Kiểm tra các Layout/Layer và dùng HIDEOBJECTS nếu cần.");
            }finally{busy=false;}
        }
        // Always finish the layer restore and visibility reconcile, even if re-hiding failed,
        // so the screen never stays half isolated and half in context.
        static void ToContext(Document doc,Session session)
        {
            System.Exception failure=null;
            try{NativeReset(doc,session.HiddenBefore);}catch(System.Exception ex){failure=ex;}
            finally{Restore(doc,true,false);session.Context=true;ReconcileContext(doc,session);RegenAll(doc);}
            if(failure!=null)doc.Editor.WriteMessage("\nMr.T: Đã trả bối cảnh nhưng có cảnh báo: "+failure.Message+"\n");
        }
        public static void End(Document doc)
        {
            Start();Hook(doc);Session session;
            if(sessions.TryGetValue(doc,out session)){
                if(session.Context)Capture(doc,session);
                try{ToContext(doc,session);}finally{sessions.Remove(doc);}
                doc.Editor.WriteMessage("\nMr.T: Đã kết thúc phiên Isolate; giữ trạng thái ẩn trước phiên.\n");
            }else{busy=true;try{doc.Editor.Command("_.UNISOLATEOBJECTS");}finally{busy=false;}Restore(doc);}
        }
        public static string Diagnose(Document doc,ObjectId space)
        {
            int total=0,hidden=0,hiddenLocked=0,onOff=0,onFrozen=0;var lines=new List<string>();
            using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction()){
                var btr=(BlockTableRecord)tr.GetObject(space,OpenMode.ForRead);
                foreach(ObjectId id in btr){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null)continue;total++;var layer=tr.GetObject(e.LayerId,OpenMode.ForRead) as LayerTableRecord;
                    if(!e.Visible){hidden++;if(layer!=null&&layer.IsLocked)hiddenLocked++;}
                    if(layer!=null){if(layer.IsOff)onOff++;if(layer.IsFrozen)onFrozen++;}}
                lines.Add("Không gian: "+btr.Name+" — "+total+" đối tượng trực tiếp.");
            }
            lines.Add("Đang ẩn bằng HIDE/ISOLATE (Visible=false): "+hidden+(hiddenLocked>0?" (trong đó "+hiddenLocked+" trên Layer khóa)":"")+".");
            lines.Add("Nằm trên Layer tắt: "+onOff+"; trên Layer đóng băng: "+onFrozen+".");
            try{lines.Add("OBJECTISOLATIONMODE="+AcApp.GetSystemVariable("OBJECTISOLATIONMODE")+" (1 = trạng thái ẩn được lưu cùng file); CVPORT="+AcApp.GetSystemVariable("CVPORT")+".");}catch(System.Exception){}
            lines.Add("Layer bật tạm còn chờ trả: "+(IsolationLayers.HasPending(doc.Database)?"có — gõ MRTUNISO":"không")+".");
            Session s;
            if(sessions.TryGetValue(doc,out s)){
                lines.Add("Phiên Mr.T: có, "+(s.Space==space?"cùng":"KHÁC")+" không gian; chế độ "+(s.Context?"BỐI CẢNH":"ISOLATE")+"; nhóm "+Resolve(doc,s).Length+" đối tượng.");
                lines.Add("Ghi nhận trước phiên: ẩn "+InSpace(doc,s.HiddenBefore,space).Length+", hiện "+InSpace(doc,s.VisibleBefore,space).Length+" (không gian này); chưa khôi phục được: "+s.Unresolved.Count+".");
                var shouldShow=InSpace(doc,s.VisibleBefore,space);int stillHidden=0;using(var tr=doc.Database.TransactionManager.StartOpenCloseTransaction())foreach(var id in shouldShow){var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e!=null&&!e.Visible)stillHidden++;}
                if(s.Context&&stillHidden>0)lines.Add("CẢNH BÁO: "+stillHidden+" đối tượng từng hiện nhưng đang ẩn trong bối cảnh.");
            }else lines.Add("Phiên Mr.T: không có"+(hidden>0?" — các đối tượng đang ẩn sẽ được hỏi khi MRTISO bắt đầu phiên mới.":"."));
            return String.Join("\n",lines);
        }
        public static ObjectId[] BeforeAssignment(Document doc,ObjectId space){Session s;return sessions.TryGetValue(doc,out s)&&s.Space==space?Resolve(doc,s):new ObjectId[0];}
        public static bool HasSession(Document doc,ObjectId space){Session s;return sessions.TryGetValue(doc,out s)&&s.Space==space;}
        public static void Add(Document doc,IEnumerable<ObjectId> additional,ObjectId space)
        {
            Session s;if(!sessions.TryGetValue(doc,out s)||s.Space!=space)throw new InvalidOperationException("Chưa có phiên MRTISO trong không gian này. Hãy cô lập nhóm đầu tiên trước.");
            var retained=Resolve(doc,s);var added=Valid(doc,additional??new ObjectId[0],space);var merged=Valid(doc,retained.Concat(added),space);
            if(merged.Length==retained.Length){doc.Editor.WriteMessage("\nMr.T: Không có đối tượng mới để bổ sung.\n");return;}
            if(s.Context)Capture(doc,s);
            Apply(doc,s,merged,true);s.Ids=s.Ids.Concat(added).Distinct().ToArray();
        }
        public static void Toggle(Document doc,ObjectId space)
        {
            Session s;if(!sessions.TryGetValue(doc,out s)||s.Space!=space)throw new InvalidOperationException("Chưa có phiên MRTISO trong không gian hiện hành; hãy chọn đối tượng và chạy MRTISO trước.");
            if(!s.Context){ToContext(doc,s);doc.Editor.SetImpliedSelection(new ObjectId[0]);doc.Editor.WriteMessage("\nMr.T: BỐI CẢNH TRƯỚC ISOLATE — nhóm "+Resolve(doc,s).Length+" đối tượng vẫn được nhớ. MRTVIEW: trở lại; MRTISOADD: thêm.\n");}
            else {Capture(doc,s);var valid=Resolve(doc,s);if(valid.Length==0)throw new InvalidOperationException("Nhóm đã chọn không còn đối tượng hợp lệ; đang giữ chế độ bối cảnh. Chọn thêm bằng MRTISOADD hoặc kết thúc MRTUNISO.");Apply(doc,s,valid,true);}
        }
        public static void AfterAssignment(Document doc,IEnumerable<ObjectId> retained,IEnumerable<ObjectId> family,IEnumerable<ObjectId> assigned,ObjectId space)
        {
            var applied=assigned==null?new ObjectId[0]:assigned.ToArray();Isolate(doc,IsolationSelection.AfterAssignment(retained,family,applied),space);
            try{doc.Editor.SetImpliedSelection(applied);}catch(Autodesk.AutoCAD.Runtime.Exception ex){doc.Editor.WriteMessage("\nMr.T: Đã giữ nhóm Isolate; chưa đánh dấu riêng ống vừa gán: "+ex.Message+"\n");}
        }
        public static void Isolate(Document doc,IEnumerable<ObjectId> chosen,ObjectId space)
        {
            Start();Hook(doc);var ids=Valid(doc,chosen,space);if(ids.Length==0){doc.Editor.WriteMessage("\nMr.T: Không có đối tượng trực tiếp trong không gian hiện hành để Isolate.\n");return;}
            Session s;if(sessions.TryGetValue(doc,out s)){
                if(s.Space!=space)throw new InvalidOperationException("Phiên Isolate đang ở Model/Layout khác. Hãy trở lại không gian đó và MRTUNISO trước.");
                if(s.Context)Capture(doc,s);
            }else{s=new Session{Space=space,Ids=ids,Context=true};Capture(doc,s);ConfirmPreHidden(doc,s);sessions[doc]=s;}
            Apply(doc,s,ids);
        }
        static void Apply(Document doc,Session s,ObjectId[] ids,bool keepRoots=false)
        {
            ids=Valid(doc,ids,s.Space);if(ids.Length==0)throw new InvalidOperationException("Không còn đối tượng hợp lệ để Isolate.");
            NativeReset(doc,s.HiddenBefore);Restore(doc,true,false);s.Context=true;
            int opened;using(var locked=doc.LockDocument())opened=IsolationLayers.Open(doc.Database,ids);
            try{doc.Editor.SetImpliedSelection(new ObjectId[0]);using(var selection=SelectionSet.FromObjectIds(ids))doc.Editor.Command("_.ISOLATEOBJECTS",selection,"");if(!keepRoots){s.Ids=ids;s.Splits.Clear();}s.Context=false;
                try{doc.Editor.SetImpliedSelection(ids);}catch(Autodesk.AutoCAD.Runtime.Exception ex){doc.Editor.WriteMessage("\nMr.T: Đã cô lập, nhưng chưa đánh dấu lại selection: "+ex.Message+"\n");}
                RegenAll(doc);doc.Editor.WriteMessage("\nMr.T: ISOLATE "+ids.Length+" đối tượng; bật tạm "+opened+" Layer. MRTVIEW: xem bối cảnh; MRTISOADD: thêm; MRTUNISO: kết thúc.\n");
            }catch{try{NativeReset(doc,s.HiddenBefore);Restore(doc);}catch(System.Exception ex){doc.Editor.WriteMessage("\nMr.T: Chưa khôi phục đầy đủ trạng thái: "+ex.Message+"\n");}throw;}
        }
    }
}
