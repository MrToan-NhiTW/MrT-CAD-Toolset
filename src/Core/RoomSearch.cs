using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MedGas2024.Core
{
    // Search helpers for lists the user browses (Excel rows, layers, drawing text).
    // Matching rooms to drawing text stays exact (RoomCatalog.MatchName); this is
    // only for finding things in a list.
    public static class RoomSearch
    {
        public static string Fold(string value)
        {
            if(String.IsNullOrEmpty(value))return "";
            var sb=new StringBuilder(value.Length);
            foreach(char c in value.Normalize(NormalizationForm.FormD))
            {
                if(CharUnicodeInfo.GetUnicodeCategory(c)==UnicodeCategory.NonSpacingMark)continue;
                if(c=='đ'||c=='Đ'){sb.Append('d');continue;}
                sb.Append(Char.IsWhiteSpace(c)?' ':Char.ToLowerInvariant(c));
            }
            return String.Join(" ",sb.ToString().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries));
        }

        // Every word of the query must appear somewhere in the fields (accent and
        // case insensitive); a word starting with '-' must not appear.
        public static bool Matches(string query,params string[] fields)
        {
            var words=Fold(query).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            if(words.Length==0)return true;
            string hay=String.Join(" | ",(fields??new string[0]).Select(Fold));
            foreach(var w in words)
            {
                if(w.Length>1&&w[0]=='-'){if(hay.IndexOf(w.Substring(1),StringComparison.Ordinal)>=0)return false;continue;}
                if(w=="-")continue;
                if(hay.IndexOf(w,StringComparison.Ordinal)<0)return false;
            }
            return true;
        }

        // Natural order: "P2" before "P10", "007" with "7", text compared without case.
        public static int NaturalCompare(string a,string b)
        {
            a=a??"";b=b??"";int i=0,j=0;
            while(i<a.Length&&j<b.Length)
            {
                if(Char.IsDigit(a[i])&&Char.IsDigit(b[j]))
                {
                    int si=i,sj=j;while(i<a.Length&&Char.IsDigit(a[i]))i++;while(j<b.Length&&Char.IsDigit(b[j]))j++;
                    string x=a.Substring(si,i-si).TrimStart('0'),y=b.Substring(sj,j-sj).TrimStart('0');
                    if(x.Length!=y.Length)return x.Length.CompareTo(y.Length);
                    int c=String.CompareOrdinal(x,y);if(c!=0)return c;
                    continue;
                }
                int ia=i,jb=j;while(i<a.Length&&!Char.IsDigit(a[i]))i++;while(j<b.Length&&!Char.IsDigit(b[j]))j++;
                int t=String.Compare(a.Substring(ia,i-ia),b.Substring(jb,j-jb),CultureInfo.CurrentCulture,CompareOptions.IgnoreCase);
                if(t!=0)return t;
            }
            int rest=(a.Length-i).CompareTo(b.Length-j);
            return rest!=0?rest:String.CompareOrdinal(a,b);
        }

        public static int Compare(object a,object b)
        {
            if(a==null||b==null)return a==null?(b==null?0:-1):1;
            if(a is IComparable&&a.GetType()==b.GetType()&&!(a is string))return ((IComparable)a).CompareTo(b);
            return NaturalCompare(Convert.ToString(a,CultureInfo.CurrentCulture),Convert.ToString(b,CultureInfo.CurrentCulture));
        }
    }
}
