using System;
using System.Collections.Generic;
using MedGas2024.Core;
class QuickJoinTests
{
    static int passed;
    static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS: "+name);}
    static JoinEndpoint P(int route,double x,double y,string style="A",double z=0,bool curved=false){return new JoinEndpoint{Route=route,X=x,Y=y,Z=z,Style=style,Curved=curved};}
    static void Main(){
        Check(JoinPlanner.Find(new[]{P(0,0,0),P(1,0,0)},0).Count==1,"Exact matching endpoints join");
        Check(JoinPlanner.Find(new[]{P(0,0,0),P(1,0.01,0)},0).Count==0,"Default exact mode does not move endpoints");
        Check(JoinPlanner.Find(new[]{P(0,0,0),P(1,0.01,0)},0.02).Count==1,"Explicit tolerance allows straight gap");
        Check(JoinPlanner.Find(new[]{P(0,0,0),P(1,0.5,0)},0.5).Count==1,"Default 0.5 gap is inclusive");
        Check(JoinPlanner.Find(new[]{P(0,0,0),P(1,0.50001,0)},0.5).Count==0,"Gap above 0.5 is not joined");
        Check(JoinPlanner.Find(new[]{P(0,0,0,curved:true),P(1,0.01,0)},0.02).Count==1,"Gap at an arc is joinable without snapping its endpoint");
        Check(JoinPlanner.Find(new[]{P(0,0,0),P(1,0,0),P(2,0,0)},0).Count==0,"Ambiguous branch is left separate");
        Check(JoinPlanner.Find(new[]{P(0,0,0,"A"),P(1,0,0,"B")},0).Count==0,"Different styles remain separate");
        Check(JoinPlanner.Find(new[]{P(0,0,0,z:1),P(1,0,0,z:2)},0).Count==0,"Different elevations remain separate in preserve mode");
        Check(JoinPlanner.Find(new[]{P(0,0,0),P(0,0,0)},0).Count==0,"No self match");
        var three=new[]{P(0,0,0),P(0,10,0),P(1,10,0),P(1,20,0),P(2,20,0),P(2,30,0)};
        Check(JoinPlanner.Find(three,0.5).Count==2,"Three consecutive routes yield two links and one chain");
        var threeChains=JoinPlanner.Chains(3,JoinPlanner.Find(three,0.5));Check(threeChains.Chains.Count==1&&threeChains.Chains[0].Count==3,"Production chain assembly merges three routes into one");
        var shortSegment=new[]{P(0,0,0),P(0,10,0),P(1,10,0),P(1,10.2,0),P(2,10.2,0),P(2,20,0)};
        Check(JoinPlanner.Find(shortSegment,0.5).Count==2,"A short middle segment joins at both exact endpoints even within tolerance");
        var shortChains=JoinPlanner.Chains(3,JoinPlanner.Find(shortSegment,0.5));Check(shortChains.Chains.Count==1&&shortChains.Chains[0].Count==3,"Short middle segment forms one production chain");
        var arc=new List<JoinVertex>{new JoinVertex{X=0,Y=0,Bulge=.5,StartWidth=2,EndWidth=3},new JoinVertex{X=10,Y=0}};
        var line=new List<JoinVertex>{new JoinVertex{X=10.4,Y=0,StartWidth=4,EndWidth=5},new JoinVertex{X=20,Y=0}};
        var route=JoinGeometry.Ordered(arc,false);JoinGeometry.Append(route,JoinGeometry.Ordered(line,false));
        Check(route.Count==4&&route[0].Bulge==.5&&route[1].X==10&&route[2].X==10.4&&route[1].Bulge==0,"Gap connector preserves curved source endpoint and bulge");
        Check(route[1].StartWidth==3&&route[1].EndWidth==4&&route[2].StartWidth==4&&route[2].EndWidth==5,"Gap connector preserves adjacent widths");
        var reversed=JoinGeometry.Ordered(arc,true);Check(reversed[0].X==10&&reversed[0].Bulge==-.5&&reversed[0].StartWidth==3&&reversed[0].EndWidth==2&&reversed[1].X==0,"Reversal preserves arc geometry and swaps widths");
        var exact=JoinGeometry.Ordered(arc,false);JoinGeometry.Append(exact,new[]{new JoinVertex{X=10,Y=0},new JoinVertex{X=20,Y=0}});Check(exact.Count==3,"Coincident endpoints use one shared vertex");
        JoinGeometry.Append(exact,new[]{new JoinVertex{X=20,Y=0},new JoinVertex{X=30,Y=0}});Check(exact.Count==4&&exact[0].X==0&&exact[3].X==30,"Three consecutive parts assemble into one polyline");
        Check(JoinMetadataPolicy.IsSupported(new int[0],new string[0]),"Empty metadata accepted");
        Check(JoinMetadataPolicy.IsSupported(new[]{1001,1000},new[]{"AcDbBlockRepETag",""}),"Observed empty tag accepted");
        Check(!JoinMetadataPolicy.IsSupported(new[]{1001,1000},new[]{"AcDbBlockRepETag","data"}),"Nonempty ETag rejected");
        Check(!JoinMetadataPolicy.IsSupported(new[]{1001,1000},new[]{"OtherApp",""}),"Unknown application metadata rejected");
        Check(!JoinMetadataPolicy.IsSupported(new[]{1001,1000,1001,1000},new[]{"AcDbBlockRepETag","","OtherApp",""}),"Additional application metadata rejected");
        Check(!JoinMetadataPolicy.IsSupported(new[]{1001},new[]{"AcDbBlockRepETag"}),"Truncated tag rejected");
        Console.WriteLine("TOTAL PASS: "+passed);
    }
}
