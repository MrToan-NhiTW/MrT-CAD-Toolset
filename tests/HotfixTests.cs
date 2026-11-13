using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class HotfixTests
{
    static List<string> log=new List<string>();static void Check(bool ok,string text){if(!ok)throw new Exception(text);log.Add("PASS: "+text);Console.WriteLine(log.Last());}
    static IEnumerable<Control> All(Control p){foreach(Control c in p.Controls){yield return c;foreach(var x in All(c))yield return x;}}
    [STAThread]public static int Main(string[] args){try
    {
        var defaults=new LegendSpec();Check(defaults.UnitMode==1&&defaults.DrawingFactor==100&&defaults.PrintDenominator==100,"New legend defaults to Model mm at 1:100, without a hidden division by 1000");
        Check(LegendMetrics.PaperFactor(1,100)==100&&LegendMetrics.PaperFactor(2,100)==.1&&LegendMetrics.PaperFactor(3,100)==1,"Model mm, explicit Model m and Layout use distinct paper conversions");
        var old=new LegendSpec{DrawingFactor=1};LegendMetrics.ReadNative(old,18000,2500,500,250,700);Check(old.DrawingFactor==100&&old.Width==180&&old.SymbolWidth==25&&old.TitleHeight==5&&old.UnitMode==0,"Legacy 1.9 table scaled x100 reopens as the same paper dimensions with factor 100");
        var styled=new LegendSpec{DrawingFactor=1};LegendMetrics.ReadNative(styled,18000,2500,5,2.5,700);Check(styled.DrawingFactor==100&&styled.TitleHeight==5&&styled.BodyHeight==2.5,"SCALE preserves paper font sizes when native cell style heights are reported without the table transform");
        var repeated=LegendSpec.Parse(old.Serialize());repeated.NativeWidth=18000;repeated.NativeSymbol=2500;repeated.NativeTitle=500;repeated.NativeBody=250;repeated.NativeHeader=700;LegendMetrics.ReadNative(repeated,18000,2500,500,250,700);Check(repeated.DrawingFactor==100&&repeated.Width==180,"Reopening without a new SCALE never applies the scale twice");
        var stretch=new LegendSpec{DrawingFactor=1};LegendMetrics.ReadNative(stretch,18000,25,5,2.5,7);Check(stretch.DrawingFactor==1&&stretch.Width==18000,"A width-only stretch is preserved and is not mistaken for uniform SCALE");
        Check(PointGlyph.Create(1).Count==0&&PointGlyph.Create(2).Count==2&&PointGlyph.Create(3).Count==2&&PointGlyph.Create(34).Count==3&&PointGlyph.Create(98).Count==4,"POINT glyphs honor hidden, plus, cross, circle and square PDMODE combinations");
        Application.EnableVisualStyles();var raw=new LegendSpec{Width=18000,SymbolWidth=2500,RowHeight=900,TitleHeight=500,BodyHeight=250,DrawingFactor=1,UnitMode=0};raw.Rows.Add(new LegendRow{Kind="POINT",Label="POINT",Description="Điểm định vị",Strokes=PointGlyph.Create(34)});
        using(var f=new LegendForm(raw,m=>new LegendRow[0],s=>true,()=>null,()=>{})){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();var width=All(f).OfType<NumericUpDown>().Single(c=>c.Name=="LegendWidth");Check(width.Value==18000&&width.Maximum>=18000&&f.Read().Width==18000,"Loading actual width 18000 expands the input range without clipping or an ArgumentOutOfRange exception");var unit=All(f).OfType<ComboBox>().Single(c=>c.Name=="LegendUnitMode");unit.SelectedIndex=1;Check(f.Read().DrawingFactor==100,"Explicit Model mm preset applies factor 100");unit.SelectedIndex=2;Check(f.Read().DrawingFactor==.1,"Metres conversion runs only after the user explicitly chooses Model m");f.Close();}
        using(var f=new LegendForm(old,m=>new LegendRow[0],s=>true,()=>null,()=>{})){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(args[0],"evidence","ui-legend-scale-fix.png"));}f.Close();}
        log.Add("RESULT: PASS; layout, unit conversion and WinForms. Native TABLE transform/POINT/linetype replacement need the native fixture.");return 0;
    }catch(Exception ex){log.Add("RESULT: FAIL\n"+ex);Console.WriteLine(ex);return 1;}finally{File.WriteAllLines(Path.Combine(args[0],"evidence","hotfix-tests.txt"),log);}}
}
