using System;
using System.Collections.Generic;
using System.Linq;
namespace MedGas2024.Core
{
    public enum PickMode { System, Similar, Combined }
    public static class PickCombination
    {
        // Each branch receives the original samples, never the other branch's expanded results.
        public static T[] Expand<T>(IEnumerable<T> samples,Func<T[],IEnumerable<T>> system,Func<T[],IEnumerable<T>> similar)
        {
            var seeds=samples.Distinct().ToArray();if(seeds.Length==0)return seeds;
            var family=system((T[])seeds.Clone()).ToArray();var matching=similar((T[])seeds.Clone()).ToArray();
            return seeds.Concat(family).Concat(matching).Distinct().ToArray();
        }
    }
}
