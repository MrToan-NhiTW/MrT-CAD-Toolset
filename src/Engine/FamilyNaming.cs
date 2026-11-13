using System;
using Autodesk.AutoCAD.DatabaseServices;
using MedGas2024.Core;
namespace MedGas2024.Engine
{
    internal static class FamilyNaming
    {
        const string Key="MRT_FAMILY_NAMING_V1";
        public static void Write(LayerTableRecord layer,Transaction tr,string prefix,string code,string suffix,int diameter)
        {
            if(layer.ExtensionDictionary.IsNull)layer.CreateExtensionDictionary();var dict=(DBDictionary)tr.GetObject(layer.ExtensionDictionary,OpenMode.ForWrite);Xrecord record;
            if(dict.Contains(Key))record=(Xrecord)tr.GetObject(dict.GetAt(Key),OpenMode.ForWrite);else{record=new Xrecord();dict.SetAt(Key,record);tr.AddNewlyCreatedDBObject(record,true);}
            using(var data=new ResultBuffer(new TypedValue(1000,"MrT.FamilyNaming:1"),new TypedValue(1000,prefix??""),new TypedValue(1000,code??""),new TypedValue(1000,suffix??""),new TypedValue(1071,diameter)))record.Data=data;
        }
        public static bool Read(LayerTableRecord layer,Transaction tr,out string prefix,out string code,out string suffix)
        {
            prefix=code=suffix=null;if(layer.ExtensionDictionary.IsNull)return false;var dict=(DBDictionary)tr.GetObject(layer.ExtensionDictionary,OpenMode.ForRead);if(!dict.Contains(Key))return false;
            var record=(Xrecord)tr.GetObject(dict.GetAt(Key),OpenMode.ForRead);using(var data=record.Data){var v=data==null?new TypedValue[0]:data.AsArray();if(v.Length!=5||Convert.ToString(v[0].Value)!="MrT.FamilyNaming:1")return false;prefix=Convert.ToString(v[1].Value);code=Convert.ToString(v[2].Value);suffix=Convert.ToString(v[3].Value);int diameter=Convert.ToInt32(v[4].Value);return String.Equals(layer.Name,Profile.ComposeName(prefix,code,diameter,suffix),StringComparison.OrdinalIgnoreCase);}
        }
    }
}
