using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
namespace MedGas2024.Core
{
    public sealed class FamilySource
    {
        public string Handle,Fingerprint,Layer,Type,Text,Label,Prefix,Code,Suffix,Font,Color,Direction,WidthNote;
        public double Height,Dash,Scale,Padding=.35;public double? Width;
        public string Summary{get{return "Ống mẫu: "+Text+" | Layer: "+Layer+"\r\nMàu thực: "+Color+" | Scale: "+Profile.N(Scale)+" | Width: "+WidthNote+"\r\n"+Font+"\r\nCao chữ: "+Profile.N(Height)+" | Đoạn nét: "+Profile.N(Dash)+" | "+Direction+". Tính lại khoảng trống theo chữ mới.";}}
    }
    public sealed class FamilyRequest
    {
        public string Prefix="Mr.T",Code="",Suffix="BASE",Diameters="";public double Padding=.35;
        public List<FamilySizeRow> Rows(FamilySource source)
        {
            if(!String.IsNullOrEmpty(Prefix)&&!Regex.IsMatch(Prefix,@"^[A-Za-z][A-Za-z0-9_.-]{0,31}$"))throw new ArgumentException("Tiền tố để trống hoặc dùng chữ không dấu, số, _, . hoặc -.");
            if(String.IsNullOrWhiteSpace(Code)||Code.Length>40||!Regex.IsMatch(Code,@"^[\p{L}\p{M}\p{N} _.-]+$"))throw new ArgumentException("Mã hệ tối đa 40 ký tự: chữ, số, dấu cách, _, . hoặc -.");
            if(!String.IsNullOrEmpty(Suffix)&&!Regex.IsMatch(Suffix,@"^[A-Za-z0-9_.-]+$"))throw new ArgumentException("Hậu tố dùng chữ không dấu, số, _, . hoặc -.");if(!Profile.Finite(Padding)||Padding<.05||Padding>5)throw new ArgumentException("Hở chữ/cao chữ trong khoảng 0.05–5.");
            var tokens=Regex.Split(Diameters??"",@"[,;\s]+").Where(x=>x.Length>0).ToList();if(tokens.Count==0||tokens.Count>200)throw new ArgumentException("Nhập từ 1 đến 200 cỡ, ví dụ 28,35.");var rows=new List<FamilySizeRow>();var seen=new HashSet<int>();foreach(string token in tokens){int d;if(!Int32.TryParse(Regex.Replace(token,@"^D(?:N)?","",RegexOptions.IgnoreCase),NumberStyles.None,CultureInfo.InvariantCulture,out d)||d<1||d>10000)throw new ArgumentException("Cỡ không hợp lệ: "+token);if(!seen.Add(d))continue;string name=Profile.ComposeName(Prefix,Code,d,Suffix);string type=Profile.ComposeName(Prefix,Profile.LinetypeCode(Code),d,Suffix);BatchEdit.Name(name);BatchEdit.Name(type);rows.Add(new FamilySizeRow{Diameter=d,Name=name,TypeName=type,Text=GasRow.FormatText(source.Label,d),State="Chưa kiểm tra"});}return rows.OrderBy(r=>r.Diameter).ToList();
        }
        public string Signature{get{return Profile.Hash(Prefix+"|"+Code+"|"+Suffix+"|"+Diameters+"|"+Profile.N(Padding));}}
    }
    public sealed class FamilySizeRow
    {
        public int Diameter{get;set;}public string Name{get;set;}public string TypeName{get;set;}public string Text{get;set;}public string State{get;set;}public bool Create{get;set;}
        public double Before,After,X,Y;
    }
    public sealed class FamilySizePlan
    {
        public string SourceHandle,SourceFingerprint,RequestSignature,Signature;public List<FamilySizeRow> Rows=new List<FamilySizeRow>();
        public string Summary{get{return "Tạo mới "+Rows.Count(r=>r.Create)+" cỡ; giữ nguyên "+Rows.Count(r=>!r.Create)+" tên đã tồn tại. Kế thừa mẫu, tự căn chữ theo từng cỡ. Chưa gán lên ống hiện hành.";}}
    }
}
