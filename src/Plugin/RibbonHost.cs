using System;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Ribbon;
using Autodesk.Windows;
using AcApp=Autodesk.AutoCAD.ApplicationServices.Application;

namespace MedGas2024.Plugin
{
    internal static class RibbonHost
    {
        private static bool started,pending,createdHook,selectRequested;
        private static RibbonPaletteSet palette;
        private static RibbonTab tab;
        public static void Start()
        {
            if(started)return;started=true;
            AcApp.SystemVariableChanged+=VariableChanged;
            AcApp.DocumentManager.DocumentActivated+=DocumentActivated;
            Queue();
        }
        public static void Restore(bool showRibbon)
        {
            Start();selectRequested|=showRibbon;
            if(showRibbon)
            {
                var doc=AcApp.DocumentManager.MdiActiveDocument;
                if(doc!=null)doc.SendStringToExecute("_.RIBBON ",true,false,true);
            }
            Queue();
        }
        private static void DocumentActivated(object sender,DocumentCollectionEventArgs e){Queue();}
        private static void VariableChanged(object sender,SystemVariableChangedEventArgs e)
        {
            if(String.Equals(e.Name,"WSCURRENT",StringComparison.OrdinalIgnoreCase)||String.Equals(e.Name,"RIBBONSTATE",StringComparison.OrdinalIgnoreCase)||String.Equals(e.Name,"COLORTHEME",StringComparison.OrdinalIgnoreCase))Queue();
        }
        private static void PaletteCreated(object sender,EventArgs e){Queue();}
        private static void WorkspaceLoaded(object sender,EventArgs e){Queue();}
        private static void HookPalette()
        {
            if(!createdHook){RibbonServices.RibbonPaletteSetCreated+=PaletteCreated;createdHook=true;}
            var current=RibbonServices.RibbonPaletteSet;
            if(Object.ReferenceEquals(current,palette))return;
            if(palette!=null)palette.WorkspaceLoaded-=WorkspaceLoaded;
            palette=current;if(palette!=null)palette.WorkspaceLoaded+=WorkspaceLoaded;
        }
        private static void Queue(){if(!started||pending)return;pending=true;AcApp.Idle+=OnIdle;}
        private static void OnIdle(object sender,EventArgs e)
        {
            // Wait until CAD finishes rebuilding its workspace. Palette/document events
            // re-arm this one-shot handler, so an absent Ribbon never spins on idle.
            AcApp.Idle-=OnIdle;pending=false;if(!started)return;
            try
            {
                HookPalette();
                if(AcApp.DocumentManager.MdiActiveDocument==null)return;
                var ribbon=palette==null?ComponentManager.Ribbon:palette.RibbonControl;
                if(ribbon==null)return;
                string resources=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"Resources");
                tab=RibbonFactory.Ensure(ribbon,tab,selectRequested,new RibbonCommandHandler(()=>AcApp.DocumentManager.MdiActiveDocument!=null,command=>{var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc!=null)doc.SendStringToExecute(command,true,false,true);}),resources);
                selectRequested=false;
            }
            catch(System.Exception ex){Log(ex);}
        }
        private static void Log(System.Exception ex)
        {
            try
            {
                string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MrT","CADToolset","Logs","ribbon-errors.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.AppendAllText(path,DateTimeOffset.Now.ToString("o")+"\r\n"+ex+"\r\n");
                var doc=AcApp.DocumentManager.MdiActiveDocument;if(doc!=null)doc.Editor.WriteMessage("\nMr.T: chưa hiện được tab. Dùng MRTRIBBON để thử lại. Log: "+path+"\n");
            }
            catch{}
        }
        public static void Stop()
        {
            if(!started)return;started=false;
            AcApp.SystemVariableChanged-=VariableChanged;
            AcApp.DocumentManager.DocumentActivated-=DocumentActivated;
            if(pending){AcApp.Idle-=OnIdle;pending=false;}
            if(createdHook){RibbonServices.RibbonPaletteSetCreated-=PaletteCreated;createdHook=false;}
            if(palette!=null){palette.WorkspaceLoaded-=WorkspaceLoaded;palette=null;}
            tab=null;selectRequested=false;
        }

    }
}
