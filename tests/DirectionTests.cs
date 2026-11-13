using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class DirectionTests
{
    static List<string> log=new List<string>();static void Check(bool value,string message){if(!value)throw new Exception(message);log.Add("PASS: "+message);Console.WriteLine(log.Last());}
    static bool Near(double a,double b){return Math.Abs(a-b)<1e-8;}
    static bool Reject(Action action){try{action();return false;}catch(ArgumentException){return true;}}
    static IEnumerable<Control> All(Control c){foreach(Control x in c.Controls){yield return x;foreach(var y in All(x))yield return y;}}
    static T Find<T>(Control c,string name)where T:Control{return All(c).OfType<T>().Single(x=>x.Name==name);}
    [STAThread]public static int Main(string[] args)
    {
        string evidence=Path.Combine(args[0],"evidence");Application.EnableVisualStyles();
        try
        {
            Check(Near(DirectionRules.Rotation(1,0),0)&&Near(DirectionRules.Rotation(-1,0),0),"Both horizontal route directions read left to right");
            Check(Near(DirectionRules.Rotation(0,1),-Math.PI/2)&&Near(DirectionRules.Rotation(0,-1),-Math.PI/2),"Both vertical route directions read top to bottom (270 degrees)");
            Check(Near(DirectionRules.Rotation(.001,1),-Math.PI/2)&&Near(DirectionRules.Rotation(-1,.001),0),"Near-axis tolerance avoids tiny surveying deviations");
            foreach(double angle in new[]{12.0,45,80,100,135,200,260,315}){double a=angle*Math.PI/180,rot=DirectionRules.Rotation(Math.Cos(a),Math.Sin(a));Check(Near(rot,DirectionRules.Rotation(-Math.Cos(a),-Math.Sin(a)))&&Math.Cos(rot)>0,"Diagonal "+angle+" has reverse-invariant left-to-right reading");}
            Check(Reject(()=>DirectionRules.Rotation(0,0))&&Reject(()=>DirectionRules.Rotation(Double.NaN,1)),"Zero and nonfinite direction vectors are rejected");
            var options=new DirectionOptions();var centers=DirectionRules.Centers(100,12,2,20,options);double half=6+.25*2,margin=3;
            Check(centers.Count>1&&Near(centers[0],100-centers.Last())&&centers.All(x=>x-half>=margin&&x+half<=100-margin),"Repeated labels are symmetric and clear both elbows");
            Check(centers.Zip(centers.Skip(1),(a,b)=>b-a).All(d=>d>=2*half+4),"Adjacent masks retain a visible straight pipe interval");
            Check(DirectionRules.Centers(18,12,2,20,options).Count==0&&DirectionRules.Centers(19,12,2,20,options).Count==1,"Short segments are skipped and exact minimum clearance fits once");
            options.RepeatMultiplier=2;Check(DirectionRules.Centers(300,12,2,20,options).Count<DirectionRules.Centers(300,12,2,20,new DirectionOptions()).Count,"Repeat multiplier changes density without changing text height");
            Check(Reject(()=>DirectionRules.Centers(1e15,1,1,1,options))&&Reject(()=>new DirectionOptions{PaddingRatio=0}.Validate())&&Reject(()=>new DirectionOptions{HeightOverride=Double.PositiveInfinity}.Validate()),"Count overflow and invalid spacing/height are rejected before CAD writes");
            using(var f=new DirectionForm(5,100))
            {
                f.Show();Application.DoEvents();Check(!Find<CheckBox>(f,"DirectionManualHeight").Checked&&!Find<NumericUpDown>(f,"DirectionHeight").Enabled,"Automatic source-derived height is default; manual override is explicit");
                f.Size=f.MinimumSize;Application.DoEvents();var notes=Find<Label>(f,"DirectionNotes");var needed=TextRenderer.MeasureText(notes.Text,notes.Font,new Size(notes.Width,10000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
                Check(notes.Height>=needed.Height,"All linked-label limitations fit at the minimum supported dialog size");
                foreach(var b in All(f).OfType<Button>())Check(b.Parent.ClientRectangle.Contains(b.Bounds),"Direction button "+b.Name+" remains visible");
                using(var bitmap=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(evidence,"ui-direction-tool.png"));}
                Find<CheckBox>(f,"DirectionManualHeight").Checked=true;Find<NumericUpDown>(f,"DirectionHeight").Value=3.5M;Find<Button>(f,"ReviewDirection").PerformClick();
                Check(f.DialogResult==DialogResult.OK&&f.Value.HeightOverride==3.5&&f.Value.ViewFactor==100&&!f.Value.Restore,"Review captures explicit height and current display estimate without changing a drawing");
            }
            using(var f=new DirectionForm(0,1)){f.Show();Find<ComboBox>(f,"DirectionMode").SelectedIndex=1;Find<Button>(f,"ReviewDirection").PerformClick();Check(f.Value.Restore,"Restore is a distinct mode in the same tool");}
            log.Add("RESULT: PASS. These tests cover placement mathematics and UI only; native CAD tests are separate.");return 0;
        }
        catch(Exception ex){log.Add("FAIL: "+ex);Console.WriteLine(ex);return 1;}
        finally{File.WriteAllLines(Path.Combine(evidence,"direction-tests.txt"),log);}
    }
}
