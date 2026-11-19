using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MedGas2024.Core;

namespace MedGas2024.Engine
{
    // Room manager (2.0.2.5): lists every Mr.T_PHONG block and writes edits straight to the
    // block, its attributes, frame and wipeout, without exploding the block or the group.
    public static partial class RoomService
    {
        static AttributeReference NameOf(Transaction tr,BlockReference br,OpenMode mode)
        {
            foreach(ObjectId id in br.AttributeCollection)
            {
                var ar=tr.GetObject(id,OpenMode.ForRead) as AttributeReference;
                if(ar!=null&&ar.Tag==TagName){if(mode==OpenMode.ForWrite)ar.UpgradeOpen();return ar;}
            }
            return null;
        }
        static bool IsRoomBlock(Transaction tr,BlockReference br)
        {
            return br!=null&&!br.IsErased&&((BlockTableRecord)tr.GetObject(br.DynamicBlockTableRecord,OpenMode.ForRead)).Name==RoomBlockName;
        }

        public static List<RoomBlockInfo> ListRoomBlocks(Database db)
        {
            var rows=new List<RoomBlockInfo>();
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
                foreach(ObjectId sid in table)
                {
                    var space=(BlockTableRecord)tr.GetObject(sid,OpenMode.ForRead);if(!space.IsLayout)continue;
                    string spaceName=space.Name.Equals(BlockTableRecord.ModelSpace,StringComparison.OrdinalIgnoreCase)?"Model":((Layout)tr.GetObject(space.LayoutId,OpenMode.ForRead)).LayoutName;
                    foreach(ObjectId id in space)
                    {
                        var br=tr.GetObject(id,OpenMode.ForRead) as BlockReference;if(!IsRoomBlock(tr,br))continue;
                        var name=NameOf(tr,br,OpenMode.ForRead);if(name==null)continue;
                        var attrs=new Dictionary<string,string>();
                        foreach(ObjectId aid in br.AttributeCollection){var ar=(AttributeReference)tr.GetObject(aid,OpenMode.ForRead);attrs[ar.Tag]=ar.TextString;}
                        RoomTag tag=null;try{var json=Read(name,tr);if(json!=null)tag=RoomTag.Parse(json);}catch(ArgumentException){}catch(System.Runtime.Serialization.SerializationException){}
                        string text;double height,width;using(var m=name.MTextAttribute){text=m.Text;height=m.TextHeight;width=m.Width;}
                        Func<string,string> a=k=>{string v;return attrs.TryGetValue(k,out v)?v:"";};
                        rows.Add(new RoomBlockInfo{Handle=br.Handle.ToString(),Space=spaceName,Id=a(TagId),Name=text,OldName=a(TagOld),NewName=a(TagNew),Status=a(TagStatus),Note=a(TagNote),
                            Layer=br.Layer,Height=height,Width=width,Color=br.Color.IsByLayer?256:br.ColorIndex,
                            Frame=tag==null||String.IsNullOrEmpty(tag.FrameShape)||String.IsNullOrEmpty(tag.Frame)?RoomLabelStyle.FrameNone:tag.FrameShape,
                            Pad=tag==null?0.4:tag.FramePad,Radius=tag==null?0.6:tag.FrameRadius,Mask=tag!=null&&tag.Mask&&!String.IsNullOrEmpty(tag.Wipeout)});
                    }
                }
            }
            return rows;
        }

        // Writes the given rows (only those that differ from the drawing). One transaction, one UNDO.
        public static int SaveRoomBlocks(Database db,IEnumerable<RoomBlockInfo> rows)
        {
            int count=0;
            using(var tr=db.TransactionManager.StartTransaction())
            {
                foreach(var row in rows)
                {
                    var problem=row.Problem();if(problem!=null)throw new InvalidOperationException("Phòng "+row.Id+": "+problem);
                    var id=FromHandle(db,row.Handle);
                    var br=id.IsNull?null:tr.GetObject(id,OpenMode.ForWrite) as BlockReference;
                    if(!IsRoomBlock(tr,br))throw new InvalidOperationException("Block "+row.Handle+" ("+row.Id+") không còn trong bản vẽ. Mở lại danh sách.");
                    var layerRecord=(LayerTableRecord)tr.GetObject(br.LayerId,OpenMode.ForRead);
                    if(layerRecord.IsLocked)throw new InvalidOperationException("Layer "+br.Layer+" của phòng "+row.Id+" đang khóa.");
                    var name=NameOf(tr,br,OpenMode.ForWrite);
                    RoomTag tag=null;try{var json=Read(name,tr);if(json!=null)tag=RoomTag.Parse(json);}catch(ArgumentException){}catch(System.Runtime.Serialization.SerializationException){}
                    tag=tag??new RoomTag{Id=row.Id,Original=row.OldName,OldName=row.OldName,NewName=row.NewName};
                    var layer=ResolveLayer(db,tr,row.Layer);
                    if(br.LayerId!=layer)br.LayerId=layer;
                    if(row.Color==256)br.Color=Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer,256);else br.ColorIndex=(short)row.Color;
                    foreach(ObjectId aid in br.AttributeCollection)
                    {
                        var ar=(AttributeReference)tr.GetObject(aid,OpenMode.ForWrite);ar.LayerId=layer;ar.Color=br.Color;
                        string value=ar.Tag==TagOld?row.OldName:ar.Tag==TagNew?row.NewName:ar.Tag==TagStatus?row.Status:ar.Tag==TagNote?row.Note:null;
                        if(value!=null&&ar.TextString!=value)ar.TextString=value??"";
                    }
                    // Name text, height and width. Everything else of the multiline attribute is kept.
                    Point3d center;Vector3d direction,normal;ObjectId styleId;
                    using(var m=name.MTextAttribute){center=m.Location;direction=m.Direction;normal=m.Normal;styleId=m.TextStyleId;}
                    name.Height=row.Height;name.Justify=AttachmentPoint.MiddleCenter;name.AlignmentPoint=center;
                    using(var m=name.MTextAttribute)
                    {
                        m.TextHeight=row.Height;m.Width=row.Width>0?row.Width:0;m.Attachment=AttachmentPoint.MiddleCenter;m.Location=center;
                        if(m.Text!=row.Name)m.Contents=Literal(row.Name);
                        name.MTextAttribute=m;
                    }
                    name.UpdateMTextAttribute();
                    tag.LastText=row.Name;tag.OldName=row.OldName;tag.NewName=row.NewName;tag.Status=row.Status;
                    tag.FrameShape=row.Frame==RoomLabelStyle.FrameNone?null:row.Frame;tag.FramePad=row.Pad;tag.FrameRadius=row.Radius;tag.Mask=row.Mask;
                    UpdateDecor(db,tr,br,name,tag);
                    Save(name,tr,tag);
                    count++;
                }
                tr.Commit();
            }
            return count;
        }

        public static Extents3d RoomExtents(Database db,string handle)
        {
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var id=FromHandle(db,handle);var br=id.IsNull?null:tr.GetObject(id,OpenMode.ForRead) as BlockReference;
                if(br==null)throw new InvalidOperationException("Không tìm thấy block "+handle+".");
                var name=NameOf(tr,br,OpenMode.ForRead);
                using(var m=name.MTextAttribute)
                {
                    double size=Math.Max(m.TextHeight*4,Math.Max(m.ActualWidth,m.Width));var c=m.Location;
                    return new Extents3d(new Point3d(c.X-size,c.Y-size,c.Z),new Point3d(c.X+size,c.Y+size,c.Z));
                }
            }
        }
    }
}
