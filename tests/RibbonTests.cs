using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Autodesk.Windows;
using MedGas2024.Plugin;

public static class RibbonTests
{
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern bool SetDllDirectory(string path);
    private static readonly List<string> log=new List<string>();
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);log.Add("PASS: "+message);Console.WriteLine(log.Last());}
    [STAThread]
    public static int Main(string[] args)
    {
        string root=args[0],api=args[1];
        AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){string path=Path.Combine(api,new AssemblyName(e.Name).Name+".dll");return File.Exists(path)?Assembly.LoadFrom(path):null;};
        SetDllDirectory(api);
        try{Run(root);log.Add("Standalone Autodesk Ribbon object tests only; AutoCAD workspace events and live Ribbon rendering not exercised.");return 0;}
        catch(Exception ex){log.Add("FAIL: "+ex);Console.WriteLine(ex);return 1;}
        finally{File.WriteAllLines(Path.Combine(root,"evidence","ribbon-tests.txt"),log);SetDllDirectory(null);}
    }
    private static void Run(string root)
    {
        string resources=Path.Combine(root,"dist","MrT.CAD.Toolset.bundle","Contents","Win64","Resources");
        bool documentAvailable=true;var sent=new List<string>();
        var command=new RibbonCommandHandler(()=>documentAvailable,text=>sent.Add(text));var ribbon=new RibbonControl();var home=new RibbonTab{Id="HOME",Title="Home",IsActive=true};ribbon.Tabs.Add(home);
        var tab=RibbonFactory.Ensure(ribbon,null,false,command,resources);
        Check(ribbon.Tabs.Count(t=>t.Id==RibbonFactory.TabId)==1&&tab.Title=="Mr.T CAD tool set","Creates exactly one visible toolset tab");
        var buttons=tab.Panels.SelectMany(p=>p.Source.Items).OfType<RibbonButton>().ToList();
        Check(buttons.Count==10&&buttons[0].Text.Replace("\n"," ")=="Mr.T Quản lý đường nét"&&Convert.ToString(buttons[0].CommandParameter)=="MRTTOOLS","Tool button has the requested Vietnamese name and opens the manager");
        Check(Convert.ToString(buttons[7].CommandParameter)=="MRTHELP"&&buttons.All(b=>b.CommandHandler==command&&b.ShowText),"All buttons are executable and retain their labels");
        buttons[0].CommandHandler.Execute(buttons[0]);
        Check(sent.SequenceEqual(new[]{"_MRTTOOLS "}),"A manager click passes the actual RibbonButton and queues exactly MRTTOOLS");
        buttons[7].CommandHandler.Execute(buttons[7]);
        Check(sent.SequenceEqual(new[]{"_MRTTOOLS ","_MRTHELP "}),"A Help click queues MRTHELP rather than the RibbonButton type name");
        buttons[1].CommandHandler.Execute(buttons[1]);buttons[2].CommandHandler.Execute(buttons[2]); Check(sent.Skip(2).SequenceEqual(new[]{"_MRTPIPE ","_MRTSCALE "}),"Fast assignment button dispatches its actual CommandParameter; no copy-source button");
        buttons[3].CommandHandler.Execute(buttons[3]);Check(sent.Last()=="_MRTR0 ","Native R0 batch conversion dispatches its separate command");
        buttons[4].CommandHandler.Execute(buttons[4]);Check(sent.Last()=="_MRTSEL ","Quick system picker dispatches its own native selection command");
        buttons[5].CommandHandler.Execute(buttons[5]);buttons[6].CommandHandler.Execute(buttons[6]);Check(sent.Skip(sent.Count-2).SequenceEqual(new[]{"_MRTISO ","_MRTUNISO "}),"Isolate and restore buttons dispatch their own commands");
        buttons[8].CommandHandler.Execute(buttons[8]);buttons[9].CommandHandler.Execute(buttons[9]);Check(sent.Skip(sent.Count-2).SequenceEqual(new[]{"_MRTLEGEND ","_MRTLEGEDIT "})&&tab.Panels[1].Source.Title=="Ký hiệu / Ghi chú","Legend create/edit buttons dispatch their registered commands in a separate annotation group");
        int before=sent.Count;command.Execute("MRTPICK");command.Execute(null);command.Execute(new object());command.Execute(new RibbonButton{CommandParameter=new object()});command.Execute(new RibbonButton{CommandParameter="LINE"});
        Check(sent.Count==before,"Null, unsupported objects and unexpected command names do not inject editor text");
        command.Execute("MRTTOOLS");command.Execute(" _mrthelp ");
        Check(sent.Skip(before).SequenceEqual(new[]{"_MRTTOOLS ","_MRTHELP "}),"Direct-string invocations also normalize to the two registered tool commands");
        documentAvailable=false;before=sent.Count;command.Execute(buttons[0]);
        Check(!command.CanExecute(buttons[0])&&sent.Count==before,"Clicking with no active drawing cannot queue a command");
        documentAvailable=true;
        Check(command.CanExecute(null)&&command.CanExecute(buttons[0]),"Command availability recovers when a drawing exists, including null-parameter probes");
        var same=RibbonFactory.Ensure(ribbon,tab,false,command,resources);
        Check(Object.ReferenceEquals(tab,same)&&ribbon.Tabs.Count==2&&tab.Panels.Count==2,"Repeated document/workspace readiness signals do not duplicate the tab or panel");
        ribbon.Tabs.Remove(tab);
        var rebuilt=RibbonFactory.Ensure(ribbon,tab,false,command,resources);
        Check(Object.ReferenceEquals(tab,rebuilt)&&tab.Panels.SelectMany(p=>p.Source.Items).Count()==10,"Restores the same tab and its buttons after a workspace clears custom content");
        before=sent.Count;((RibbonButton)rebuilt.Panels[0].Source.Items[0]).CommandHandler.Execute(rebuilt.Panels[0].Source.Items[0]);
        Check(sent.Count==before+1&&sent.Last()=="_MRTTOOLS ","Restored buttons still route clicks to the manager command");
        tab.IsVisible=false;RibbonFactory.Ensure(ribbon,tab,true,command,resources);
        Check(tab.IsVisible&&tab.IsActive,"Manual restore reveals and selects an existing hidden tab");
        ribbon.Tabs.Remove(tab);var replacement=new RibbonControl();var restored=RibbonFactory.Ensure(replacement,tab,false,command,resources);
        Check(Object.ReferenceEquals(restored,tab)&&replacement.Tabs.Count==1,"Reattaches cached controls when AutoCAD replaces the Ribbon control");
        var withoutImages=RibbonFactory.Ensure(new RibbonControl(),null,false,command,Path.Combine(root,"evidence","no-icons"));
        Check(withoutImages.Panels[0].Source.Items.Count==8,"A missing optional icon folder does not suppress the tab");
        string broken=Path.Combine(root,"evidence","corrupt-icons");Directory.CreateDirectory(broken);File.WriteAllText(Path.Combine(broken,"manage32.png"),"not a PNG");File.WriteAllText(Path.Combine(broken,"help32.png"),"not a PNG");
        var brokenTab=RibbonFactory.Ensure(new RibbonControl(),null,false,command,broken);
        Check(brokenTab.Panels[0].Source.Items.OfType<RibbonButton>().All(b=>b.ShowText&&!b.ShowImage),"Corrupt optional images fall back to usable text buttons");
    }
}
