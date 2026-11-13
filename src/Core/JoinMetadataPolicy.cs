using System;
namespace MedGas2024.Core {
    // Allow only the exact empty tag observed on the copied input. Other XData
    // is conservatively rejected; unknown application semantics are not inferred.
    public static class JoinMetadataPolicy {
        public static bool IsSupported(int[] codes,string[] values) {
            if(codes==null || values==null || codes.Length!=values.Length)return false;
            if(codes.Length==0)return true;
            return codes.Length==2 && codes[0]==1001 && codes[1]==1000 &&
                String.Equals(values[0],"AcDbBlockRepETag",StringComparison.OrdinalIgnoreCase) &&
                values[1]=="";
        }
    }
}