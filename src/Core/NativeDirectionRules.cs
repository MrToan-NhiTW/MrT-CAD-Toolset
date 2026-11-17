using System;
using System.Collections.Generic;
namespace MedGas2024.Core
{
    public static class NativeDirectionRules
    {
        public sealed class Run { public int Start,End,Sign; }
        public static void ReverseWidths(double[] starts,double[] ends,out double[] reversedStarts,out double[] reversedEnds)
        {
            if(starts==null||ends==null||starts.Length!=ends.Length)throw new ArgumentException("Dữ liệu độ rộng không hợp lệ.");
            reversedStarts=new double[starts.Length];reversedEnds=new double[ends.Length];
            for(int i=0;i<starts.Length;i++){int j=starts.Length-1-i;reversedStarts[i]=ends[j];reversedEnds[i]=starts[j];}
        }
        public static List<Run> Runs(int[] signs, bool splitEverySegment=false)
        {
            var output=new List<Run>();if(signs==null)throw new ArgumentNullException("signs");int i=0;
            while(i<signs.Length){if(signs[i]!=1&&signs[i]!=-1)throw new ArgumentException("Đoạn không có hướng hợp lệ.");int end=i+1;while(!splitEverySegment&&end<signs.Length&&signs[end]==signs[i])end++;output.Add(new Run{Start=i,End=end,Sign=signs[i]});i=end;}return output;
        }
        // Lines a little off vertical (snap/import error) used to follow left→right and read
        // bottom→top, while true verticals read top→bottom, so neighbours looked inconsistent.
        public const double VerticalToleranceDegrees=2;
        // The drawing plane is WCS XY. Near-vertical lines use the vertical rule.
        public static int Sign(double dx,double dy)
        {
            double length=Math.Sqrt(dx*dx+dy*dy);
            if(!Profile.Finite(length)||length<=1e-9)return 0;
            if(Math.Abs(dx)/length<=Math.Sin(VerticalToleranceDegrees*Math.PI/180))return dy<0?1:-1;
            return dx>0?1:-1;
        }
        // Zero-length segments (duplicate vertices) have no direction; they take the sign of
        // the neighbouring segment instead of rejecting the whole polyline.
        public static int[] FillZeroSigns(int[] signs)
        {
            if(signs==null)throw new ArgumentNullException("signs");var output=(int[])signs.Clone();int known=0;
            foreach(int s in output)if(s!=0){known=s;break;}if(known==0)return output;
            for(int i=0;i<output.Length;i++){if(output[i]==0)output[i]=known;else known=output[i];}
            return output;
        }
    }
}
