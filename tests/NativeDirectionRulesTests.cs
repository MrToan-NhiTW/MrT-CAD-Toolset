using System;
using System.Linq;
using MedGas2024.Core;
class NativeDirectionRulesTests
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    static void Main()
    {
        Check(NativeDirectionRules.Sign(10,0)==1&&NativeDirectionRules.Sign(-10,0)==-1,"horizontal directions");
        Check(NativeDirectionRules.Sign(0,-10)==1&&NativeDirectionRules.Sign(0,10)==-1,"vertical directions");
        Check(NativeDirectionRules.Sign(10,10)==1&&NativeDirectionRules.Sign(-10,-10)==-1,"diagonal directions");
        Check(NativeDirectionRules.Sign(0,0)==0,"zero length rejected");
        Check(NativeDirectionRules.Sign(.001,-10)==1&&NativeDirectionRules.Sign(-.001,10)==-1,"near vertical tolerance");
        Check(NativeDirectionRules.Sign(10,0)!=NativeDirectionRules.Sign(0,10),"incompatible L polyline detected");
        var runs=NativeDirectionRules.Runs(new[]{-1,-1,1,1,-1});Check(runs.Count==3&&runs[0].Start==0&&runs[0].End==2&&runs[1].Start==2&&runs[1].End==4&&runs[2].Start==4&&runs[2].End==5&&runs.Select(x=>x.Sign).SequenceEqual(new[]{-1,1,-1}),"mixed polyline grouped at direction changes without losing segments");
        var generated=NativeDirectionRules.Runs(new[]{-1,-1,1},true);Check(generated.Count==3&&generated.Select(x=>x.End-x.Start).All(n=>n==1)&&generated.Select(x=>x.Sign).SequenceEqual(new[]{-1,-1,1}),"continuous generation uses separate original segments with each direction retained");
        var sample=NativeDirectionRules.Runs(new[]{NativeDirectionRules.Sign(-1387,0),NativeDirectionRules.Sign(0,-5600)});Check(sample.Count==2&&sample[0].Sign==-1&&sample[1].Sign==1,"reported L route requires splitting and reversing its horizontal run only");
        double[] start,end;NativeDirectionRules.ReverseWidths(new[]{1.0,3.0,5.0},new[]{2.0,4.0,6.0},out start,out end);
        Check(start.SequenceEqual(new[]{6.0,4.0,2.0})&&end.SequenceEqual(new[]{5.0,3.0,1.0}),"reversed segments retain their original tapered geometry");
        Check(JoinMetadataPolicy.IsSupported(new[]{1001,1000},new[]{"AcDbBlockRepETag",""})&&!JoinMetadataPolicy.IsSupported(new[]{1001,1000},new[]{"OtherApp",""}),"joined empty ETag is supported; unknown XData remains protected");
    }
}
