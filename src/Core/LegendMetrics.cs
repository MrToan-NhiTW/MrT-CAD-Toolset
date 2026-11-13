using System;
using System.Collections.Generic;
using System.Linq;
namespace MedGas2024.Core
{
    public static class LegendMetrics
    {
        // mode 0 = custom; 1 = Model mm; 2 = Model m; 3 = Layout mm.
        public static double PaperFactor(int mode,double denominator){if(!Profile.Finite(denominator)||denominator<=0)throw new ArgumentException("Tỷ lệ in phải lớn hơn 0.");return mode==2?denominator/1000:mode==3?1:denominator;}
        static bool Near(double a,double b){return Math.Abs(a-b)<=Math.Max(Math.Abs(a),Math.Abs(b))*1e-5;}
        public static void ReadNative(LegendSpec s,double width,double symbol,double title,double body,double header)
        {
            double oldTitle=s.TitleHeight,oldBody=s.BodyHeight;var actual=new[]{width,symbol,title,body,header};var expected=new[]{s.NativeWidth>0?s.NativeWidth:s.Width*s.DrawingFactor,s.NativeSymbol>0?s.NativeSymbol:s.SymbolWidth*s.DrawingFactor,s.NativeTitle>0?s.NativeTitle:s.TitleHeight*s.DrawingFactor,s.NativeBody>0?s.NativeBody:s.BodyHeight*s.DrawingFactor,s.NativeHeader>0?s.NativeHeader:Math.Max(s.BodyHeight*2.4,7)*s.DrawingFactor};
            var ratios=actual.Select((n,i)=>Profile.Finite(n)&&n>0&&expected[i]>0?n/expected[i]:0).Where(n=>n>0).ToArray();double scale=1;
            // Three agreeing independent dimensions distinguish SCALE from a column stretch.
            foreach(double candidate in ratios)if(!Near(candidate,1)&&ratios.Count(r=>Near(r,candidate))>=3){scale=candidate;break;}
            if(!Near(scale,1)){s.DrawingFactor*=scale;s.UnitMode=0;s.LoadNote="Đã nhận bảng SCALE ×"+Profile.N(scale)+"; giữ kích thước hiện tại, quy đổi lại hệ số bảng.";}
            if(!Profile.Finite(s.DrawingFactor)||s.DrawingFactor<1e-9||s.DrawingFactor>1e9)throw new ArgumentException("Hệ số bảng sau SCALE ngoài phạm vi hỗ trợ.");
            s.Width=width/s.DrawingFactor;s.SymbolWidth=symbol/s.DrawingFactor;if(title>0)s.TitleHeight=!Near(scale,1)&&Near(title,expected[2])?oldTitle:title/s.DrawingFactor;if(body>0)s.BodyHeight=!Near(scale,1)&&Near(body,expected[3])?oldBody:body/s.DrawingFactor;
        }
    }
    public static class PointGlyph
    {
        public static List<LegendStroke> Create(int mode)
        {
            var lines=new List<LegendStroke>();int centre=mode&31;if(centre==1)return lines;
            Action<double[]> add=p=>lines.Add(new LegendStroke{Points=p});
            if(centre==2){add(new double[]{-.5,0,.5,0});add(new double[]{0,-.5,0,.5});}
            else if(centre==3){add(new double[]{-.5,-.5,.5,.5});add(new double[]{-.5,.5,.5,-.5});}
            else if(centre==4)add(new double[]{0,0,0,.5});
            if((mode&32)!=0){var circle=new List<double>();for(int i=0;i<=48;i++){circle.Add(.5*Math.Cos(i*Math.PI/24));circle.Add(.5*Math.Sin(i*Math.PI/24));}add(circle.ToArray());}
            if((mode&64)!=0)add(new double[]{-.5,-.5,.5,-.5,.5,.5,-.5,.5,-.5,-.5});
            if(centre==0)add(new double[]{-.03,0,0,.03,.03,0,0,-.03,-.03,0});return lines;
        }
    }
}
