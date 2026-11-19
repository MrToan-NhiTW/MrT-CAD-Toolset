using System;
using System.Windows.Input;
using Autodesk.Windows;

namespace MedGas2024.Plugin
{
    internal sealed class RibbonCommandHandler : ICommand
    {
        private readonly Func<bool> hasDocument;
        private readonly Action<string> sendCommand;
        public RibbonCommandHandler(Func<bool> documentAvailable,Action<string> execute)
        {
            hasDocument=documentAvailable;sendCommand=execute;
        }
        public bool CanExecute(object parameter){return hasDocument();}
        public void Execute(object parameter)
        {
            if(!hasDocument())return;
            // Autodesk passes the source RibbonButton to ICommand.Execute,
            // rather than passing its CommandParameter string as WPF normally does.
            var button=parameter as RibbonButton;
            string name=button==null?parameter as string:button.CommandParameter as string;
            if(String.IsNullOrWhiteSpace(name))return;
            name=name.Trim().TrimStart('_').ToUpperInvariant();
            if(name!="MRTDENSITY"&&name!="MRTTABLEMASK"&&name!="MRTFONT"&&name!="MRTFONTALL"&&name!="MRTFONTCHECK"&&name!="MRTZ0"&&name!="MRTZ0SCAN"&&name!="MRTJOIN"&&name!="MRTBREAK"&&name!="MRTALIASES"&&name!="MRTVIEW"&&name!="MRTISOADD"&&name!="MRTBLOCKLAY"&&name!="MRTBLOCKMERGE"&&name!="MRTSELLAYER"&&name!="MRTALIGNR"&&name!="MRTLEGEND"&&name!="MRTLEGEDIT"&&name!="MRTLEGSYNC"&&name!="MRTROOMS"&&name!="MRTROOMLIST"&&name!="MRTROOMINFO"&&name!="MRTTOOLS"&&name!="MRTHELP"&&name!="MRTPIPE"&&name!="MRTSCALE"&&name!="MRTFIXTEXT"&&name!="MRTGROUP"&&name!="MRTR0"&&name!="MRTISO"&&name!="MRTUNISO"&&name!="MRTSEL"&&name!="MRTPICKSYSTEM")return;
            sendCommand("_"+name+" ");
        }
        public event EventHandler CanExecuteChanged
        {
            add{CommandManager.RequerySuggested+=value;}
            remove{CommandManager.RequerySuggested-=value;}
        }
    }
}
