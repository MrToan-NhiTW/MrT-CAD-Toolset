using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MedGas2024.Core;

namespace MedGas2024.Engine
{
    // Room labels as a block with attributes (2.0.2.4): the visible name plus ID,
    // old name, new name, status and a free note, so the data shows in Properties,
    // EATTEDIT and DATAEXTRACTION. A rounded frame polyline on the same layer is
    // grouped with the block and redrawn when the name changes.
    public static partial class RoomService
    {
        public const string RoomBlockName="Mr.T_PHONG";
        public const string TagName="TEN_PHONG",TagId="MA_PHONG",TagOld="TEN_CU",TagNew="TEN_MOI",TagStatus="TRANG_THAI",TagNote="GHI_CHU";

        static ObjectId EnsureRoomBlock(Database db,Transaction tr,ObjectId styleId)
        {
            var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
            if(table.Has(RoomBlockName))
            {
                var existing=(BlockTableRecord)tr.GetObject(table[RoomBlockName],OpenMode.ForRead);
                var tags=existing.Cast<ObjectId>().Select(id=>tr.GetObject(id,OpenMode.ForRead) as AttributeDefinition).Where(a=>a!=null).Select(a=>a.Tag).ToList();
                if(!tags.Contains(TagName)||!tags.Contains(TagId))
                    throw new InvalidOperationException("Bản vẽ đã có block "+RoomBlockName+" nhưng thiếu thuộc tính "+TagName+"/"+TagId+". Đổi tên block đó rồi chạy lại.");
                return existing.ObjectId;
            }
            table.UpgradeOpen();
            var def=new BlockTableRecord{Name=RoomBlockName,Origin=Point3d.Origin,Comments="Mr.T tên phòng: tên hiển thị, mã, tên cũ, tên mới, trạng thái"};
            table.Add(def);tr.AddNewlyCreatedDBObject(def,true);
            var name=new AttributeDefinition{Tag=TagName,Prompt="Tên phòng",TextString="TÊN PHÒNG",Height=1,Position=Point3d.Origin,TextStyleId=styleId,LockPositionInBlock=true};
            name.SetDatabaseDefaults(db);name.Layer="0";name.TextStyleId=styleId;name.Justify=AttachmentPoint.MiddleCenter;name.AlignmentPoint=Point3d.Origin;name.IsMTextAttributeDefinition=true;
            def.AppendEntity(name);tr.AddNewlyCreatedDBObject(name,true);
            using(var m=name.MTextAttributeDefinition)
            {
                m.TextStyleId=styleId;m.Location=Point3d.Origin;m.Attachment=AttachmentPoint.MiddleCenter;m.TextHeight=1;m.Width=0;m.Contents="TÊN PHÒNG";
                name.MTextAttributeDefinition=m;
            }
            name.UpdateMTextAttributeDefinition();
            int row=0;
            foreach(var field in new[]{new[]{TagId,"Mã phòng (ID)"},new[]{TagOld,"Tên cũ"},new[]{TagNew,"Tên mới (Excel)"},new[]{TagStatus,"Trạng thái"},new[]{TagNote,"Ghi chú"}})
            {
                var a=new AttributeDefinition{Tag=field[0],Prompt=field[1],TextString="",Invisible=true,Height=0.5,Position=new Point3d(0,-1.2-0.8*row++,0),TextStyleId=styleId,LockPositionInBlock=true};
                a.SetDatabaseDefaults(db);a.Layer="0";a.TextStyleId=styleId;a.Invisible=true;
                def.AppendEntity(a);tr.AddNewlyCreatedDBObject(a,true);
            }
            return def.ObjectId;
        }

        static ObjectId ResolveLayer(Database db,Transaction tr,string layer)
        {
            var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
            if(!layers.Has(layer))
            {
                try{SymbolUtilityServices.ValidateSymbolName(layer,false);}
                catch(Exception){throw new InvalidOperationException("Tên layer đích không hợp lệ: "+layer);}
                layers.UpgradeOpen();var created=new LayerTableRecord{Name=layer};layers.Add(created);tr.AddNewlyCreatedDBObject(created,true);
            }
            return layers[layer];
        }

        static Matrix3d RoomFrame(Point3d center,Vector3d direction,Vector3d normal)
        {
            var x=direction.GetNormal();var z=normal.GetNormal();var y=z.CrossProduct(x).GetNormal();
            return Matrix3d.AlignCoordinateSystem(Point3d.Origin,Vector3d.XAxis,Vector3d.YAxis,Vector3d.ZAxis,center,x,y,z);
        }

        // Writes the visible name into the multiline attribute with the chosen height/width.
        static void WriteName(AttributeReference name,string display,RoomLabelStyle style,Point3d center,Vector3d direction,Vector3d normal,ObjectId styleId)
        {
            name.TextStyleId=styleId;
            LayoutName(name.Database,name,display,style.Height,style.Width,style.MaxLines,style.WidthFactor,center,direction,normal);
        }

        static string NameContents(string display,double widthFactor)
        {
            return Factor(Literal(display),widthFactor);
        }
        static string Factor(string contents,double widthFactor)
        {
            return Math.Abs(widthFactor-1)<1e-9?contents:"{\\W"+widthFactor.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+";"+contents+"}";
        }
        static double Measure(Database db,ObjectId styleId,double height,double widthFactor,string text)
        {
            using(var m=new MText())
            {
                m.SetDatabaseDefaults(db);m.TextStyleId=styleId;m.TextHeight=height;m.Width=0;m.Contents=NameContents(text,widthFactor);
                double w=m.ActualWidth;return w>0?w:text.Length*height*0.75*widthFactor;
            }
        }
        // Column width for a name: Fixed width when set; otherwise the narrowest column that keeps the
        // name within Max lines (0 = one line). Words are never split.
        // Lines of a name for Max lines: whole words, as few lines as read well (about
        // LineTarget × Text height each, at most maxLines), balanced to the narrowest column.
        public static string[] NameLines(Database db,ObjectId styleId,string display,double height,int maxLines,double widthFactor)
        {
            var words=RoomLayout.Words(display);
            if(maxLines<=1||words.Length<=1)return new[]{RoomLayout.Plain(display)};
            var widths=words.Select(w=>Measure(db,styleId,height,widthFactor,w)).ToList();
            double space=Math.Max(0,Measure(db,styleId,height,widthFactor,"x x")-Measure(db,styleId,height,widthFactor,"xx"));
            double single=Measure(db,styleId,height,widthFactor,display);
            int lines=Math.Max(1,Math.Min(maxLines,(int)Math.Ceiling(single/(RoomLayout.LineTarget*height))));
            if(lines<=1)return new[]{RoomLayout.Plain(display)};
            var starts=RoomLayout.Breaks(widths,space,lines);
            return starts.Select((s,i)=>String.Join(" ",words.Skip(s).Take((i+1<starts.Length?starts[i+1]:words.Length)-s))).ToArray();
        }
        // Text, height, width (Max lines / Fixed width) and Width factor of the name attribute.
        static void LayoutName(Database db,AttributeReference name,string display,double height,double fixedWidth,int maxLines,double widthFactor,Point3d center,Vector3d direction,Vector3d normal)
        {
            if(!(widthFactor>0))widthFactor=1;
            // UpdateMTextAttribute re-applies the attribute's own height and justification, so set them first.
            name.Height=height;name.Justify=AttachmentPoint.MiddleCenter;name.AlignmentPoint=center;
            // Max lines: the tool places the line breaks itself (exact line count); Fixed width: AutoCAD wraps.
            double width=fixedWidth>0?fixedWidth:0;
            string contents=fixedWidth>0?NameContents(display,widthFactor):
                Factor(String.Join("\\P",NameLines(db,name.TextStyleId,display,height,maxLines,widthFactor).Select(Literal)),widthFactor);
            using(var m=name.MTextAttribute)
            {
                m.TextStyleId=name.TextStyleId;m.Normal=normal;m.Location=center;m.Direction=direction;m.Attachment=AttachmentPoint.MiddleCenter;
                m.TextHeight=height;m.Width=width;m.Contents=contents;
                name.MTextAttribute=m;
            }
            name.UpdateMTextAttribute();
        }
        static void Relayout(Database db,AttributeReference name,string display,RoomTag tag)
        {
            Point3d center;Vector3d direction,normal;double height,width;
            using(var m=name.MTextAttribute){center=m.Location;direction=m.Direction;normal=m.Normal;height=m.TextHeight;width=m.Width;}
            if(!tag.LayoutSet){tag.LayoutSet=true;tag.FixedWidth=width;tag.MaxLines=0;tag.WidthFactor=1;}
            LayoutName(db,name,display,height,tag.FixedWidth,tag.MaxLines,tag.WidthFactor>0?tag.WidthFactor:1,center,direction,normal);
        }

        // Box of the visible name in its own plane (local min/max around the label
        // centre) from the real text corners, so the frame fits any wrap or justification.
        static Extents2d NameBox(AttributeReference name,Matrix3d frame)
        {
            var toLocal=frame.Inverse();var points=new List<Point3d>();
            try
            {
                using(var m=name.MTextAttribute)
                {
                    // Actual text size (not the column width), centred on the middle-centre location.
                    double w=m.ActualWidth,h=m.ActualHeight;
                    if(w>0&&h>0&&m.Attachment==AttachmentPoint.MiddleCenter)
                    {
                        var c=m.Location.TransformBy(toLocal);
                        return new Extents2d(c.X-w/2,c.Y-h/2,c.X+w/2,c.Y+h/2);
                    }
                    using(var corners=m.GetBoundingPoints())
                        foreach(Point3d c in corners)points.Add(c);
                }
            }
            catch(Autodesk.AutoCAD.Runtime.Exception){points.Clear();}
            if(points.Count==0)
            {
                var e=name.GeometricExtents;
                points.AddRange(new[]{e.MinPoint,e.MaxPoint,new Point3d(e.MinPoint.X,e.MaxPoint.Y,e.MinPoint.Z),new Point3d(e.MaxPoint.X,e.MinPoint.Y,e.MinPoint.Z)});
            }
            var local=points.Select(p=>p.TransformBy(toLocal)).ToList();
            return new Extents2d(local.Min(p=>p.X),local.Min(p=>p.Y),local.Max(p=>p.X),local.Max(p=>p.Y));
        }

        // Outline around the name in label-local coordinates: (x, y, bulge to next vertex).
        static List<Tuple<double,double,double>> Outline(Extents2d box,double height,string shape,double pad,double radius)
        {
            double hw=(box.MaxPoint.X-box.MinPoint.X)/2+pad*height,hh=(box.MaxPoint.Y-box.MinPoint.Y)/2+pad*height;
            double cx=(box.MinPoint.X+box.MaxPoint.X)/2,cy=(box.MinPoint.Y+box.MaxPoint.Y)/2;
            var pts=new List<Tuple<double,double,double>>();
            const double quarter=0.41421356237309503; // tan(22.5°): 90° arc
            if(shape==RoomLabelStyle.FrameSlot)
            {
                double r=hh;hw=Math.Max(hw,r);
                pts.Add(Tuple.Create(-hw+r,-hh,0d));pts.Add(Tuple.Create(hw-r,-hh,1d));pts.Add(Tuple.Create(hw-r,hh,0d));pts.Add(Tuple.Create(-hw+r,hh,1d));
            }
            else
            {
                double r=shape==RoomLabelStyle.FrameRect||shape==RoomLabelStyle.FrameNone?0:Math.Min(radius*height,Math.Min(hw,hh)*0.999);
                if(r<=1e-9){pts.Add(Tuple.Create(-hw,-hh,0d));pts.Add(Tuple.Create(hw,-hh,0d));pts.Add(Tuple.Create(hw,hh,0d));pts.Add(Tuple.Create(-hw,hh,0d));}
                else
                {
                    pts.Add(Tuple.Create(-hw+r,-hh,0d));pts.Add(Tuple.Create(hw-r,-hh,quarter));pts.Add(Tuple.Create(hw,-hh+r,0d));pts.Add(Tuple.Create(hw,hh-r,quarter));
                    pts.Add(Tuple.Create(hw-r,hh,0d));pts.Add(Tuple.Create(-hw+r,hh,quarter));pts.Add(Tuple.Create(-hw,hh-r,0d));pts.Add(Tuple.Create(-hw,-hh+r,quarter));
                }
            }
            return pts.Select(p=>Tuple.Create(p.Item1+cx,p.Item2+cy,p.Item3)).ToList();
        }

        // Closed LWPOLYLINE on the outline; rewrites a database polyline in place.
        static void DrawFrame(Polyline pl,Matrix3d frame,Vector3d normal,List<Tuple<double,double,double>> pts)
        {
            var toPlane=Matrix3d.WorldToPlane(normal);
            pl.Normal=normal;
            double elevation=0;int old=pl.NumberOfVertices;
            for(int i=0;i<pts.Count;i++)
            {
                var p=new Point3d(pts[i].Item1,pts[i].Item2,0).TransformBy(frame).TransformBy(toPlane);
                elevation=p.Z;var q=new Point2d(p.X,p.Y);
                if(i<old){pl.SetPointAt(i,q);pl.SetBulgeAt(i,pts[i].Item3);pl.SetStartWidthAt(i,0);pl.SetEndWidthAt(i,0);}
                else pl.AddVertexAt(i,q,pts[i].Item3,0,0);
            }
            for(int i=old-1;i>=pts.Count;i--)pl.RemoveVertexAt(i);
            pl.Elevation=elevation;pl.Closed=true;
        }

        // Polygon for the WIPEOUT: the same outline with arcs split into short chords, closed.
        static Point2dCollection Polygon(List<Tuple<double,double,double>> pts)
        {
            var result=new Point2dCollection();
            for(int i=0;i<pts.Count;i++)
            {
                var a=new Point2d(pts[i].Item1,pts[i].Item2);var b=new Point2d(pts[(i+1)%pts.Count].Item1,pts[(i+1)%pts.Count].Item2);double bulge=pts[i].Item3;
                result.Add(a);
                if(Math.Abs(bulge)<1e-9)continue;
                double sweep=4*Math.Atan(bulge),chord=a.GetDistanceTo(b);if(chord<1e-12)continue;
                double radius=chord/(2*Math.Sin(Math.Abs(sweep)/2));
                var mid=new Point2d((a.X+b.X)/2,(a.Y+b.Y)/2);var dir=(b-a).GetNormal();var left=new Vector2d(-dir.Y,dir.X);
                double sagitta=radius-Math.Sqrt(Math.Max(0,radius*radius-chord*chord/4));
                var center=mid+left*((bulge>0?1:-1)*(radius-sagitta));
                double start=Math.Atan2(a.Y-center.Y,a.X-center.X);int steps=Math.Max(4,(int)Math.Ceiling(Math.Abs(sweep)/(Math.PI/16)));
                for(int k=1;k<steps;k++){double t=start+sweep*k/steps;result.Add(new Point2d(center.X+radius*Math.Cos(t),center.Y+radius*Math.Sin(t)));}
            }
            result.Add(result[0]);
            return result;
        }

        static ObjectId FromHandle(Database db,string handle)
        {
            long value;ObjectId id;
            if(String.IsNullOrEmpty(handle)||!Int64.TryParse(handle,System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out value)||
                !db.TryGetObjectId(new Handle(value),out id)||id.IsErased)return ObjectId.Null;
            return id;
        }

        static Group FindRoomGroup(Transaction tr,BlockReference br)
        {
            using(var reactors=br.GetPersistentReactorIds())
                foreach(ObjectId r in reactors){if(r.IsNull||r.IsErased)continue;var g=tr.GetObject(r,OpenMode.ForRead) as Group;if(g!=null&&g.Description!=null&&g.Description.StartsWith("Mr.T phòng"))return g;}
            return null;
        }
        // The frame polyline and wipeout grouped with this block, in the same space.
        static void DecorMembers(Transaction tr,BlockReference br,Group group,out Polyline frame,out Wipeout wipe)
        {
            frame=null;wipe=null;if(group==null)return;
            foreach(var id in group.GetAllEntityIds())
            {
                if(id==br.ObjectId||id.IsErased)continue;var e=tr.GetObject(id,OpenMode.ForRead) as Entity;if(e==null||e.OwnerId!=br.OwnerId)continue;
                if(frame==null&&e is Polyline)frame=(Polyline)e;else if(wipe==null&&e is Wipeout)wipe=(Wipeout)e;
            }
        }
        static Group RoomGroup(Database db,Transaction tr,BlockReference br,string id)
        {
            var found=FindRoomGroup(tr,br);if(found!=null)return found;
            var groups=(DBDictionary)tr.GetObject(db.GroupDictionaryId,OpenMode.ForWrite);
            var created=new Group("Mr.T phòng "+id,true);groups.SetAt("*",created);tr.AddNewlyCreatedDBObject(created,true);
            created.Append(br.ObjectId);return created;
        }

        // Creates, redraws or removes the frame and the WIPEOUT of one room block from its tag,
        // keeps them grouped with the block, the wipeout just below the block. Updates tag handles.
        static void UpdateDecor(Database db,Transaction tr,BlockReference br,AttributeReference name,RoomTag tag)
        {
            var space=(BlockTableRecord)tr.GetObject(br.OwnerId,OpenMode.ForWrite);
            Point3d center;Vector3d direction,normal;double height;
            using(var m=name.MTextAttribute){center=m.Location;direction=m.Direction;normal=m.Normal;height=m.TextHeight;}
            var local=RoomFrame(center,direction,normal);
            string shape=String.IsNullOrEmpty(tag.FrameShape)?RoomLabelStyle.FrameNone:tag.FrameShape;
            var outline=Outline(NameBox(name,local),height,shape,tag.FramePad,tag.FrameRadius);
            // Frame and wipeout are taken only from the block's own group. Stored handles are not trusted:
            // after a copy to another drawing they may name an unrelated object there.
            Polyline member;Wipeout memberWipe;var group=FindRoomGroup(tr,br);
            DecorMembers(tr,br,group,out member,out memberWipe);
            if(shape==RoomLabelStyle.FrameNone)
            {
                if(member!=null){member.UpgradeOpen();member.Erase();}
                tag.Frame=null;
            }
            else
            {
                Polyline pl=member;if(pl!=null)pl.UpgradeOpen();
                if(pl==null)
                {
                    pl=new Polyline();pl.SetDatabaseDefaults(db);DrawFrame(pl,local,normal,outline);
                    space.AppendEntity(pl);tr.AddNewlyCreatedDBObject(pl,true);
                    group=group??RoomGroup(db,tr,br,tag.Id);group.UpgradeOpen();group.Append(pl.ObjectId);
                }
                else DrawFrame(pl,local,normal,outline);
                pl.LayerId=br.LayerId;pl.Color=br.Color;tag.Frame=pl.ObjectId.Handle.ToString();
            }
            if(!tag.Mask)
            {
                if(memberWipe!=null){memberWipe.UpgradeOpen();memberWipe.Erase();}
                tag.Wipeout=null;
            }
            else
            {
                var wipe=memberWipe;if(wipe!=null)wipe.UpgradeOpen();
                bool created=wipe==null;
                if(created){wipe=new Wipeout();wipe.SetDatabaseDefaults(db);}
                wipe.SetFrom(Polygon(outline),Vector3d.ZAxis);wipe.TransformBy(local);
                if(created)
                {
                    space.AppendEntity(wipe);tr.AddNewlyCreatedDBObject(wipe,true);
                    group=group??RoomGroup(db,tr,br,tag.Id);group.UpgradeOpen();group.Append(wipe.ObjectId);
                    var order=(DrawOrderTable)tr.GetObject(space.DrawOrderTableId,OpenMode.ForWrite);
                    order.MoveBelow(new ObjectIdCollection(new[]{wipe.ObjectId}),br.ObjectId);
                }
                wipe.LayerId=br.LayerId;tag.Wipeout=wipe.ObjectId.Handle.ToString();
            }
        }

        static string Status(RoomEntry entry)
        {
            return String.IsNullOrEmpty(entry.Status)?RoomCatalog.StatusRename:entry.Status;
        }

        // Creates one Mr.T_PHONG insert (+ frame and wipeout, grouped) and returns the visible name attribute.
        static ObjectId CreateRoomBlock(Database db,Transaction tr,BlockTableRecord space,RoomEntry entry,bool newMode,RoomLabelStyle style,
            Tuple<ObjectId,ObjectId> target,ObjectId def,Point3d center,Vector3d direction,Vector3d normal,string source,string oldText,bool review=false,string note=null,string status=null)
        {
            short color=(short)(review?style.ReviewColor:style.Color);
            var statusText=String.IsNullOrEmpty(status)?Status(entry):status;
            string display=RoomRules.Label(entry,newMode,style.Upper);
            if(String.IsNullOrWhiteSpace(display))throw new InvalidOperationException("Tên cần hiển thị của "+entry.Id+" đang trống.");
            normal=normal.Length<1e-12?Vector3d.ZAxis:normal.GetNormal();
            direction=(direction-normal*direction.DotProduct(normal));direction=direction.Length<1e-12?Vector3d.XAxis:direction.GetNormal();
            var ocs=direction.TransformBy(Matrix3d.WorldToPlane(normal));
            var br=new BlockReference(center,def){Normal=normal,Rotation=Math.Atan2(ocs.Y,ocs.X),LayerId=target.Item1};
            br.SetDatabaseDefaults(db);br.LayerId=target.Item1;br.ColorIndex=color;
            space.AppendEntity(br);tr.AddNewlyCreatedDBObject(br,true);
            var values=new Dictionary<string,string>{{TagId,entry.Id},{TagOld,oldText??entry.OldName},{TagNew,entry.NewName},{TagStatus,statusText},{TagNote,note??""}};
            AttributeReference name=null;
            var definition=(BlockTableRecord)tr.GetObject(def,OpenMode.ForRead);
            foreach(ObjectId id in definition)
            {
                var ad=tr.GetObject(id,OpenMode.ForRead) as AttributeDefinition;
                if(ad==null||ad.Constant)continue;
                var ar=new AttributeReference();ar.SetAttributeFromBlock(ad,br.BlockTransform);ar.LayerId=target.Item1;ar.ColorIndex=color;
                string value;if(values.TryGetValue(ad.Tag,out value))ar.TextString=value??"";
                br.AttributeCollection.AppendAttribute(ar);tr.AddNewlyCreatedDBObject(ar,true);
                if(ad.Tag==TagName)name=ar;
            }
            if(name==null)throw new InvalidOperationException("Block "+RoomBlockName+" thiếu thuộc tính "+TagName+".");
            if(!name.IsMTextAttribute)throw new InvalidOperationException("Thuộc tính "+TagName+" của block "+RoomBlockName+" cần là MTEXT nhiều dòng.");
            WriteName(name,display,style,center,direction,normal,target.Item2);
            var tag=new RoomTag{Id=entry.Id,Original=entry.OldName,OldName=entry.OldName,NewName=entry.NewName,LastText=display,ShowingNew=newMode,
                Source=source,Upper=style.Upper,FrameShape=style.Frame==RoomLabelStyle.FrameNone?null:style.Frame,
                FramePad=style.Pad,FrameRadius=style.Radius,Mask=style.Mask,Status=statusText,
                LayoutSet=true,MaxLines=style.MaxLines,WidthFactor=style.WidthFactor,FixedWidth=style.Width,ReviewSet=true,Review=review};
            if(style.Frame!=RoomLabelStyle.FrameNone||style.Mask)UpdateDecor(db,tr,br,name,tag);
            Save(name,tr,tag);
            return name.ObjectId;
        }

        // After a name switch: refresh the other attributes and redraw the frame and wipeout.
        static void RefreshRoomBlock(Database db,Transaction tr,AttributeReference name,RoomTag tag,RoomEntry entry)
        {
            var br=tr.GetObject(name.OwnerId,OpenMode.ForRead) as BlockReference;
            if(br==null)return;
            foreach(ObjectId id in br.AttributeCollection)
            {
                var ar=tr.GetObject(id,OpenMode.ForRead) as AttributeReference;if(ar==null||ar.ObjectId==name.ObjectId)continue;
                string value=ar.Tag==TagNew?entry.NewName:ar.Tag==TagStatus&&!String.IsNullOrEmpty(entry.Status)?entry.Status:null;
                if(value!=null&&ar.TextString!=value){ar.UpgradeOpen();ar.TextString=value;}
            }
            Relayout(db,name,tag.LastText,tag);
            UpdateDecor(db,tr,br,name,tag);Save(name,tr,tag);
        }

        static Tuple<ObjectId,ObjectId> ResolveLabelTarget(Database db,Transaction tr,RoomLabelStyle style)
        {
            var layer=ResolveLayer(db,tr,style.Layer);
            var target=(LayerTableRecord)tr.GetObject(layer,OpenMode.ForRead);
            if(target.IsDependent||target.IsLocked||target.IsFrozen)throw new InvalidOperationException("Layer đích bị khóa hoặc đóng băng.");
            var styles=(TextStyleTable)tr.GetObject(db.TextStyleTableId,OpenMode.ForRead);
            if(!styles.Has(style.Style)&&style.Style==RoomCatalog.DefaultStyle)
            {
                styles.UpgradeOpen();var created=new TextStyleTableRecord{Name=style.Style,FileName="arial.ttf"};styles.Add(created);tr.AddNewlyCreatedDBObject(created,true);
            }
            if(!styles.Has(style.Style))throw new InvalidOperationException("Kiểu chữ "+style.Style+" không có trong bản vẽ.");
            var record=(TextStyleTableRecord)tr.GetObject(styles[style.Style],OpenMode.ForRead);
            if(record.IsDependent||record.IsShapeFile||record.IsVertical)throw new InvalidOperationException("Kiểu chữ đã chọn không phù hợp với tên phòng.");
            return Tuple.Create(layer,record.ObjectId);
        }

        // Batch apply with room blocks. Splits get one block per new room, stacked
        // around the old position so they can be dragged into place.
        public static RoomApplyResult ApplyBlocks(Database db,ObjectId spaceId,IEnumerable<RoomHit> selected,bool newMode,RoomLabelStyle style,string hideLayer)
        {
            style=style.Valid();var result=new RoomApplyResult();
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var target=ResolveLabelTarget(db,tr,style);
                var def=EnsureRoomBlock(db,tr,target.Item2);
                var space=(BlockTableRecord)tr.GetObject(spaceId,OpenMode.ForWrite);
                foreach(var hit in selected)
                {
                    if(hit.Id.IsErased)throw new InvalidOperationException("Chữ "+hit.Text+" đã bị xóa. Hãy rà soát lại.");
                    var e=(Entity)tr.GetObject(hit.Id,OpenMode.ForRead);
                    var transform=hit.Path!=null&&hit.Path.Length>0?PathTransform(db,tr,spaceId,hit,e):Matrix3d.Identity;
                    if(RoomText(e)!=hit.Text||!transform.IsEqualTo(hit.ToWorld,new Tolerance(1e-9,1e-7))||Read(e,tr)!=null)
                        throw new InvalidOperationException("Chữ "+hit.Text+" đã thay đổi sau xem trước. Hãy rà soát lại.");
                    Point3d center;Vector3d direction,normal;double height;
                    TextFrame(e,out center,out direction,out normal,out height);
                    var worldCenter=center.TransformBy(transform);var worldDirection=direction.TransformBy(transform);var worldUp=normal.CrossProduct(direction).TransformBy(transform);
                    var worldNormal=worldDirection.CrossProduct(worldUp);worldNormal=worldNormal.Length<1e-12?Vector3d.ZAxis:worldNormal.GetNormal();
                    if(worldNormal.Z<0)worldNormal=worldNormal.Negate();
                    bool replace=style.ReplaceOld&&hit.Direct&&!hit.NeedsLabel;
                    var rooms=hit.Splits??new List<RoomEntry>{hit.Entry};
                    var up=worldNormal.CrossProduct(worldDirection.GetNormal()).GetNormal();
                    double step=style.Height*(1.6+2*style.Pad)+style.Height*0.4;
                    for(int k=0;k<rooms.Count;k++)
                    {
                        var at=worldCenter+up*((rooms.Count-1)/2.0-k)*step;
                        hit.Label=CreateRoomBlock(db,tr,space,rooms[k],newMode,style,target,def,at,worldDirection,worldNormal,replace?null:hit.Address,hit.Text,hit.Review,hit.Note,rooms.Count==1?hit.RoomStatus:null);
                        result.Labels++;
                    }
                    var superseded=hit.Supersedes as RoomHit;
                    if(superseded!=null)
                    {
                        // The 2.0.2.3 MTEXT label for this place becomes the block.
                        if(superseded.Id.IsErased)throw new InvalidOperationException("Nhãn cũ của "+hit.Text+" đã bị xóa. Hãy rà soát lại.");
                        var label=(Entity)tr.GetObject(superseded.Id,OpenMode.ForRead);
                        var layer=(LayerTableRecord)tr.GetObject(label.LayerId,OpenMode.ForRead);
                        if(!(label is MText)||Read(label,tr)==null||layer.IsLocked)throw new InvalidOperationException("Nhãn cũ của "+hit.Text+" không thay được (đã đổi hoặc layer khóa).");
                        label.UpgradeOpen();label.Erase();result.Upgraded++;
                    }
                    if(replace)
                    {
                        var layer=(LayerTableRecord)tr.GetObject(e.LayerId,OpenMode.ForRead);
                        if(e.OwnerId!=spaceId||layer.IsLocked)throw new InvalidOperationException("Chữ "+hit.Text+" không xóa được (layer khóa hoặc đã đổi chỗ).");
                        e.UpgradeOpen();e.Erase();result.Replaced++;
                    }
                }
                if(!String.IsNullOrEmpty(hideLayer))
                {
                    var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
                    if(!layers.Has(hideLayer))throw new InvalidOperationException("Không tìm thấy layer "+hideLayer+".");
                    if(String.Equals(hideLayer,style.Layer,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Không tắt layer đích.");
                    var record=(LayerTableRecord)tr.GetObject(layers[hideLayer],OpenMode.ForWrite);
                    if(!record.IsOff){record.IsOff=true;result.HiddenLayer=hideLayer;}
                }
                tr.Commit();
            }
            return result;
        }

        // Single room at a picked point.
        public static ObjectId PlaceBlock(Database db,RoomEntry row,Point3d point,Vector3d normal,Vector3d direction,bool newMode,RoomLabelStyle style)
        {
            if(row==null||String.IsNullOrWhiteSpace(row.Id)||String.IsNullOrWhiteSpace(row.OldName))throw new ArgumentException("Dòng cần ID và tên cũ.");
            style=style.Valid();
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var target=ResolveLabelTarget(db,tr,style);var def=EnsureRoomBlock(db,tr,target.Item2);
                var space=(BlockTableRecord)tr.GetObject(db.CurrentSpaceId,OpenMode.ForWrite);
                var id=CreateRoomBlock(db,tr,space,row,newMode,style,target,def,point,direction,normal,null,null);
                tr.Commit();return id;
            }
        }
    }
}
