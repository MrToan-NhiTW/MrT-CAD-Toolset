using System;
using System.Collections.Generic;
using System.Linq;

namespace MedGas2024.Core
{
    // Splits a room name into at most N lines with the narrowest possible column:
    // words stay in order, each line is a run of whole words, and the widest line is minimised.
    public static class RoomLayout
    {
        // Preferred longest line, in Text heights, before another line is used.
        public const double LineTarget=10;
        public static string[] Words(string text){return (text??"").Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries);}
        // Width of the widest line of the best split, given each word's width and the width of a space.
        public static double ColumnWidth(IList<double> words,double space,int maxLines)
        {
            int n=words.Count;if(n==0)return 0;
            Func<int,int,double> line=(i,j)=>{double w=0;for(int k=i;k<=j;k++)w+=words[k];return w+space*(j-i);};
            if(maxLines<=1||n==1)return line(0,n-1);
            int lines=Math.Min(maxLines,n);
            // best[k,j]: smallest widest line when the first j+1 words use k+1 lines.
            var best=new double[lines,n];
            for(int j=0;j<n;j++)best[0,j]=line(0,j);
            for(int k=1;k<lines;k++)
                for(int j=0;j<n;j++)
                {
                    best[k,j]=best[k-1,j];
                    for(int i=1;i<=j;i++)best[k,j]=Math.Min(best[k,j],Math.Max(best[k-1,i-1],line(i,j)));
                }
            return best[lines-1,n-1];
        }
        // The same best split, as the index of the first word of each line.
        public static int[] Breaks(IList<double> words,double space,int maxLines)
        {
            int n=words.Count;if(n==0)return new int[0];
            int lines=Math.Max(1,Math.Min(maxLines,n));
            Func<int,int,double> line=(i,j)=>{double w=0;for(int k=i;k<=j;k++)w+=words[k];return w+space*(j-i);};
            var best=new double[lines,n];var from=new int[lines,n];
            for(int j=0;j<n;j++){best[0,j]=line(0,j);from[0,j]=0;}
            for(int k=1;k<lines;k++)
                for(int j=0;j<n;j++)
                {
                    best[k,j]=best[k-1,j];from[k,j]=-1;
                    for(int i=1;i<=j;i++){double v=Math.Max(best[k-1,i-1],line(i,j));if(v<best[k,j]-1e-12){best[k,j]=v;from[k,j]=i;}}
                }
            var starts=new List<int>();int end=n-1,kk=lines-1;
            while(kk>=0)
            {
                int s=from[kk,end];
                if(s<0){kk--;continue;}
                starts.Insert(0,s);end=s-1;kk--;if(end<0)break;
            }
            if(starts.Count==0||starts[0]!=0)starts.Insert(0,0);
            return starts.Distinct().ToArray();
        }
        // Spaces and line breaks collapsed, so a name split over lines still compares equal.
        public static string Plain(string text){return String.Join(" ",Words(text));}
    }
}
