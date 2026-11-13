using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class LegendTests
{
    static List<string> log=new List<string>();
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);log.Add("PASS: "+name);Console.WriteLine(log.Last());}
    static IEnumerable<Control> All(Control c){foreach(Control x in c.Controls){yield return x;foreach(var y in All(x))yield return y;}}
    static T Named<T>(Control c,string name)where T:Control{return All(c).OfType<T>().Single(x=>x.Name==name);}
    static bool Reject(Action a){try{a();return false;}catch(ArgumentException){return true;}}
    static void MaskPreview(string root)
    {
        var s=new LegendSpec{Groups=1,Width=100,SymbolWidth=45,RowHeight=35,Title="WIPEOUT"};
        var mask=new LegendStroke{Mask=true,Points=new double[]{-.2,-.2,.2,-.2,.2,.2,-.2,.2,-.2,-.2}};
        s.Rows.Add(new LegendRow{Kind="Hình 2D",Description="Mẫu vùng che",Strokes=new List<LegendStroke>{new LegendStroke{Points=new double[]{-.5,0,.5,0}},mask,new LegendStroke{Points=new double[]{.35,-.4,.35,.4}}}});
        var restored=LegendSpec.Parse(s.Serialize());Check(restored.Rows[0].Strokes[1].Mask&&!restored.Rows[0].Strokes[0].Mask,"WIPEOUT preview mask flag survives JSON without changing ordinary strokes");
        using(var canvas=new LegendCanvas{Size=new Size(900,600),Spec=s,PreviewLayout=LegendLayout.Build(s)})using(var masked=new Bitmap(900,600))using(var plain=new Bitmap(900,600))
        {
            canvas.DrawToBitmap(masked,new Rectangle(0,0,900,600));mask.Mask=false;canvas.DrawToBitmap(plain,new Rectangle(0,0,900,600));mask.Mask=true;
            float scale=(float)Math.Min((900-32)/s.Width,(600-48)/canvas.PreviewLayout.Height);float x=(900-(float)s.Width*scale)/2+(float)s.SymbolWidth/2*scale;float y=16+(float)(canvas.PreviewLayout.TitleHeight+canvas.PreviewLayout.HeaderHeight+s.RowHeight/2)*scale;
            int centreMasked=0,centrePlain=0;for(int iy=(int)y-1;iy<=(int)y+1;iy++)for(int ix=(int)x-2;ix<=(int)x+2;ix++){if(masked.GetPixel(ix,iy).R<180)centreMasked++;if(plain.GetPixel(ix,iy).R<180)centrePlain++;}
            Check(centrePlain>centreMasked&&centreMasked==0,"Preview fills mask after the rear line, hiding its centre while retaining later foreground strokes");masked.Save(Path.Combine(root,"evidence","ui-wipeout-preview.png"));
        }
    }
    static LegendSpec Sample()
    {
        var s=new LegendSpec{DatabaseKey="TEST",Notes="Kích thước và cao độ tuân theo bản vẽ chi tiết.\nCác mẫu dưới đây chỉ minh họa giao diện, chưa lấy từ DWG."};
        foreach(string pair in new[]{"CN|Đường ống cấp nước sinh hoạt","TN|Đường ống thoát nước thải","TH|Đường ống thông hơi"}){var p=pair.Split('|');s.Rows.Add(new LegendRow{Kind="Nét ống",Label=p[0],Description=p[1]});}
        s.Rows.Add(new LegendRow{Kind="Hình 2D",Label="VAN_KHOA",Description="Van khóa",Strokes=new List<LegendStroke>{new LegendStroke{Points=new double[]{-.5,-.25,.5,.25,.5,-.25,-.5,.25,-.5,-.25}}}});
        var circle=new List<double>();for(int i=0;i<=48;i++){circle.Add(Math.Cos(i*Math.PI/24)*.45);circle.Add(Math.Sin(i*Math.PI/24)*.45);}s.Rows.Add(new LegendRow{Kind="Hình 2D",Label="BOM",Description="Bơm cấp nước",Strokes=new List<LegendStroke>{new LegendStroke{Points=circle.ToArray()},new LegendStroke{Points=new double[]{-.3,-.3,.3,0,-.3,.3,-.3,-.3}}}});
        s.Rows.Add(new LegendRow{Kind="Hình 2D",Label="DONG_HO",Description="Đồng hồ đo lưu lượng",Strokes=new List<LegendStroke>{new LegendStroke{Points=circle.ToArray()},new LegendStroke{Points=new double[]{-.25,-.1,0,.3,.25,-.1}}}});return s;
    }
    static void HeaderClick(DataGridView grid,string name)
    {
        var method=typeof(DataGridView).GetMethod("OnColumnHeaderMouseClick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        method.Invoke(grid,new object[]{new DataGridViewCellMouseEventArgs(grid.Columns[name].Index,-1,0,0,new MouseEventArgs(MouseButtons.Left,1,0,0,0))});Application.DoEvents();
    }
    static void NewWorkflow()
    {
        var input=Sample();input.Rows[0].Description="Giữ diễn giải";input.Rows[0].PipeBaseline=true;input.Rows[0].SampleBlock="OLD";int calls=0;bool cancel=false;
        using(var f=new LegendForm(input,mode=>{calls++;if(cancel)return new LegendRow[0];return new[]{new LegendRow{Kind=mode==3?"Nét ống":"Hình 2D",Label=mode==3?"PIPE_NEW":"SHAPE_NEW",Sources=new[]{"ABC"},SampleScale=1}};},x=>true,()=>null,()=>{}))
        {
            f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();
            var grid=Named<DataGridView>(f,"LegendRows");grid.CurrentCell=grid.Rows[0].Cells[3];grid.ClearSelection();grid.Rows[0].Selected=true;string originalId=((LegendRow)grid.Rows[0].DataBoundItem).Id;
            All(f).OfType<RibbonButton>().Single(b=>b.Text=="Chọn lại\nống").PerformClick();Application.DoEvents();var result=(LegendRow)grid.Rows[0].DataBoundItem;
            Check(result.Id==originalId&&result.Label=="PIPE_NEW"&&result.Description=="Giữ diễn giải"&&!result.PipeBaseline&&result.SampleBlock==null&&result.Sources[0]=="ABC","Repick pipe replaces source while retaining row identity and description and clearing cached baseline");
            cancel=true;All(f).OfType<RibbonButton>().Single(b=>b.Text=="Chọn lại\nhình").PerformClick();Check(ReferenceEquals(result,grid.Rows[0].DataBoundItem),"Cancelled repick preserves previous source");cancel=false;
            All(f).OfType<RibbonButton>().Single(b=>b.Text=="Chọn lại\nhình").PerformClick();result=(LegendRow)grid.Rows[0].DataBoundItem;Check(result.Kind=="Hình 2D"&&result.Label=="SHAPE_NEW"&&result.Id==originalId&&result.Description=="Giữ diễn giải","Repick shape can replace a pipe while retaining row identity and description");
            Named<Button>(f,"LegendSelectPipes").PerformClick();var picked=grid.SelectedRows.Cast<DataGridViewRow>().Select(r=>(LegendRow)r.DataBoundItem).ToArray();Check(picked.Length==2&&picked.All(r=>r.Kind=="Nét ống"),"Quick selection selects only pipe rows");
            Named<Button>(f,"LegendSelectShapes").PerformClick();picked=grid.SelectedRows.Cast<DataGridViewRow>().Select(r=>(LegendRow)r.DataBoundItem).ToArray();Check(picked.Length==4&&picked.All(r=>r.Kind=="Hình 2D"),"Quick selection selects only shape rows");
            grid.Rows[0].Cells["MinHeight"].Value=9.0;grid.Rows[1].Cells["MinHeight"].Value=12.0;grid.Rows[2].Cells["MinHeight"].Value=2.0;grid.EndEdit();HeaderClick(grid,"MinHeight");var asc=f.Read().Rows.Select(x=>x.MinHeight).ToArray();Check(asc.SequenceEqual(asc.OrderBy(x=>x)),"Header sort uses numeric rather than lexical order and changes emitted row order");HeaderClick(grid,"MinHeight");var desc=f.Read().Rows.Select(x=>x.MinHeight).ToArray();Check(desc.SequenceEqual(desc.OrderByDescending(x=>x)),"Second header click reverses numeric order");
            var sym=Named<NumericUpDown>(f,"LegendSymbolWidth");sym.Value=25.12345M;var factor=Named<NumericUpDown>(f,"LegendFactor");factor.Value=100;var mul=Named<NumericUpDown>(f,"LegendPipeLinetype");mul.Value=1.23456M;
            Check(Math.Abs(f.Read().SymbolWidth-25.12345)<1e-9&&Math.Abs(f.Read().PipeLinetypeMultiplier-1.23456)<1e-9,"Legend numeric inputs retain five decimal digits");
            Check(factor.Text==100M.ToString("0.######",System.Globalization.CultureInfo.CurrentCulture)&&mul.Text==1.23456M.ToString("0.#####",System.Globalization.CultureInfo.CurrentCulture),"Numbers display significant decimals without padded trailing zeroes");
            factor.Value=.000001M;Check(Math.Abs(f.Read().DrawingFactor-.000001)<1e-12,"Existing six-decimal conversion factors are retained");
            sym.Focus();Application.DoEvents();sym.Text=25.98765M.ToString("0.00000",System.Globalization.CultureInfo.CurrentCulture);typeof(NumericUpDown).GetMethod("ValidateEditText",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(sym,null);Check(sym.Value==25.98765M&&Math.Abs(f.Read().SymbolWidth-25.98765)<1e-9,"Typing and committing a five-decimal value preserves every digit");
            f.Size=f.MinimumSize;Application.DoEvents();foreach(string name in new[]{"LegendWidth","LegendSymbolWidth","LegendDescriptionWidth","LegendRowHeight","LegendFactor","LegendPrintScale","LegendPipeLinetype","LegendPipeWidth","LegendSelectPipes","LegendSelectShapes"}){var ctl=All(f).Single(c=>c.Name==name);Check(ctl.Parent.ClientRectangle.Contains(ctl.Bounds),name+" stays visible at minimum window size");}
            using(var preview=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size));preview.Save(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),"..","evidence","ui-legend-minimum-195.png"));}
            f.Close();
        }
        var oldCulture=System.Threading.Thread.CurrentThread.CurrentCulture;
        try{foreach(string culture in new[]{"en-US","vi-VN"}){System.Threading.Thread.CurrentThread.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo(culture);using(var g=new LegendForm(Sample(),mode=>new LegendRow[0],x=>true,()=>null,()=>{})){g.ShowInTaskbar=false;g.Location=new Point(-20000,-20000);g.Show();Application.DoEvents();var field=Named<NumericUpDown>(g,"LegendSymbolWidth");field.Focus();field.Text=25.12345M.ToString("0.00000",System.Globalization.CultureInfo.CurrentCulture);typeof(NumericUpDown).GetMethod("ValidateEditText",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(field,null);Check(field.Value==25.12345M&&field.Text==25.12345M.ToString("0.#####",System.Globalization.CultureInfo.CurrentCulture),"Five-digit numeric input and display follow "+culture);g.Close();}}}finally{System.Threading.Thread.CurrentThread.CurrentCulture=oldCulture;}
    }
    [STAThread]public static int Main(string[] args)
    {
        string root=args[0];try
        {
            var empty=new LegendSpec();Check(empty.Rows.Count==0,"New legend contains no medical-gas or other seeded production symbols");
            Check(empty.BlockName=="Mr.T Bảng ký hiệu","Default legend block name uses the requested Vietnamese name");
            empty.BlockName="Bảng kỹ thuật tầng 2";Check(LegendSpec.Parse(empty.Serialize()).BlockName==empty.BlockName,"Custom Unicode block name survives saved metadata");
            var oldName=System.Text.RegularExpressions.Regex.Replace(empty.Serialize(),"\\\"BlockName\\\":\\\"[^\\\"]*\\\",","");Check(LegendSpec.Parse(oldName).BlockName=="Mr.T Bảng ký hiệu","Old legend metadata receives the default block name");empty.BlockName=" ";Check(Reject(()=>empty.Validate()),"Blank block names are rejected before writing");empty.BlockName="Mr.T Bảng ký hiệu";
            Check(empty.PipeLinetypeMultiplier==1 && empty.PipeWidthMultiplier==1,"New multiplier defaults preserve source attributes");
            Check(!empty.PipeWidthAbsolute,"Width multiplier mode remains the default");
            var legacy=LegendSpec.Parse(empty.Serialize().Replace("\"PipeLinetypeMultiplier\":1,","").Replace("\"PipeWidthMultiplier\":1,", ""));
            Check(legacy.PipeLinetypeMultiplier==1 && legacy.PipeWidthMultiplier==1,"Older metadata defaults missing multipliers to 1");
            empty.PipeLinetypeMultiplier=0;Check(Reject(()=>empty.Validate()),"Zero linetype multiplier is rejected");empty.PipeLinetypeMultiplier=1;empty.PipeWidthMultiplier=-1;Check(Reject(()=>empty.Validate()),"Negative width multiplier is rejected");
            empty.PipeLinetypeMultiplier=10000;empty.PipeWidthMultiplier=20000;empty.PipeWidthAbsolute=false;empty.Validate();double actualScale,actualWidth;empty.PipeValues(2,3,out actualScale,out actualWidth);Check(actualScale==20000&&actualWidth==60000,"Factors larger than 100 survive validation and calculate independently");
            empty.PipeWidthAbsolute=true;empty.PipeValues(2,0,out actualScale,out actualWidth);Check(actualWidth==20000&&LegendSpec.Parse(empty.Serialize()).PipeWidthAbsolute,"Direct width works when source width is zero and survives saved metadata");
            empty.PipeLinetypeMultiplier=Double.MaxValue;Check(Reject(()=>empty.PipeValues(2,0,out actualScale,out actualWidth)),"Overflowing product is rejected before CAD write");
            var mixed=Sample();mixed.Rows[0].SampleScale=2;mixed.Validate();mixed.Rows[3].SampleScale=2;Check(Reject(()=>mixed.Validate()),"Larger per-row scale applies only to pipes, preserving shape rules");
            var per=Sample();per.PipeLinetypeMultiplier=2;per.PipeWidthMultiplier=3;per.Rows[0].PipeLinetypeFactor=4;per.Rows[0].PipeWidthFactor=5;per.PipeValues(2,3,per.Rows[0],out actualScale,out actualWidth);Check(actualScale==16&&actualWidth==45,"Per-row factors adjust pipe scale and width independently of common controls");
            var oldRows=Sample();var oldJson=oldRows.Serialize().Replace("\"PipeLinetypeFactor\":1,","").Replace("\"PipeWidthFactor\":1,","");var oldLoaded=LegendSpec.Parse(oldJson);Check(oldLoaded.Rows.All(x=>x.PipeLinetypeFactor==1&&x.PipeWidthFactor==1),"Existing saved legends inherit neutral per-row factors");

            var s=Sample();var l=LegendLayout.Build(s);Check(l.DataRows==3&&l.Slots.Select(z=>z.Column).SequenceEqual(new[]{0,0,0,2,2,2}),"Six items balance into two independent symbol/description groups");
            s.SplitAt=2;l=LegendLayout.Build(s);Check(l.DataRows==4&&l.Slots[2].Column==2&&l.Slots[2].Row==0,"Explicit split chooses the first item of the second group");
            s.Groups=1;l=LegendLayout.Build(s);Check(l.DataRows==6&&l.Slots.All(z=>z.Column==0),"Single group keeps list order and two columns");
            s.Rows[0].Use=false;l=LegendLayout.Build(s);Check(l.Items.Count==5&&!l.Items.Contains(s.Rows[0]),"Unchecked rows are excluded without deleting them");
            s.Rows[1].Description=String.Join(" ",Enumerable.Repeat("Đường ống có diễn giải dài",25));l=LegendLayout.Build(s);Check(l.Heights[0]>s.RowHeight,"Long descriptions grow row height instead of shrinking the body font");
            var round=LegendSpec.Parse(s.Serialize());Check(round.Rows[1].Description==s.Rows[1].Description&&round.TitleFont==s.TitleFont&&round.Rows[3].Strokes[0].Points.Length==10,"Unicode text, settings and preview geometry survive embedded JSON round trip");
            round.Width=30;Check(Reject(()=>round.Validate()),"Too-narrow description columns are rejected");round=Sample();round.Rows[0].Rotation=Double.NaN;Check(Reject(()=>round.Validate()),"Non-finite per-symbol rotation is rejected before any drawing write");
            Application.EnableVisualStyles();MaskPreview(root);NewWorkflow();int writes=0,captures=0;var received=new List<LegendSpec>();
            using(var f=new LegendForm(Sample(),mode=>{captures++;return new[]{new LegendRow{Label="TEST",Kind="Hình 2D",Description="Mẫu được chọn"}};},data=>{writes++;received.Add(LegendSpec.Parse(data.Serialize()));return true;},()=>null,()=>{}))
            {
                f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();
                Check(All(f).OfType<RibbonGroup>().Count()==3&&All(f).OfType<RibbonButton>().Count()==12,"Legend screen exposes source replacement actions alongside existing tools");
                var name=Named<TextBox>(f,"LegendBlockName");Check(!name.Visible&&Named<Label>(f,"LegendGroupMode").Text.Contains("GROUP")==false&&Named<Label>(f,"LegendGroupMode").Text.Contains("nhóm"),"Legend form shows grouped TABLE and WIPEOUT without requiring a block name");
                Check(All(f).OfType<Label>().Where(x=>x.Name.EndsWith("Caption")).All(x=>x.Height>=20&&x.RectangleToScreen(x.ClientRectangle).Bottom<f.Bottom),"Ribbon group captions are visible");var grid=Named<DataGridView>(f,"LegendRows");Check(!grid.Columns["Description"].ReadOnly&&grid.Columns["Label"].ReadOnly&&grid.Rows.Count==6,"Descriptions edit directly while source identity remains separate");
                grid.Rows[0].Cells["Description"].Value="Ống cấp nước HDPE — diễn giải sửa tay";grid.EndEdit();Named<ComboBox>(f,"LegendBodyFont").SelectedItem="Arial";Named<NumericUpDown>(f,"LegendFactor").Value=100;Named<Button>(f,"LegendInsert").PerformClick();Application.DoEvents();Check(writes==1&&received[0].DrawingFactor==100&&received[0].Rows[0].Description.Contains("sửa tay"),"Insert receives edited descriptions, actual selected font and paper-to-model factor");
                All(f).OfType<RibbonButton>().Single(b=>b.Text=="Nét ống").PerformClick();Check(captures==1&&grid.Rows.Count==7,"Native sample picker callback adds a row without rebuilding existing data");
                var c=Named<ComboBox>(f,"LegendGroups");c.SelectedIndex=0;Check(f.Read().Groups==1,"Switching layout changes the requested native table column count");c.SelectedIndex=1;
                Named<NumericUpDown>(f,"LegendDescriptionWidth").Value=80;Check(Math.Abs(f.Read().Width-210)<0.01 && Math.Abs(f.Read().SymbolWidth-25)<0.01,"Editing description column keeps A and adjusts overall width in two-group layout");
                Named<NumericUpDown>(f,"LegendSymbolWidth").Value=30;Check(Math.Abs(f.Read().Width-210)<0.01 && Named<NumericUpDown>(f,"LegendDescriptionWidth").Value==75,"Editing A preserves overall width and updates B");
                Named<NumericUpDown>(f,"LegendPipeLinetype").Value=2;Named<NumericUpDown>(f,"LegendPipeWidth").Value=3;Check(f.Read().PipeLinetypeMultiplier==2 && f.Read().PipeWidthMultiplier==3,"Separate pipe multipliers are captured from the form");
                Named<NumericUpDown>(f,"LegendPipeLinetype").Value=1500.12345M;Named<NumericUpDown>(f,"LegendPipeWidth").Value=333.125M;Named<ComboBox>(f,"LegendPipeWidthMode").SelectedIndex=1;Check(f.Read().PipeLinetypeMultiplier==1500.12345 && f.Read().PipeWidthMultiplier==333.125 && f.Read().PipeWidthAbsolute,"Pipe controls accept factors above 100 and a direct width for zero-width originals");
                grid.Rows[0].Cells["PipeLinetypeFactor"].Value=4.125;grid.Rows[0].Cells["PipeWidthFactor"].Value=5.25;grid.EndEdit();Check(f.Read().Rows[0].PipeLinetypeFactor==4.125&&f.Read().Rows[0].PipeWidthFactor==5.25,"Separate per-row pipe controls are editable");

                Named<NumericUpDown>(f,"LegendWidth").Value=50;Check(Named<Label>(f,"LegendStatus").Text.Contains("tối thiểu"),"Invalid layout has an actionable explanation");Named<NumericUpDown>(f,"LegendWidth").Value=180;
                f.ClientSize=new Size(1430,850);Application.DoEvents();using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"evidence","ui-legend.png"));}
                var canvas=All(f).OfType<LegendCanvas>().Single();Check(canvas.Width>250&&canvas.Height>200,"Preview receives usable space beside the editable list");f.Close();
            }
            log.Add("RESULT: PASS; standalone layout/WinForms tests. Native TABLE and linetype rendering not covered.");return 0;
        }catch(Exception ex){log.Add("RESULT: FAIL\n"+ex);Console.WriteLine(ex);return 1;}finally{File.WriteAllLines(Path.Combine(root,"evidence","legend-tests.txt"),log);}
    }
}
