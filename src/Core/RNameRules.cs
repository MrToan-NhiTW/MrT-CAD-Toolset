using System;
using System.Text.RegularExpressions;
namespace MedGas2024.Core
{
    public static class RNameRules
    {
        public static bool CanonicalLayer(string layer){return !String.IsNullOrEmpty(layer)&&Regex.IsMatch(layer,@"^Mr\.T_.+_D[0-9]+(?:_[A-Za-z0-9_.-]+)?$",RegexOptions.IgnoreCase);}
        public static bool OldName(string name){return DensityMath.IsMrT(name)&&(Regex.IsMatch(name,@"^Mr\.T_(?:R0|Net)_[0-9a-f]{16}(?:_[0-9a-f]{8})?(?:_(?:D|E)_[0-9a-f]{8})*(?:_SP_[0-9]+)?$",RegexOptions.IgnoreCase)||Regex.IsMatch(name,@"(?:_(?:D|E)_[0-9a-f]{8})+$",RegexOptions.IgnoreCase)||Regex.IsMatch(name,@"_SP_[0-9]+$",RegexOptions.IgnoreCase));}
        public static string LabelKey(string label){return LabelRules.Canonical(label).Trim().ToUpperInvariant();}
    }
    public sealed class RNameReviewRow
    {
        public bool Use{get;set;}public string Key{get;set;}public string Label{get;set;}public string StandardLayer{get;set;}public string Target{get;set;}public string Source{get;set;}
        public int Layers{get;set;}public int Entities{get;set;}public int Nested{get;set;}public int Variants{get;set;}public string State{get;set;}public string Details{get;set;}
        public bool Ready{get;set;}
    }
}
