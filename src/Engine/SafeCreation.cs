using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MedGas2024.Core;

namespace MedGas2024.Engine
{
    public static class SafeCreation
    {
        private static Profile Copy(Profile p)
        {
            return new Profile{SchemaVersion=p.SchemaVersion,UnitTag=p.UnitTag,GeometryFactor=p.GeometryFactor,FontFile=p.FontFile,FontBold=p.FontBold,PaddingRatio=p.PaddingRatio,LayerPrefix=p.LayerPrefix,AutoFitOnSync=p.AutoFitOnSync,Rows=p.Selected.Select(r=>r.Copy()).ToList(),SessionStyleName="MrT_TX_"+Guid.NewGuid().ToString("N").Substring(0,16)};
        }
        private static Profile NewRows(Database db,Profile input,out List<string> skipped)
        {
            input.RequireValid();var copy=Copy(input);skipped=new List<string>();
            using(var tr=db.TransactionManager.StartOpenCloseTransaction())
            {
                var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);var types=(LinetypeTable)tr.GetObject(db.LinetypeTableId,OpenMode.ForRead);
                foreach(var row in copy.Rows.ToList())
                {
                    if(layers.Has(copy.LayerNameFor(row))||types.Has(copy.NameFor(row))||layers.Has(copy.LegacyNameFor(row))||types.Has(copy.LegacyNameFor(row)))
                    {skipped.Add("Layer: "+copy.LayerNameFor(row)+" | Linetype: "+copy.NameFor(row)+" (ít nhất một tên đã tồn tại)");copy.Rows.Remove(row);}
                }
            }
            copy.RefreshNames();return copy;
        }
        public static string Plan(Database db,Profile input)
        {
            List<string> skipped;var p=NewRows(db,input,out skipped);
            return "THƯ VIỆN / TẠO MỚI — không cập nhật tên đã tồn tại\r\n"+String.Join("\r\n",p.Rows.Select(r=>"Tạo mới: "+p.LayerNameFor(r)))+"\r\n"+String.Join("\r\n",skipped.Select(n=>"Bỏ qua / giữ nguyên DWG: "+n))+"\r\nMuốn sửa hiện hành: trở lại Quản lý đường nét → Chi tiết / Sửa.";
        }
        public static int Apply(Database db,Profile input)
        {
            List<string> skipped;var p=NewRows(db,input,out skipped);if(p.Rows.Count==0)return 0;
            // Every created batch has a private style. Existing styles are never reconfigured.
            RequireNewOnly(db,p);
            int created=SyncService.Apply(db,p);db.Plinegen=false;return created;
        }
        private static void RequireNewOnly(Database db,Profile p)
        {
            if(SyncService.Inspect(db,p).Any(r=>r.Layer!="Tạo mới"||r.Linetype!="Tạo mới"))throw new InvalidOperationException("Có tên đã tồn tại; đọc lại kế hoạch trước khi tạo. Không cập nhật hoặc đổi tên hiện hành.");
        }
        public static int Preview(Database db,Profile input,Point3d origin)
        {
            input.RequireValid();var p=Copy(input);string token=Guid.NewGuid().ToString("N");p.LayerPrefix="Mr.T_PV_"+token.Substring(0,10);
            // Isolate legacy MRG names too, so the older migration code cannot rename a live definition.
            p.UnitTag="PV"+token.Substring(0,12).ToUpperInvariant();foreach(var row in p.Rows){row.PrefixOverride=null;row.SuffixOverride=null;row.LinetypeNameOverride=null;}p.RefreshNames();RequireNewOnly(db,p);
            int created=SyncService.CreateSamples(db,p,origin);db.Plinegen=false;return created;
        }
    }
}
