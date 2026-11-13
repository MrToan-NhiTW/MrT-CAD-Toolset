using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MedGas2024.Core
{
    [DataContract]
    public sealed class LegendRow
    {
        [DataMember] public string Id {get;set;}
        [DataMember] public bool Use {get;set;}
        [DataMember] public string Kind {get;set;}
        [DataMember] public string Label {get;set;}
        [DataMember] public string Description {get;set;}
        [DataMember] public string[] Sources {get;set;}
        [DataMember] public string SampleBlock {get;set;}
        [DataMember] public double SampleScale {get;set;}
        [DataMember] public double Rotation {get;set;}
        [DataMember] public double MinHeight {get;set;}
        [DataMember] public bool PipeBaseline {get;set;}
        [DataMember] public double PipeBaseScale {get;set;}
        [DataMember] public double PipeBaseWidth {get;set;}
        [DataMember] public double PipeLinetypeFactor {get;set;}
        [DataMember] public double PipeWidthFactor {get;set;}
        [DataMember(EmitDefaultValue=false)] public string PipeSourceLayer {get;set;}
        [DataMember(EmitDefaultValue=false)] public string PipeSourceHandle {get;set;}
        [DataMember(EmitDefaultValue=false)] public string PipeSourceDatabaseKey {get;set;}
        [DataMember] public List<LegendStroke> Strokes {get;set;}
        [OnDeserializing] void InitializePipeFactors(StreamingContext context){PipeLinetypeFactor=1;PipeWidthFactor=1;}
        public LegendRow(){Id=Guid.NewGuid().ToString("N");Use=true;Kind="Trống";Label="";Description="";Sources=new string[0];SampleScale=1;PipeLinetypeFactor=1;PipeWidthFactor=1;Strokes=new List<LegendStroke>();}
    }
    [DataContract]
    public sealed class LegendStroke
    {
        [DataMember] public double[] Points {get;set;}
        [DataMember(EmitDefaultValue=false)] public bool Mask {get;set;}
    }
    [DataContract]
    public sealed class LegendSpec
    {
        [DataMember] public int Schema=1;
        [DataMember] public string Title="BẢNG KÝ HIỆU";
        [DataMember] public string BlockName="Mr.T Bảng ký hiệu";
        [DataMember] public string TitleFont="Bahnschrift";
        [DataMember] public string BodyFont="Arial";
        [DataMember] public bool TitleBold=true,BodyBold=false;
        [DataMember] public double TitleHeight=5,BodyHeight=2.5,RowHeight=9,Width=180,SymbolWidth=25,DrawingFactor=100;
        [DataMember] public int Groups=2,SplitAt=0;
        [DataMember] public string Notes="";
        [DataMember] public string DatabaseKey="";
        [DataMember] public List<LegendRow> Rows=new List<LegendRow>();
        [DataMember] public int UnitMode=1;
        [DataMember] public double PrintDenominator=100;
        [DataMember] public double NativeWidth,NativeSymbol,NativeTitle,NativeBody,NativeHeader;
        [DataMember] public double PipeLinetypeMultiplier=1,PipeWidthMultiplier=1;
        [DataMember(EmitDefaultValue=false)] public bool PipeWidthAbsolute;
        [OnDeserializing] void InitializeDefaults(StreamingContext context){PipeLinetypeMultiplier=1;PipeWidthMultiplier=1;BlockName="Mr.T Bảng ký hiệu";}
        public string LoadNote="";
        public void PipeValues(double baseScale,double baseWidth,out double actualScale,out double actualWidth)
        {
            actualScale=baseScale*PipeLinetypeMultiplier;actualWidth=PipeWidthAbsolute?PipeWidthMultiplier:baseWidth*PipeWidthMultiplier;
            if(!Profile.Finite(actualScale)||actualScale<=0||!Profile.Finite(actualWidth)||actualWidth<0)throw new ArgumentException("Scale/Width mẫu ống sau tính toán không hợp lệ hoặc vượt phạm vi số; hãy giảm giá trị.");
        }
        public void PipeValues(double baseScale,double baseWidth,LegendRow row,out double actualScale,out double actualWidth)
        {
            PipeValues(baseScale,baseWidth,out actualScale,out actualWidth);actualScale*=row.PipeLinetypeFactor;actualWidth*=row.PipeWidthFactor;
            if(!Profile.Finite(actualScale)||actualScale<=0||!Profile.Finite(actualWidth)||actualWidth<0)throw new ArgumentException("Hệ số riêng của mẫu ống làm Scale/Width vượt phạm vi số.");
        }
        public void Validate()
        {
            if(Schema!=1||Rows==null||Rows.Count>300)throw new ArgumentException("Bảng hỗ trợ tối đa 300 hàng.");
            if(Groups!=1&&Groups!=2)throw new ArgumentException("Chọn một hoặc hai nhóm cột.");
            foreach(double n in new[]{TitleHeight,BodyHeight,RowHeight,Width,SymbolWidth,DrawingFactor})if(!Profile.Finite(n)||n<=0)throw new ArgumentException("Kích thước và hệ số phải lớn hơn 0.");
            if(TitleHeight>10000||BodyHeight>10000||Width>1e7||SymbolWidth>1e7||RowHeight>1e6||DrawingFactor>1e9||DrawingFactor<1e-9)throw new ArgumentException("Thông số bảng ngoài phạm vi hỗ trợ.");
            if(!Profile.Finite(PipeLinetypeMultiplier)||PipeLinetypeMultiplier<=0||!Profile.Finite(PipeWidthMultiplier)||PipeWidthMultiplier<0)throw new ArgumentException("Nét ống: Linetype phải dương và Width không âm; các giá trị phải hữu hạn.");
            if(SymbolWidth<8||Width/Groups-SymbolWidth<15)throw new ArgumentException("Ô ký hiệu tối thiểu 8 mm; ô diễn giải tối thiểu 15 mm.");
            if(String.IsNullOrWhiteSpace(TitleFont)||String.IsNullOrWhiteSpace(BodyFont))throw new ArgumentException("Chọn font tiêu đề và diễn giải.");
            if(String.IsNullOrWhiteSpace(BlockName)||BlockName.Trim().Length>200)throw new ArgumentException("Tên block bảng không được trống và tối đa 200 ký tự.");
            if((Title??"").Length>500||(Notes??"").Length>8000)throw new ArgumentException("Tiêu đề/ghi chú quá dài.");
            foreach(var r in Rows){if(r==null||!Profile.Finite(r.SampleScale)||r.SampleScale<=0||(r.Kind!="Nét ống"&&r.SampleScale>1)||!Profile.Finite(r.Rotation)||!Profile.Finite(r.MinHeight)||r.MinHeight<0||r.MinHeight>1e6)throw new ArgumentException("Cỡ ống × phải dương; hình khác phải trong khoảng lớn hơn 0 đến 1; cao hàng không âm.");if(!Profile.Finite(r.PipeLinetypeFactor)||r.PipeLinetypeFactor<=0||!Profile.Finite(r.PipeWidthFactor)||r.PipeWidthFactor<0)throw new ArgumentException("Hệ số riêng của ống: Ltype > 0, Width ≥ 0 và hữu hạn.");if((r.Description??"").Length>4000)throw new ArgumentException("Diễn giải tối đa 4000 ký tự mỗi dòng.");if(String.IsNullOrEmpty(r.Id))r.Id=Guid.NewGuid().ToString("N");}
        }
        public string Serialize(){using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(LegendSpec)).WriteObject(stream,this);return Encoding.UTF8.GetString(stream.ToArray());}}
        public static LegendSpec Parse(string value){if(value==null||value.Length>2000000)throw new ArgumentException("Dữ liệu bảng không hợp lệ.");using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(value))){var s=(LegendSpec)new DataContractJsonSerializer(typeof(LegendSpec)).ReadObject(stream);s.Validate();return s;}}
    }
    public sealed class LegendSlot
    {
        public int Index,Row,Column;
    }
    public sealed class LegendLayout
    {
        public List<LegendRow> Items;public List<LegendSlot> Slots=new List<LegendSlot>();public double[] Heights;public int DataRows;public double HeaderHeight,TitleHeight,NotesHeight;
        public double Height {get{return TitleHeight+HeaderHeight+Heights.Sum()+NotesHeight;}}
        public static LegendLayout Build(LegendSpec spec)
        {
            spec.Validate();var x=new LegendLayout{Items=spec.Rows.Where(r=>r.Use).ToList(),TitleHeight=Math.Max(spec.TitleHeight*1.8,8),HeaderHeight=Math.Max(spec.BodyHeight*2.4,7)};
            int n=x.Items.Count,split=spec.Groups==1?n:spec.SplitAt>0?Math.Min(n,spec.SplitAt):(n+1)/2;
            x.DataRows=Math.Max(1,Math.Max(split,n-split));x.Heights=Enumerable.Repeat(Math.Max(spec.RowHeight,spec.BodyHeight*2.2),x.DataRows).ToArray();
            double bodyWidth=spec.Width/spec.Groups-spec.SymbolWidth-4;
            for(int i=0;i<n;i++){int group=i<split?0:1;int row=i<split?i:i-split;var item=x.Items[i];x.Slots.Add(new LegendSlot{Index=i,Row=row,Column=group*2});int lines=WrapCount(item.Description,Math.Max(1,(int)(bodyWidth/(spec.BodyHeight*.6))));x.Heights[row]=Math.Max(x.Heights[row],Math.Max(item.MinHeight,lines*spec.BodyHeight*1.5+3));}
            x.NotesHeight=String.IsNullOrWhiteSpace(spec.Notes)?0:Math.Max(8,WrapCount(spec.Notes,Math.Max(1,(int)((spec.Width-4)/(spec.BodyHeight*.6))))*spec.BodyHeight*1.5+4);return x;
        }
        public static int WrapCount(string text,int columns){return (text??"").Replace("\r","").Split('\n').Sum(line=>Math.Max(1,(line.Length+columns-1)/columns));}
    }
}
