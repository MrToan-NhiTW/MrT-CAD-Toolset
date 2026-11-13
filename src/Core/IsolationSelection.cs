using System.Collections.Generic;
using System.Linq;
namespace MedGas2024.Core
{
    // Preserve the complete working selection, including items without a pipe label.
    public static class IsolationSelection
    {
        public static T[] AfterAssignment<T>(IEnumerable<T> isolated,IEnumerable<T> family,IEnumerable<T> assigned)
        {
            return (isolated??Enumerable.Empty<T>()).Concat(family??Enumerable.Empty<T>()).Concat(assigned??Enumerable.Empty<T>()).Distinct().ToArray();
        }
        public static T[] ResolveSplit<T>(IEnumerable<T> roots,IDictionary<T,T[]> splits,System.Func<T,bool> isLive)
        {
            var seen=new HashSet<T>();var output=new List<T>();System.Action<T> visit=null;
            visit=id=>{if(!seen.Add(id))return;if(isLive(id)){output.Add(id);return;}T[] children;if(splits.TryGetValue(id,out children))foreach(var child in children)visit(child);};
            foreach(var id in roots??Enumerable.Empty<T>())visit(id);return output.ToArray();
        }
    }
}
