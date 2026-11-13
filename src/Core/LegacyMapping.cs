using System;
using System.Collections.Generic;
namespace MedGas2024.Core
{
    public sealed class LegacyMapRow
    {
        public bool Use{get;set;} public string Source{get;set;} public string Label{get;set;} public int Count{get;set;}
        public string CurrentLayers{get;set;} public string TargetLayer{get;set;} public string TargetType{get;set;} public bool SyncLayer{get;set;} public bool CopySourceToExisting{get;set;} public string State{get;set;}
    }
    public sealed class LegacyMapCatalog
    {
        public const string KeepLayer="(Giữ Layer hiện tại)";
        public string Fingerprint;public List<LegacyMapRow> Rows=new List<LegacyMapRow>();public List<string> Layers=new List<string>();public List<string> Types=new List<string>();
    }
}
