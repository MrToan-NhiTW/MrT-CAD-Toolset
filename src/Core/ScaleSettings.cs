using System;
using System.Collections.Generic;
namespace MedGas2024.Core
{
    public sealed class ScaleOptions
    {
        public bool ApplyScale{get;set;} public double EntityScale{get;set;}
        public bool ApplyWidth{get;set;} public double GlobalWidth{get;set;}
        public void Validate(){if(!ApplyScale&&!ApplyWidth)throw new ArgumentException("Chọn ít nhất Scale hoặc Global Width.");if(ApplyScale&&(!Profile.Finite(EntityScale)||EntityScale<=0||EntityScale>1e6))throw new ArgumentException("Scale phải lớn hơn 0 và không quá 1000000.");if(ApplyWidth&&(!Profile.Finite(GlobalWidth)||GlobalWidth<0||GlobalWidth>1e9))throw new ArgumentException("Global Width phải từ 0 đến 1000000000.");}
        public string Signature{get{return ApplyScale+"|"+Profile.N(EntityScale)+"|"+ApplyWidth+"|"+Profile.N(GlobalWidth);}}
    }
    public sealed class ScaleContext
    {
        public double UnitMm=1,GlobalLtscale=1;
        public bool LayoutNormalized;
        public string Note="Giả định đơn vị Model là mm; tỷ lệ 1:100 là giá trị bắt đầu, cần chỉnh theo bản in.";
    }
    public sealed class PaperSuggestion
    {
        public string Paper;public double TextMm,WidthMm;
        public static List<PaperSuggestion> Defaults(){return new List<PaperSuggestion>{new PaperSuggestion{Paper="A0",TextMm=3.5,WidthMm=.35},new PaperSuggestion{Paper="A1",TextMm=3,WidthMm=.30},new PaperSuggestion{Paper="A2",TextMm=2.8,WidthMm=.25},new PaperSuggestion{Paper="A3",TextMm=2.5,WidthMm=.20},new PaperSuggestion{Paper="A4",TextMm=2.5,WidthMm=.18}};}
        public override string ToString(){return Paper;}
        public static ScaleOptions Estimate(double textMm,double widthMm,double denominator,double unitMm,double nativeTextHeight,double globalLtscale,bool layoutNormalized)
        {
            foreach(double n in new[]{textMm,denominator,unitMm,nativeTextHeight,globalLtscale})if(!Profile.Finite(n)||n<=0)throw new ArgumentException("Thông số ước lượng phải dương, hữu hạn.");
            var value=new ScaleOptions{ApplyScale=true,ApplyWidth=true,EntityScale=textMm*(layoutNormalized?1:denominator/unitMm)/nativeTextHeight/globalLtscale,GlobalWidth=widthMm*denominator/unitMm};value.Validate();return value;
        }
    }
}
