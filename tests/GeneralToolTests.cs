using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class GeneralToolTests
{
    static readonly List<string> log=new List<string>();static string evidence;
    static void Check(bool value,string message){if(!value)throw new Exception(message);log.Add("PASS: "+message);Console.WriteLine(log.Last());}
    static bool Near(double a,double b){return Math.Abs(a-b)<1e-8;}
    static bool Reject(Action a){try{a();return false;}catch(ArgumentException){return true;}}
    static IEnumerable<Control> All(Control p){foreach(Control c in p.Controls){yield return c;foreach(var d in All(c))yield return d;}}
    static T Named<T>(Control p,string name)where T:Control{return All(p).OfType<T>().Single(c=>c.Name==name);}
    static void Capture(Form f,string file){Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(evidence,file+".png"));}}
    static void NearField(Form f,string name,double value){Check(Near((double)Named<NumericUpDown>(f,name).Value,value),name+" = "+value);}
    [STAThread]public static int Main(string[] args)
    {
        evidence=Path.Combine(args[0],"evidence");Application.EnableVisualStyles();
        try
        {
            var p=Profile.Load(Path.Combine(args[0],"bin","default-profile.json"));Check(p.Rows.Count==0&&p.UnitTag=="BASE","Shipped library contains zero systems and a neutral suffix");
            Check(typeof(Profile).GetMethod("WorkingDefaults")==null&&typeof(Profile).GetMethod("UseFourBarLayout")==null,"Runtime no longer contains medical-gas default generation or four-bar layout helpers");
            var papers=PaperSuggestion.Defaults();Check(papers.Select(x=>x.Paper).SequenceEqual(new[]{"A0","A1","A2","A3","A4"})&&papers[0].TextMm==3.5&&papers[0].WidthMm==.35,"A0–A4 are explicitly estimated readability suggestions");
            var estimate=PaperSuggestion.Estimate(3.5,.35,100,1,2.5,1,false);Check(Near(estimate.EntityScale,140)&&Near(estimate.GlobalWidth,35),"A0 estimate uses real linetype height, print ratio and model unit");
            var meters=PaperSuggestion.Estimate(3.5,.35,100,1000,.0025,1,false);Check(Near(meters.EntityScale,140)&&Near(meters.GlobalWidth,.035),"Equivalent meter/mm definitions estimate equal printed text size");
            Check(Near(PaperSuggestion.Estimate(3.5,.35,100,1,350,1,false).EntityScale,1),"Legacy large pattern is estimated from its actual height instead of receiving a universal raw scale");
            Check(Near(PaperSuggestion.Estimate(3.5,.35,100,1,2.5,10,false).EntityScale,14),"Existing global LTSCALE is accounted for without changing it");
            Check(Near(PaperSuggestion.Estimate(3.5,.35,100,1,2.5,1,true).EntityScale,1.4),"PSLTSCALE=1 uses paper-space pattern units rather than multiplying viewport scale twice");
            Check(Reject(()=>PaperSuggestion.Estimate(3.5,.35,100,1,0,1,false))&&Reject(()=>PaperSuggestion.Estimate(3.5,.35,Double.NaN,1,2.5,1,false)),"Invalid native heights and nonfinite estimates are rejected");
            Check(Reject(()=>new ScaleOptions().Validate())&&Reject(()=>new ScaleOptions{ApplyScale=true,EntityScale=0}.Validate())&&Reject(()=>new ScaleOptions{ApplyWidth=true,GlobalWidth=-1}.Validate()),"Scale/width validation rejects empty actions, zero scale and negative width");new ScaleOptions{ApplyWidth=true,GlobalWidth=0}.Validate();Check(true,"Width-only reset to zero is allowed");
            int reads=0,writes=0;using(var f=new ManagerForm(p,"DWG thử; thư viện mới trống",q=>{reads++;return "create-only";},q=>{writes++;return "created";},null))
            {
                f.Show();Check(Named<DataGridView>(f,"MainGrid").Rows.Count==0&&!Named<CheckBox>(f,"UseAll").Enabled&&!Named<Button>(f,"ApplyDrawing").Enabled,"Empty library renders without old rows or an enabled write action");Named<Button>(f,"Inspect").PerformClick();Check(reads==0&&writes==0,"Inspecting an empty library never invokes engine callbacks");Capture(f,"ui-empty-library");f.Close();
            }
            var seed=new GasRow{SystemCode="",LabelPrefix="",Diameter=110,Enabled=true,Dash=10,GapBefore=8,GapAfter=8,TextScale=2.5,OffsetX=-7,OffsetY=-1,RotationMode="U",ColorIndex=7};
            using(var f=new NewSystemForm(p,p.Rows,seed))
            {
                f.Show();Check(f.NewRows.Count==0&&Named<TextBox>(f,"SystemCode").Text==""&&Named<TextBox>(f,"LabelPrefix").Text==""&&Named<TextBox>(f,"Diameters").Text=="","Creation requires user-defined code, label and batch sizes");Named<TextBox>(f,"SystemCode").Text="HDPE";Named<TextBox>(f,"LabelPrefix").Text="HDPE";Named<CheckBox>(f,"CreateMany").Checked=true;Named<TextBox>(f,"Diameters").Text="90,110,160";Check(f.NewRows.Select(x=>x.Text).SequenceEqual(new[]{"D90.HDPE","D110.HDPE","D160.HDPE"})&&f.NewRows.All(x=>x.SourceFile==null),"Generic water/drainage/material rows are generated without reference metadata");Capture(f,"ui-generic-new-system");f.Close();Check(p.Rows.Count==0,"Cancelling generic creation keeps the original library empty");
            }
            var rows=new List<DrawingLayerInfo>{new DrawingLayerInfo{LayerName="Mr.T_CN_D110_BASE",LinetypeName="CN110",Text="D110.CN",NativeTextHeight=2.5,Writable=true,Managed=true,State="Sẵn sàng"},new DrawingLayerInfo{LayerName="Mr.T_TN_D160_BASE",LinetypeName="TN160",Text="D160.TN",NativeTextHeight=350,Writable=true,Managed=true,State="Sẵn sàng"}};
            using(var f=new DrawingManagerForm(rows,()=>rows,(r,e)=>{})){f.Show();Capture(f,"ui-general-drawing-manager");f.Close();}
            using(var f=new ScaleForm(rows,5,new ScaleContext(),false,rows[0]))
            {
                f.Show();NearField(f,"TargetScale",100);NearField(f,"TargetWidth",20);Named<ComboBox>(f,"PaperPreset").SelectedIndex=0;NearField(f,"TargetScale",140);NearField(f,"TargetWidth",35);Named<NumericUpDown>(f,"PrintDenominator").Value=50;NearField(f,"TargetScale",70);NearField(f,"TargetWidth",17.5);
                Named<ComboBox>(f,"ScaleScope").SelectedIndex=1;var list=Named<CheckedListBox>(f,"ScaleLayers");list.SetItemChecked(0,true);Named<TextBox>(f,"ScaleLayerSearch").Text="TN";list.SetItemChecked(0,true);Named<TextBox>(f,"ScaleLayerSearch").Clear();Check(f.LayerNames.Length==2,"Layer selections survive filtering and support more than one system");
                Named<NumericUpDown>(f,"TargetScale").Value=.7M;Named<NumericUpDown>(f,"TargetWidth").Value=15;Capture(f,"ui-scale-sync");f.Size=f.MinimumSize;Application.DoEvents();Check(Named<Button>(f,"ReviewScale").RectangleToScreen(Named<Button>(f,"ReviewScale").ClientRectangle).Bottom<=f.RectangleToScreen(f.ClientRectangle).Bottom,"Sync confirmation remains visible at minimum size");Capture(f,"ui-scale-sync-minimum");Named<Button>(f,"ReviewScale").PerformClick();Check(f.Value.ApplyScale&&f.Value.ApplyWidth&&f.UseLayers&&f.LayerNames.Length==2&&f.Value.EntityScale==.7&&f.Value.GlobalWidth==15,"Manual final values and Layer scope are captured exactly after confirmation");
            }
            using(var f=new ScaleForm(rows,0,new ScaleContext(),true,rows[1])){f.Show();Named<ComboBox>(f,"PaperPreset").SelectedIndex=0;NearField(f,"TargetScale",1);Named<CheckBox>(f,"ChangeWidth").Checked=false;Named<Button>(f,"ReviewScale").PerformClick();Check(!f.Value.ApplyWidth&&f.Value.ApplyScale,"Paper suggestions can apply scale while preserving existing widths");}
            using(var picker=new PipePickerForm(rows,5,new ScaleContext()))
            {
                picker.Show();Exception error=null;bool seen=false;using(var timer=new Timer{Interval=60})
                {
                    timer.Tick+=delegate{var dialog=Application.OpenForms.OfType<ScaleForm>().FirstOrDefault();if(dialog==null)return;timer.Stop();seen=true;try{dialog.Size=dialog.MinimumSize;Named<ComboBox>(dialog,"PaperPreset").SelectedIndex=0;Named<NumericUpDown>(dialog,"TargetScale").Value=.25M;Named<NumericUpDown>(dialog,"TargetWidth").Value=15;Capture(dialog,"ui-paper-preset");Named<Button>(dialog,"ReviewScale").PerformClick();}catch(Exception ex){error=ex;dialog.Close();}};timer.Start();Named<Button>(picker,"AssignPaperPreset").PerformClick();timer.Stop();
                }
                if(error!=null)throw error;Check(seen&&!picker.KeepScale&&picker.EntityScale==.25&&picker.GlobalWidth==15&&!picker.ColorByLayer,"Fast assignment receives chosen scale/width while preserving color by default");Capture(picker,"ui-general-assign");picker.Close();
            }
            Check(rows.All(r=>r.ColorText==null)&&p.Rows.Count==0,"Settings and estimates never mutate drawing catalog or library rows");return 0;
        }
        catch(Exception ex){log.Add("FAIL: "+ex);Console.WriteLine(ex);return 1;}
        finally{File.WriteAllLines(Path.Combine(evidence,"general-tool-tests.txt"),log);}
    }
}
