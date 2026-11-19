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
            var titles=tab.Panels.Select(p=>p.Source.Title).ToArray();
            if(!titles.SequenceEqual(new[]{"Đường ống","Chọn & Cô lập","Hình học & Block","Chữ & Font","Bảng & Phòng","Tiện ích"}))throw new Exception("Unexpected panel order: "+String.Join(", ",titles));
            var buttons=RibbonFactory.AllButtons(tab).ToList();var ids=buttons.Select(b=>(string)b.CommandParameter).ToList();
            if(ids.Count!=ids.Distinct().Count())throw new Exception("Duplicate ribbon command");
            var expected=new[]{"MRTTOOLS","MRTPIPE","MRTSCALE","MRTR0","MRTDENSITY","MRTALIGNR","MRTSEL","MRTISO","MRTVIEW","MRTSELLAYER","MRTISOADD","MRTUNISO","MRTJOIN","MRTBREAK","MRTZ0","MRTZ0PL","MRTBLOCKLAY","MRTBLOCKMERGE","MRTFONT","MRTFONTALL","MRTFONTCHECK","MRTLEGEND","MRTLEGEDIT","MRTLEGSYNC","MRTROOMS","MRTROOMLIST","MRTROOMINFO","MRTALIASES","MRTHELP"};
            if(!ids.OrderBy(x=>x).SequenceEqual(expected.OrderBy(x=>x)))throw new Exception("Ribbon command set changed: "+String.Join(", ",ids));
            var isolate=new[]{"MRTISO","MRTVIEW","MRTISOADD","MRTUNISO"};var pickPanel=tab.Panels[1];
            if(!isolate.All(id=>RibbonFactory.AllButtons(tab).Any(b=>(string)b.CommandParameter==id&&PanelOf(tab,b)==pickPanel)))throw new Exception("Isolate tools are split across panels");
            var join=buttons.Single(b=>(string)b.CommandParameter=="MRTJOIN");
            handler.Execute(join);if(queued.Count!=1||queued[0]!="_MRTJOIN ")throw new Exception("Wrong join command");
            Console.WriteLine("PASS: 6 panels in work order, every command appears once, isolate tools grouped, JOIN dispatches MRTJOIN.");
    }
    static RibbonPanel PanelOf(RibbonTab tab,RibbonButton button)
    {
        foreach(var panel in tab.Panels)foreach(var item in panel.Source.Items){if(item==button)return panel;var row=item as RibbonRowPanel;if(row!=null&&row.Items.Contains(button))return panel;}
        return null;
    }
}
