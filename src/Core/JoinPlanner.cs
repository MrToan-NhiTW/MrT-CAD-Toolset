using System;
using System.Collections.Generic;
using System.Linq;

namespace MedGas2024.Core
{
    // Mutual, unambiguous endpoint matches only: a tee must never be silently joined.
    public sealed class JoinEndpoint
    {
        public int Route; public bool End; public double X,Y,Z; public string Style;
        public bool Curved;
    }
    public sealed class JoinLink { public int A,B; public double Gap; }
    public sealed class JoinChainsResult
    {
        public readonly List<List<Tuple<int,bool>>> Chains=new List<List<Tuple<int,bool>>>();
        public int Cycles;
    }
    public static class JoinPlanner
    {
        public static List<JoinLink> Find(IList<JoinEndpoint> ends,double tolerance)
        {
            if(tolerance<0||Double.IsNaN(tolerance)||Double.IsInfinity(tolerance))throw new ArgumentOutOfRangeException("tolerance");
            int n=ends.Count;var candidate=new int[n];var counts=new int[n];var gaps=new double[n];
            for(int i=0;i<n;i++){candidate[i]=-1;gaps[i]=Double.PositiveInfinity;}
            // The caller bounds selection size. A straight bridge preserves any
            // curved source segments when a gap is within the chosen tolerance.
            for(int i=0;i<n;i++)for(int j=i+1;j<n;j++){
                var a=ends[i];var b=ends[j];if(a.Route==b.Route||a.Style!=b.Style||Math.Abs(a.Z-b.Z)>1e-9)continue;
                double dx=a.X-b.X,dy=a.Y-b.Y,d=Math.Sqrt(dx*dx+dy*dy);
                if(d>tolerance)continue;
                // Prefer the nearest candidate. Several candidates at the same
                // minimum distance remain ambiguous (e.g. a true T junction).
                if(d<gaps[i]-1e-9){gaps[i]=d;candidate[i]=j;counts[i]=1;}else if(Math.Abs(d-gaps[i])<=1e-9)counts[i]++;
                if(d<gaps[j]-1e-9){gaps[j]=d;candidate[j]=i;counts[j]=1;}else if(Math.Abs(d-gaps[j])<=1e-9)counts[j]++;
            }
            var links=new List<JoinLink>();for(int i=0;i<n;i++){int j=candidate[i];if(j>i&&counts[i]==1&&counts[j]==1&&candidate[j]==i)links.Add(new JoinLink{A=i,B=j,Gap=gaps[i]});}return links;
        }
        public static JoinChainsResult Chains(int routeCount,IList<JoinLink> links)
        {
            var result=new JoinChainsResult();var mates=new int[routeCount*2];for(int i=0;i<mates.Length;i++)mates[i]=-1;
            foreach(var link in links){if(link.A<0||link.B<0||link.A>=mates.Length||link.B>=mates.Length||link.A/2==link.B/2||mates[link.A]>=0||mates[link.B]>=0)throw new ArgumentException("Invalid join links");mates[link.A]=link.B;mates[link.B]=link.A;}
            var used=new bool[routeCount];for(int i=0;i<routeCount;i++)if(!used[i]&&(mates[i*2]<0||mates[i*2+1]<0)){
                int ep=mates[i*2]<0?i*2:i*2+1;var chain=new List<Tuple<int,bool>>();while(ep>=0&&!used[ep/2]){int n=ep/2;used[n]=true;chain.Add(Tuple.Create(n,(ep%2)==1));int exit=n*2+((ep%2)==0?1:0);ep=mates[exit];}result.Chains.Add(chain);
            }
            // Do not turn an ambiguous closed cycle into a self-intersecting route.
            for(int i=0;i<used.Length;i++)if(!used[i]){result.Chains.Add(new List<Tuple<int,bool>>{Tuple.Create(i,false)});result.Cycles++;}
            return result;
        }
    }
}
