using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
namespace MedGas2024.Core
{
    public static class LabelBrowse
    {
        public static bool LayerMatches(string layer,string prefix,string suffix){return (layer??"").StartsWith((prefix??"").Trim(),StringComparison.OrdinalIgnoreCase)&&(layer??"").EndsWith((suffix??"").Trim(),StringComparison.OrdinalIgnoreCase);}
        public static List<LabelMember> FilterMembers(LabelCatalog catalog,string prefix,string suffix){return catalog.Members.Where(m=>LayerMatches(m.Layer,prefix,suffix)).ToList();}
        static string Natural(string value){return Regex.Replace(value??"",@"[0-9]+",m=>m.Value.PadLeft(16,'0'));}
        public static List<LabelGroup> Rows(LabelCatalog catalog,bool system,string query,string prefix,string suffix,string column,bool ascending)
        {
            var filtered=new LabelCatalog();filtered.Members.AddRange(FilterMembers(catalog,prefix,suffix));var groups=filtered.Groups(system).Where(g=>LabelRules.Matches(query,g.Label,filtered.Members.Where(m=>LabelRules.Key(m.Text,system)==g.Key).Select(m=>m.Text))).ToList();
            Comparison<LabelGroup> compare=delegate(LabelGroup a,LabelGroup b)
            {
                int result;if(column=="Label")result=StringComparer.OrdinalIgnoreCase.Compare(Natural(a.Label),Natural(b.Label));else if(column=="Scales")result=a.MinScale.CompareTo(b.MinScale);else{var prop=typeof(LabelGroup).GetProperty(column??"Label");var x=prop==null?a.Label:prop.GetValue(a,null);var y=prop==null?b.Label:prop.GetValue(b,null);result=x is string?StringComparer.OrdinalIgnoreCase.Compare((string)x,(string)y):((IComparable)x).CompareTo(y);}if(result==0)result=StringComparer.OrdinalIgnoreCase.Compare(a.Key,b.Key);return ascending?result:-result;
            };groups.Sort(compare);return groups;
        }
    }
}
