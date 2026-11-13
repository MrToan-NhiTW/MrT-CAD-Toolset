using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Autodesk.Windows;
using MedGas2024.Plugin;
public static class RibbonPlacement2005
{
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool SetDllDirectory(string path);
    [STAThread] public static int Main(string[] args)
    {
        string api=args[1];AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){string p=Path.Combine(api,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
        SetDllDirectory(api);
        try{Run(args[0]);return 0;}
        catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}
        finally{SetDllDirectory(null);}
    }
    static void Run(string root)
    {
            var queued=new List<string>();var handler=new RibbonCommandHandler(()=>true,s=>queued.Add(s));
            var create=typeof(RibbonFactory).GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic);
            var tab=(RibbonTab)create.Invoke(null,new object[]{handler,Path.Combine(root,"dist","MrT.CAD.Toolset.bundle","Contents","Win64","Resources")});
            var drawing=tab.Panels.Single(p=>p.Source.Title=="Công cụ bản vẽ").Source;
            var input=tab.Panels.Single(p=>p.Source.Title=="Xử lý đầu vào").Source;
            var join=drawing.Items.OfType<RibbonButton>().Single(b=>(string)b.CommandParameter=="MRTJOIN");
            if(input.Items.OfType<RibbonButton>().Any(b=>(string)b.CommandParameter=="MRTJOIN"))throw new Exception("MRTJOIN remains in input panel");
            if(!new[]{"MRTZ0","MRTZ0SCAN","MRTFONT","MRTFONTALL","MRTFONTCHECK"}.All(id=>input.Items.OfType<RibbonButton>().Any(b=>(string)b.CommandParameter==id)))throw new Exception("Missing input function");
            handler.Execute(join);if(queued.Count!=1||queued[0]!="_MRTJOIN ")throw new Exception("Wrong join command");
            Console.WriteLine("PASS: JOIN appears only in drawing panel, dispatches MRTJOIN, and Z0/font remain in input panel.");
    }
}
