using System;
using System.IO;

namespace MedGas2024.Core
{
    public static class Spacing
    {
        // Coordinates measured by DBText at rotation 0 in the same font and nominal height.
        public static void FitSymmetric(GasRow r,double minX,double minY,double maxX,double maxY,double paddingRatio)
        {
            if((r.RotationMode!="R" && r.RotationMode!="U") || Math.Abs(r.RotationDegrees)>1e-10)
                throw new InvalidDataException("Tự căn đối xứng hỗ trợ chữ dễ đọc U=0 hoặc theo đường R=0; chọn lại hướng chữ hoặc tắt Tự căn.");
            if(!Profile.Finite(minX) || !Profile.Finite(minY) || !Profile.Finite(maxX) || !Profile.Finite(maxY) || maxX<=minX || maxY<=minY)
                throw new InvalidDataException("Kích thước glyph không hợp lệ.");
            if(!Profile.Finite(paddingRatio) || paddingRatio<=0 || !Profile.Finite(r.TextScale) || r.TextScale<=0)
                throw new InvalidDataException("Khoảng hở/cao chữ không hợp lệ.");
            double padding=r.TextScale*paddingRatio;
            // Place the insertion at the gap centre, with the glyph centred around it.
            // The projected glyph interval is symmetric about this anchor under a local half-turn.
            // U=0 uses this as an experimental layout; AutoCAD's native upright rendering needs visual QA.
            r.GapBefore=(maxX-minX)/2+padding; r.GapAfter=r.GapBefore;
            r.OffsetX=-(minX+maxX)/2; r.OffsetY=-(minY+maxY)/2;
        }
    }
}
