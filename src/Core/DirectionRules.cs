using System;
using System.Collections.Generic;
namespace MedGas2024.Core
{
    public sealed class DirectionOptions
    {
        public bool Restore{get;set;}
        public double HeightOverride{get;set;}
        public double ViewFactor{get;set;}
        public double PaddingRatio{get;set;}
        public double CornerRatio{get;set;}
        public double RepeatMultiplier{get;set;}
        public DirectionOptions(){ViewFactor=1;PaddingRatio=.25;CornerRatio=1.5;RepeatMultiplier=1;}
        public void Validate(){if(PaddingRatio<.05||PaddingRatio>2||CornerRatio<.1||CornerRatio>20||RepeatMultiplier<.1||RepeatMultiplier>20)throw new ArgumentException("Khoảng hở/né cút/nhịp nhãn ngoài phạm vi.");if(!Profile.Finite(HeightOverride)||HeightOverride<0||HeightOverride>1e9)throw new ArgumentException("Cao chữ không hợp lệ.");foreach(double v in new[]{ViewFactor,PaddingRatio,CornerRatio,RepeatMultiplier})if(!Profile.Finite(v)||v<=0||v>1e9)throw new ArgumentException("Khoảng hở/nhịp nhãn không hợp lệ.");}
        public string Signature{get{return Restore+"|"+Profile.N(HeightOverride)+"|"+Profile.N(ViewFactor)+"|"+Profile.N(PaddingRatio)+"|"+Profile.N(CornerRatio)+"|"+Profile.N(RepeatMultiplier);}}
    }
    public static class DirectionRules
    {
        public static double Rotation(double dx,double dy)
        {
            double length=Math.Sqrt(dx*dx+dy*dy);if(!Profile.Finite(length)||length<=1e-12)throw new ArgumentException("Đoạn không có chiều dài.");
            double tolerance=Math.Sin(.25*Math.PI/180);
            if(Math.Abs(dx)/length<=tolerance)return -Math.PI/2; // Top to bottom in WCS XY.
            if(Math.Abs(dy)/length<=tolerance)return 0;
            double a=Math.Atan2(dy,dx);if(dx<0)a+=Math.PI;if(a>Math.PI)a-=2*Math.PI;return a;
        }
        public static List<double> Centers(double length,double labelLength,double height,double nativePeriod,DirectionOptions options)
        {
            options.Validate();foreach(double n in new[]{length,labelLength,height,nativePeriod})if(!Profile.Finite(n)||n<=0)throw new ArgumentException("Kích thước nhãn/đoạn không hợp lệ.");
            double half=labelLength/2+options.PaddingRatio*height,margin=options.CornerRatio*height;
            var result=new List<double>();double available=length-2*(half+margin);if(available<0)return result;
            double step=Math.Max(nativePeriod*options.RepeatMultiplier,2*half+2*height);double wanted=1+Math.Floor(available/step);if(wanted>2000)throw new ArgumentException("Quá nhiều nhãn trên một đoạn; tăng nhịp nhãn.");int count=(int)wanted;if(count>2000)throw new ArgumentException("Quá nhiều nhãn trên một đoạn; tăng nhịp nhãn.");
            double start=(length-(count-1)*step)/2;for(int i=0;i<count;i++)result.Add(start+i*step);return result;
        }
    }
}
