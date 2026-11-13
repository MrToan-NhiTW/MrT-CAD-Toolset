using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
namespace MedGas2024.Core
{
    public sealed class LayerParts
    {
        public string Prefix,System,Diameter,Suffix,Label;public bool Canonical;
        public static LayerParts Read(DrawingLayerInfo row)
        {
            var p=new LayerParts();var match=Regex.Match(row.LayerName??"",@"^(.+?)_(.+)_D([0-9]+)(?:_(.*))?$",RegexOptions.IgnoreCase);
            if(match.Success&&!row.UseLabelIdentity){p.Canonical=true;p.Prefix=match.Groups[1].Value;p.System=match.Groups[2].Value;p.Diameter=match.Groups[3].Value;p.Suffix=match.Groups[4].Value;}
            var label=Regex.Match(row.Text??"",@"^D([0-9]+)\.(.+)$",RegexOptions.IgnoreCase);if(label.Success){if(p.Diameter==null)p.Diameter=label.Groups[1].Value;p.Label=label.Groups[2].Value;}else {var legacy=Regex.Match(row.Text??"",@"^(.+)\.D([0-9]+)$",RegexOptions.IgnoreCase);if(legacy.Success){p.Label=legacy.Groups[1].Value;if(p.Diameter==null)p.Diameter=legacy.Groups[2].Value;}else p.Label=row.Text??"";}return p;
        }
        public string Name(){return Prefix+"_"+System+"_D"+Diameter+(String.IsNullOrEmpty(Suffix)?"":"_"+Suffix);}
    }
    public sealed class BatchEdit
    {
        public bool Delete,ChangeColor,ColorByLayer,RebindOldOverrides=true;
        public int Aci=7;
        public bool ChangePrefix,ChangeSuffix,ChangeSystem,ChangeLabel,ChangeText,ChangeDescription,ChangeDiameter;public int Diameter=110;
        public string Prefix="Mr.T",Suffix="BASE",System="",Label="",TextTemplate="D{D}.{Label}",DescriptionTemplate="{System} - D{D}";
        public bool ExactLayer,ExactType;public string LayerName="",TypeName="";
        public bool ChangeLinetype;public string AssignLinetype="";
        public bool ChangeHeight,ChangeDash,ChangeGaps,AutoFit,ChangeFont,ForceR;
        public double Height=2.5,Dash=10,Before=8,After=8,Padding=.35;
        public string FontFile="bahnschrift.ttf";public bool Bold=true;
        public bool ChangesPattern{get{return ChangeDiameter||ChangeLabel||ChangeText||ChangeDescription||ChangeHeight||ChangeDash||ChangeGaps||AutoFit||ChangeFont||ForceR;}}
        public bool Renames{get{return ExactLayer||ChangePrefix||ChangeSuffix||ChangeSystem||ChangeDiameter;}}
        public void Validate(int count)
        {
            if(count<1)throw new ArgumentException("Tích ít nhất một dòng.");
            if(Delete)return;
            if(!(ChangeColor||ColorByLayer||Renames||ExactType||ChangeLinetype||ChangesPattern))throw new ArgumentException("Tích thuộc tính muốn thay đổi; các mục chưa tích được giữ nguyên.");
            if(ChangeColor&&(Aci<1||Aci>255))throw new ArgumentException("Chọn ACI từ 1 đến 255.");
            if(ChangeDiameter&&(Diameter<1||Diameter>10000))throw new ArgumentException("Cỡ ống phải từ 1 đến 10000.");
            if(ExactLayer&&(ChangePrefix||ChangeSuffix||ChangeSystem||ChangeDiameter))throw new ArgumentException("Chọn tên Layer đầy đủ hoặc các thành phần tiền tố/hệ/hậu tố.");
            if(ExactLayer||ExactType){if(count!=1)throw new ArgumentException("Tên đầy đủ chỉ dùng cho một dòng.");if(ExactLayer)Name(LayerName);if(ExactType)Name(TypeName);}
            if(ChangePrefix)Part(Prefix,false);if(ChangeSuffix)Part(Suffix,true);if(ChangeSystem&&(String.IsNullOrWhiteSpace(System)||System.Length>40||!Regex.IsMatch(System,@"^[\p{L}\p{M}\p{N} _.-]+$")))throw new ArgumentException("Mã hệ tối đa 40 ký tự: chữ, số, dấu cách, _, . hoặc -.");
            if(ChangeLinetype)Name(AssignLinetype);
            foreach(double n in new[]{Height,Dash,Before,After,Padding})if(!Profile.Finite(n)||n<=0||n>1e9)throw new ArgumentException("Kích thước/hở chữ phải dương và hữu hạn.");
            if(ChangeFont&&(String.IsNullOrWhiteSpace(FontFile)||!Regex.IsMatch(FontFile,@"^[A-Za-z0-9 _.\-]+\.(ttf|shx)$",RegexOptions.IgnoreCase)))throw new ArgumentException("Font là tên file TTF/SHX đã cài, ví dụ bahnschrift.ttf.");
        }
        public static void Name(string value){if(String.IsNullOrWhiteSpace(value)||value.Length>240||value.Any(c=>c<' '||"<>/\\\":;?*|,=`".Contains(c)))throw new ArgumentException("Tên Layer/Linetype không hợp lệ: "+value);if(value.Equals("ByLayer",StringComparison.OrdinalIgnoreCase)||value.Equals("ByBlock",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Tên đặc biệt không dùng làm tên mới.");}
        private static void Part(string value,bool empty){if(empty&&value=="")return;if(String.IsNullOrWhiteSpace(value)||!Regex.IsMatch(value,@"^[A-Za-z0-9_.-]+$"))throw new ArgumentException("Mã/tiền tố/hậu tố dùng chữ không dấu, số, _, . hoặc -.");}
        public string NewLayer(DrawingLayerInfo row)
        {
            if(ExactLayer)return LayerName.Trim();if(!Renames)return row.LayerName;var p=LayerParts.Read(row);if(!p.Canonical)throw new ArgumentException("Tên không theo mẫu TiềnTố_Hệ_Dcỡ_HậuTố; sửa tên đầy đủ riêng dòng: "+row.LayerName);if(ChangePrefix)p.Prefix=Prefix;if(ChangeSuffix)p.Suffix=Suffix;if(ChangeSystem)p.System=System;if(ChangeDiameter)p.Diameter=Diameter.ToString(CultureInfo.InvariantCulture);var name=p.Name();Name(name);return name;
        }
        public string Expand(string template,DrawingLayerInfo row)
        {
            var p=LayerParts.Read(row);string label=ChangeLabel?Label:p.Label;string system=ChangeSystem?System:p.System??p.Label;
            return (template??"").Replace("{D}",ChangeDiameter?Diameter.ToString(CultureInfo.InvariantCulture):p.Diameter??"").Replace("{Label}",label??"").Replace("{System}",system??"").Replace("{Text}",row.Text??"").Replace("{Layer}",row.LayerName??"");
        }
        public string NewText(DrawingLayerInfo row)
        {
            if(ChangeText)return Expand(TextTemplate,row);if(!ChangeLabel&&!ChangeDiameter)return row.Text;var p=LayerParts.Read(row);if(String.IsNullOrEmpty(p.Diameter))throw new ArgumentException("Không nhận ra cỡ ống; dùng mẫu chữ trực tiếp: "+row.LayerName);return "D"+(ChangeDiameter?Diameter.ToString(CultureInfo.InvariantCulture):p.Diameter)+"."+(ChangeLabel?Label:p.Label);
        }
        public string Signature{get{return Profile.Hash(String.Join("|",GetType().GetFields().Where(f=>!f.IsStatic).OrderBy(f=>f.Name).Select(f=>f.Name+"="+Convert.ToString(f.GetValue(this),CultureInfo.InvariantCulture))));}}
    }
    public sealed class BatchPreviewRow
    {
        public string LayerBefore{get;set;}public string LayerAfter{get;set;}public string TypeBefore{get;set;}public string TypeAfter{get;set;}
        public string TextBefore{get;set;}public string TextAfter{get;set;}public string ColorBefore{get;set;}public string ColorAfter{get;set;}public string Detail{get;set;}public string BeforeState{get;set;}public string AfterState{get;set;}
        public bool Blocked{get;set;}
    }
}
