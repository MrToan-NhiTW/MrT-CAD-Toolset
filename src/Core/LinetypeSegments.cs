using System;
namespace MedGas2024.Core
{
    public static class LinetypeSegments
    {
        // Native text access is valid only for a text-bearing segment, never for
        // plain dashes/gaps (no style) or numbered SHX shapes.
        public static string ReadText(bool hasStyle,int shapeNumber,Func<string> readText)
        {
            if(!hasStyle||shapeNumber!=0)return "";
            return readText()??"";
        }
    }
}
