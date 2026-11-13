using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MedGas2024.Core;
using MedGas2024.UI;
public static class CombinedPickTests
{
    static List<string> log=new List<string>();static string evidence;static void Check(bool value,string text){if(!value)throw new Exception(text);log.Add("PASS: "+text);Console.WriteLine(log.Last());}
    static IEnumerable<Control> All(Control p){foreach(Control c in p.Controls){yield return c;foreach(var d in All(c))yield return d;}}
    static T Find<T>(Control p,string name)where T:Control{return All(p).OfType<T>().Single(c=>c.Name==name);}
    static void Capture(Form f,string name){Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(evidence,name+".png"));}}
    static IEnumerable<string> Once(){yield return "CO2-15";yield return "block-A";yield return "CO2-15";}
    [STAThread]public static int Main(string[] args){evidence=Path.Combine(args[0],"evidence");Application.EnableVisualStyles();try{
        string[] received=null;int familyCalls=0,similarCalls=0;var result=PickCombination.Expand(Once(),seeds=>{familyCalls++;Check(seeds.SequenceEqual(new[]{"CO2-15","block-A"}),"System branch receives deduplicated original samples");seeds[0]="local-mutation";return new[]{"CO2-15","CO2-22","CO2-28"};},seeds=>{similarCalls++;received=seeds;return new[]{"CO2-15","foreign-line","block-B"};});
        Check(received.SequenceEqual(new[]{"CO2-15","block-A"})&&familyCalls==1&&similarCalls==1,"Select Similar receives original samples, never expanded or mutated system results");Check(result.SequenceEqual(new[]{"CO2-15","block-A","CO2-22","CO2-28","foreign-line","block-B"}),"Combined result is the union, deduplicates overlap and retains the selected samples");
        Check(PickCombination.Expand(new string[0],seeds=>{throw new Exception("should not run");},seeds=>{throw new Exception("should not run");}).Length==0,"Empty selection does not launch either expansion");Check(PickCombination.Expand(new[]{"block"},seeds=>new string[0],seeds=>new[]{"block","other-block"}).Length==2,"Non-pipe sources can contribute through Similar alone");
        bool failed=false;try{PickCombination.Expand(new[]{"pipe"},seeds=>new[]{"pipe","pipe2"},seeds=>{throw new OperationCanceledException();});}catch(OperationCanceledException){failed=true;}Check(failed,"Cancelled Similar cannot be reported as a successful partial combination");
        using(var f=new PickModeForm(2)){f.Show();Check(All(f).OfType<Button>().Count(b=>b.Name.StartsWith("Pick"))==3,"Picker offers three distinct modes");Capture(f,"ui-pick-combined");Find<Button>(f,"PickCombined").PerformClick();Check(f.Mode==PickMode.Combined&&f.DialogResult==DialogResult.OK,"Combined mode has an explicit selection result");}using(var f=new PickModeForm(0)){f.Show();Find<Button>(f,"CancelPickMode").PerformClick();Check(f.DialogResult==DialogResult.Cancel,"Picker cancellation remains available");}
        int logged=0;var source=new FamilySource{Handle="A",Fingerprint="fixture",Layer="Mr.T_CO2_D22_RAW",Text="D22.CO2",Label="CO2",Prefix="Mr.T",Code="CO2",Suffix="RAW",Color="ACI 5",Height=.35,Dash=1.5,Scale=.15,Width=15,WidthNote="15",Font="Bahnschrift Bold",Direction="Theo mẫu"};using(var f=new FamilySizesForm(source,r=>{var plan=new FamilySizePlan{Rows=r.Rows(source)};foreach(var row in plan.Rows){row.Create=true;row.State="Sẽ tạo";}return plan;},(r,p)=>{var ex=new InvalidOperationException("test native failure");ex.Data["MrT.Stage"]="Gán nét/màu cho Layer";throw ex;})){f.ErrorHandler=ex=>{logged++;};f.Show();Find<TextBox>(f,"FamilyDiameters").Text="28";Find<Button>(f,"InspectFamilySizes").PerformClick();Find<Button>(f,"CreateFamilySizes").PerformClick();Check(logged==1&&f.CreatedCount==0&&!Find<Button>(f,"CreateFamilySizes").Enabled&&Find<Label>(f,"FamilyReport").Text.Contains("Gán nét/màu"),"Family create failure logs once, shows its exact stage and requires review again");Capture(f,"ui-family-error-stage");f.Close();}
        log.Add("RESULT: PASS; standalone union and UI tests. Native selection and eNoDatabase regression need AutoCAD.");return 0;
    }catch(Exception ex){log.Add("FAIL: "+ex);Console.WriteLine(ex);return 1;}finally{File.WriteAllLines(Path.Combine(evidence,"combined-pick-tests.txt"),log);}}
}
