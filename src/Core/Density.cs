using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace MedGas2024.Core
{
    public sealed class DensityOptions
    {
        public bool PaperMode; public double Multiplier=2, PaperMm=100, Denominator=100;
        public void Validate(){if(!Profile.Finite(Multiplier)||Multiplier<=0||Multiplier>1000||!Profile.Finite(PaperMm)||PaperMm<=0||PaperMm>10000||!Profile.Finite(Denominator)||Denominator<=0||Denominator>100000)throw new ArgumentException("Nhịp, khoảng giấy và tỷ lệ phải dương, hữu hạn và trong giới hạn.");}
        public string Signature{get{return PaperMode+"|"+Multiplier.ToString("R",CultureInfo.InvariantCulture)+"|"+PaperMm.ToString("R",CultureInfo.InvariantCulture)+"|"+Denominator.ToString("R",CultureInfo.InvariantCulture);}}
    }
    public sealed class DensityRow
    {
        public bool Selected{get;set;} public string Key{get;set;} public bool IsOverride{get;set;} public string Scope{get{return IsOverride?"Gán riêng":"Theo layer";}} public string Layer{get;set;} public string Type{get;set;} public string Text{get;set;}
        public int Direct{get;set;} public int MatchingOverride{get;set;} public int OtherOverride{get;set;} public int MrTOverride{get;set;} public int Layout{get;set;} public int Nested{get;set;} public int NonCurve{get;set;}
        public double Period{get;set;} public double Dash{get;set;} public double Gap{get;set;} public double MinScale{get;set;} public double MaxScale{get;set;}
        public string State{get;set;} public string Result{get;set;} public string Before{get;set;} public string After{get;set;}
        public string Fingerprint{get;set;} public DrawingLayerInfo Source{get;set;}
        public bool PeriodCompatible{get;set;}
        public string TypeHandle{get;set;} public string TypeFingerprint{get;set;} public System.Collections.Generic.List<string> Handles=new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<DensityMember> Members=new System.Collections.Generic.List<DensityMember>();
        public DensityRow(){MinScale=Double.PositiveInfinity;MaxScale=Double.NegativeInfinity;}
    }
    public sealed class DensityMember{public string Handle;public double Scale;}
    public sealed class DensityScaleGroup{public double Scale;public System.Collections.Generic.List<string> Handles=new System.Collections.Generic.List<string>();}
    public static class DensityMath
    {
        public static System.Collections.Generic.List<DensityScaleGroup> ScaleGroups(System.Collections.Generic.IEnumerable<DensityMember> members)
        {
            var groups=new System.Collections.Generic.List<DensityScaleGroup>();
            foreach(var member in System.Linq.Enumerable.OrderBy(members,x=>x.Scale)){if(!Profile.Finite(member.Scale)||member.Scale<=0)throw new ArgumentException("Có đường có linetype scale không hợp lệ.");var group=System.Linq.Enumerable.FirstOrDefault(groups,x=>SameScale(x.Scale,member.Scale));if(group==null){group=new DensityScaleGroup{Scale=member.Scale};groups.Add(group);}group.Handles.Add(member.Handle);}
            return System.Linq.Enumerable.ToList(System.Linq.Enumerable.ThenBy(System.Linq.Enumerable.OrderByDescending(groups,x=>x.Handles.Count),x=>x.Scale));
        }
        // Derive a readable family name; old _D_<random> runs must not accumulate.
        // A new suffix is allocated by the caller after checking the DWG table.
        public static string ReadableStem(string layer,string type,string label,bool isOverride)
        {
            string old=Regex.Replace(type??"",@"(?:_D_[0-9a-f]{8})+$","",RegexOptions.IgnoreCase);
            old=Regex.Replace(old,@"(?:_SP_[0-9]+)$","",RegexOptions.IgnoreCase);
            bool hashed=Regex.IsMatch(old,@"^Mr\.T_(?:R0|Net)_[0-9a-f]{16}(?:_[0-9a-f]{8})?$",RegexOptions.IgnoreCase);
            string stem=!isOverride&&IsMrT(layer)?layer:!hashed&&IsMrT(old)?old:!String.IsNullOrWhiteSpace(label)?"Mr.T_"+label:IsMrT(layer)?layer:"Mr.T_PIPE";
            stem=stem.Replace('Đ','D').Replace('đ','d').Normalize(NormalizationForm.FormD);
            var sb=new StringBuilder();bool underscore=false;
            foreach(char c in stem){if(CharUnicodeInfo.GetUnicodeCategory(c)==UnicodeCategory.NonSpacingMark)continue;bool allowed=c<128&&(Char.IsLetterOrDigit(c)||c=='.'||c=='_');char next=allowed?c:'_';if(next=='_'&&underscore)continue;sb.Append(next);underscore=next=='_';}
            string result=sb.ToString().Trim('_');if(!IsMrT(result))result="Mr.T_"+result;if(result.Length>180)result=result.Substring(0,180).TrimEnd('_');return result.Length>5?result:"Mr.T_PIPE";
        }
        public static bool IsMrT(string name){return !String.IsNullOrEmpty(name)&&(name.StartsWith("Mr.T_",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Mr.T.",StringComparison.OrdinalIgnoreCase)||name.Equals("Mr.T",StringComparison.OrdinalIgnoreCase));}
        public static bool SameScale(double min,double max){return Profile.Finite(min)&&Profile.Finite(max)&&min>0&&Math.Abs(max-min)<=Math.Max(Math.Abs(min),Math.Abs(max))*1e-8;}
        public static double DashFor(DensityOptions o,double period,double gap,double entityScale,double globalScale)
        {
            o.Validate();if(!Profile.Finite(period)||period<=0||!Profile.Finite(gap)||gap<=0)throw new ArgumentException("Chu kỳ nét không hợp lệ.");
            double target;if(o.PaperMode){if(!Profile.Finite(entityScale)||entityScale<=0||!Profile.Finite(globalScale)||globalScale<=0)throw new ArgumentException("Scale đường hoặc LTSCALE không hợp lệ.");target=o.PaperMm*o.Denominator/(entityScale*globalScale);}else target=period*o.Multiplier;
            double dash=target-gap;if(!Profile.Finite(dash)||dash<=1e-9||dash>1e9)throw new ArgumentException("Nhịp yêu cầu quá ngắn/dài so với khoảng chứa chữ; chọn nhịp khác.");return dash;
        }
        public static double EstimatePaper(double period,double scale,double globalScale,double denominator){return period*scale*globalScale/denominator;}
    }
}
