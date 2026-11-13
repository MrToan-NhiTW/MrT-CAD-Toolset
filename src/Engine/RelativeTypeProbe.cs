using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.DatabaseServices;
namespace MedGas2024.Engine
{
    // AutoCAD 2024's managed API does not expose the upright bit. Read native
    // DXF flags from the original linetype record without cloning or modifying it.
    public static class RelativeTypeProbe
    {
        public static Dictionary<string,bool> ParseFlags(string[] lines)
        {
            var output=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);bool type=false;string handle=null;var flags=new List<int>();
            Action flush=delegate{if(handle!=null){var texts=flags.Where(x=>(x&2)!=0).ToList();output[handle]=texts.Count==1&&texts[0]==2;}};
            for(int i=0;i+1<lines.Length;i+=2){string c=lines[i].Trim(),v=lines[i+1].Trim();if(c=="0"){if(type)flush();type=v=="LTYPE";handle=null;flags.Clear();}else if(type&&c=="5")handle=v;else if(type&&c=="74"){int n;if(Int32.TryParse(v,NumberStyles.Integer,CultureInfo.InvariantCulture,out n))flags.Add(n);}}
            if(type)flush();return output;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct AdsName { public IntPtr A; public IntPtr B; }
        // AutoCAD 2024 x64 exports, verified against the installed product.
        [DllImport("acdb24.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="?acdbGetAdsName@@YA?AW4ErrorStatus@Acad@@AEAY01_JVAcDbObjectId@@@Z")]
        static extern Autodesk.AutoCAD.Runtime.ErrorStatus GetAdsName(out AdsName name,ObjectId id);
        [DllImport("accore.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="acdbEntGet")]
        static extern IntPtr EntGet(ref AdsName name);
        public static Dictionary<ObjectId,bool> Read(Database db,IEnumerable<ObjectId> typeIds)
        {
            var ids=typeIds.Where(x=>!x.IsNull&&x.IsValid&&!x.IsErased).Distinct().ToArray();var output=new Dictionary<ObjectId,bool>();if(ids.Length==0)return output;
            foreach(var id in ids)
            {
                if(id.Database.UnmanagedObject!=db.UnmanagedObject)continue;
                AdsName name;if(GetAdsName(out name,id)!=Autodesk.AutoCAD.Runtime.ErrorStatus.OK)continue;
                var ptr=EntGet(ref name);if(ptr==IntPtr.Zero)continue;
                using(var data=ResultBuffer.Create(ptr,true))
                {var texts=data.AsArray().Where(x=>x.TypeCode==74).Select(x=>Convert.ToInt32(x.Value)).Where(x=>(x&2)!=0).ToArray();output[id]=texts.Length==1&&texts[0]==2;}
            }
            return output;
        }
    }
}
