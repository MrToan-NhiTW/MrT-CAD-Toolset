using System;
using System.Collections.Generic;
using System.Linq;
namespace MedGas2024.Core
{
    public sealed class JoinVertex { public double X,Y,Bulge,StartWidth,EndWidth; }
    public static class JoinGeometry
    {
        public static List<JoinVertex> Ordered(IList<JoinVertex> original,bool reverse)
        {
            if(!reverse)return original.Select(v=>new JoinVertex{X=v.X,Y=v.Y,Bulge=v.Bulge,StartWidth=v.StartWidth,EndWidth=v.EndWidth}).ToList();
            var list=new List<JoinVertex>();for(int i=original.Count-1;i>=0;i--){var p=original[i];var old=i>0?original[i-1]:null;list.Add(new JoinVertex{X=p.X,Y=p.Y,Bulge=old==null?0:-old.Bulge,StartWidth=old==null?0:old.EndWidth,EndWidth=old==null?0:old.StartWidth});}return list;
        }
        // Never move either original endpoint; a straight segment bridges a nonzero gap.
        // In particular, snapping an arc endpoint would change the source arc geometry.
        public static void Append(List<JoinVertex> result,IList<JoinVertex> next)
        {
            if(next==null||next.Count<2)throw new ArgumentException("A route needs at least two vertices");
            if(result.Count==0){result.AddRange(next);return;}
            var end=result[result.Count-1];var start=next[0];
            if(end.X==start.X&&end.Y==start.Y){end.Bulge=start.Bulge;end.StartWidth=start.StartWidth;end.EndWidth=start.EndWidth;result.AddRange(next.Skip(1));return;}
            end.Bulge=0;end.StartWidth=result.Count>1?result[result.Count-2].EndWidth:0;end.EndWidth=start.StartWidth;
            result.AddRange(next);
        }
    }
}
