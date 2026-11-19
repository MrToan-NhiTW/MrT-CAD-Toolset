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
                        Polyline frame;Wipeout wipe;DecorMembers(tr,br,FindRoomGroup(tr,br),out frame,out wipe);
                        bool layout=tag!=null&&tag.LayoutSet;
                        rows.Add(new RoomBlockInfo{Handle=br.Handle.ToString(),Space=spaceName,Id=a(TagId),Name=RoomLayout.Plain(text),OldName=a(TagOld),NewName=a(TagNew),Status=a(TagStatus),Note=a(TagNote),
                            Layer=br.Layer,Height=height,Width=layout?tag.FixedWidth:width,Color=br.Color.IsByLayer?256:br.ColorIndex,
                            MaxLines=layout?tag.MaxLines:0,WidthFactor=layout&&tag.WidthFactor>0?tag.WidthFactor:1,
                            Checked=tag!=null&&tag.ReviewSet?!tag.Review:String.IsNullOrWhiteSpace(a(TagNote)),
                            Frame=frame==null||tag==null||String.IsNullOrEmpty(tag.FrameShape)?RoomLabelStyle.FrameNone:tag.FrameShape,
                            Pad=tag==null?0.4:tag.FramePad,Radius=tag==null?0.6:tag.FrameRadius,Mask=wipe!=null});
                    }
                }
            }
            return rows;
        }

        // Writes the given rows (only those that differ from the drawing). One transaction, one UNDO.
        public static int SaveRoomBlocks(Database db,IEnumerable<RoomBlockInfo> rows)
        {
            return SaveRoomBlocks(db,rows,158,1);
        }
        // Checked rooms take the normal colour, unchecked ones the review colour.
        public static int SaveRoomBlocks(Database db,IEnumerable<RoomBlockInfo> rows,int normalColor,int reviewColor)
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
                    br.ColorIndex=(short)(row.Checked?normalColor:reviewColor);
                    foreach(ObjectId aid in br.AttributeCollection)
                    {
                        var ar=(AttributeReference)tr.GetObject(aid,OpenMode.ForWrite);ar.LayerId=layer;ar.Color=br.Color;
                        string value=ar.Tag==TagOld?row.OldName:ar.Tag==TagNew?row.NewName:ar.Tag==TagStatus?row.Status:ar.Tag==TagNote?row.Note:null;
                        if(value!=null&&ar.TextString!=value)ar.TextString=value??"";
                    }
                    // Name text, height and width. Everything else of the multiline attribute is kept.
                    Point3d center;Vector3d direction,normal;ObjectId styleId;
                    using(var m=name.MTextAttribute){center=m.Location;direction=m.Direction;normal=m.Normal;styleId=m.TextStyleId;}
                    LayoutName(db,name,row.Name,row.Height,row.Width,row.MaxLines,row.WidthFactor,center,direction,normal);
                    tag.LastText=row.Name;tag.OldName=row.OldName;tag.NewName=row.NewName;tag.Status=row.Status;
                    tag.LayoutSet=true;tag.FixedWidth=row.Width;tag.MaxLines=row.MaxLines;tag.WidthFactor=row.WidthFactor;tag.ReviewSet=true;tag.Review=!row.Checked;
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
