using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MedGas2024.Core
{
    public sealed class DrawingLayerInfo
    {
        public bool UseLabelIdentity{get;set;}
        public bool HasFamilyDefaults{get;set;}public double FamilyScale{get;set;}public double? FamilyWidth{get;set;}public string FamilySource{get;set;}
        public bool Assignable{get;set;} public bool CanAssign{get{return Writable||Assignable;}}
        public string LayerName{get;set;} public string LinetypeName{get;set;}
        public string Text{get;set;} public string Description{get;set;} public string ColorText{get;set;}
        public string Font{get;set;} public string Details{get;set;} public string State{get;set;}
        public string Fingerprint{get;set;} public bool Managed{get;set;} public bool Writable{get;set;}
        public bool Editable{get;set;} public int UsedCount{get;set;} public int OverrideCount{get;set;}
        public int SharedLayers{get;set;} public int AffectedCount{get;set;}
        public double Dash{get;set;} public double Before{get;set;} public double After{get;set;}
        public double Height{get;set;} public double X{get;set;} public double Y{get;set;}
        public int Aci{get;set;} public double NativeTextHeight{get;set;}
        public string Display{get{return String.IsNullOrEmpty(Text)?LayerName:Text;}}
    }
    public sealed class DefinitionEdit
    {
        public string Text{get;set;} public string Description{get;set;}
        public double Dash{get;set;} public double Before{get;set;} public double After{get;set;}
        public double Height{get;set;} public double X{get;set;} public double Y{get;set;}
        public bool AutoFit{get;set;} public bool ChangeColor{get;set;} public int Aci{get;set;}
        public void Validate(bool editable)
        {
            if(ChangeColor&&(Aci<1||Aci>255))throw new ArgumentException("Màu ACI phải từ 1 đến 255.");
            if((Description??"").Length>200||(Description??"").Any(Char.IsControl))throw new ArgumentException("Mô tả tối đa 200 ký tự, một dòng.");
            if(!editable)return;
            if(String.IsNullOrWhiteSpace(Text)||Text.Length>80||Text.Any(c=>Char.IsControl(c)||c=='"'||c=='['||c==']'||c==','))throw new ArgumentException("Chữ trên nét: tối đa 80 ký tự; không dùng dấu ngoặc kép, phẩy hoặc ngoặc vuông.");
            foreach(double n in new[]{Dash,Before,After,Height})if(!Profile.Finite(n)||n<=0||n>1e9)throw new ArgumentException("Nét, khoảng trống và cao chữ phải dương, hữu hạn.");
            if(!Profile.Finite(X)||!Profile.Finite(Y)||Math.Abs(X)>1e9||Math.Abs(Y)>1e9)throw new ArgumentException("Offset không hợp lệ.");
        }
    }
    public static class DrawingSearch
    {
        public static bool Preview(string name){return (name??"").StartsWith("Mr.T_PV_",StringComparison.OrdinalIgnoreCase);}
        private static string Key(string text)
        {
            return new string((text??"").Replace('đ','d').Replace('Đ','D').Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark&&Char.IsLetterOrDigit(c)).Select(Char.ToUpperInvariant).ToArray());
        }
        public static List<DrawingLayerInfo> Filter(IEnumerable<DrawingLayerInfo> items,string query,string prefix,string suffix,bool managedOnly,bool usedOnly)
        {
            var terms=Regex.Split(query??"",@"\s+").Select(Key).Where(s=>s.Length>0).ToArray();
            return items.Where(r=>!Preview(r.LayerName)&&(!managedOnly||r.Managed)&&(!usedOnly||r.UsedCount>0)&&
                (r.LayerName??"").StartsWith(prefix??"",StringComparison.OrdinalIgnoreCase)&&(r.LayerName??"").EndsWith(suffix??"",StringComparison.OrdinalIgnoreCase)&&
                terms.All(t=>SearchKey(r).Contains(t))).OrderBy(r=>Key(r.Text)).ThenBy(r=>r.LayerName,StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static List<DrawingLayerInfo> Sort(IEnumerable<DrawingLayerInfo> rows,string column,bool ascending)
        {
            Func<DrawingLayerInfo,object> value=r=>column=="Text"?(object)Regex.Replace(r.Text??"",@"[0-9]+",m=>m.Value.PadLeft(16,'0')):column=="ColorText"?(object)Regex.Replace(r.ColorText??"",@"[0-9]+",m=>m.Value.PadLeft(16,'0')):typeof(DrawingLayerInfo).GetProperty(column??"LayerName")==null?(object)r.LayerName:typeof(DrawingLayerInfo).GetProperty(column).GetValue(r,null);
            var sorted=rows.ToList();sorted.Sort(delegate(DrawingLayerInfo a,DrawingLayerInfo b){object x=value(a),y=value(b);int n=x is string?StringComparer.OrdinalIgnoreCase.Compare((string)x,(string)y):Comparer<object>.Default.Compare(x,y);if(n==0)n=StringComparer.OrdinalIgnoreCase.Compare(a.LayerName,b.LayerName);return ascending?n:-n;});return sorted;
        }
        private static string SearchKey(DrawingLayerInfo r)
        {
            string extra="";var match=Regex.Match(r.Text??"",@"^D(\d+)[._](.+)$",RegexOptions.IgnoreCase);
            if(match.Success)extra=match.Groups[2].Value+match.Groups[1].Value;
            return Key(r.LayerName+" "+r.LinetypeName+" "+r.Text+" "+r.Description+" "+extra);
        }
    }
}
