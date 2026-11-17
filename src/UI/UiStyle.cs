using System.Drawing;
using System.Windows.Forms;

namespace MedGas2024.UI
{
    // System fonts keep the UI consistent across machines without shipping a font file.
    // Drawing text continues to use Profile.FontFile / FontBold independently.
    // Colours and the larger 11 pt body come from UiTheme (Autodesk-style dialogs).
    internal static class UiStyle
    {
        public static Font Body(){return new Font(UiTheme.Family,UiTheme.BodySize,FontStyle.Regular);}
        public static Font Emphasis(float size){return new Font("Segoe UI Semibold",size,FontStyle.Regular);}
        public static void Grid(DataGridView grid)
        {
            grid.RowTemplate.Height=38;
            grid.ColumnHeadersHeight=42;
            grid.ColumnHeadersDefaultCellStyle.Font=Emphasis(10.5F);
            grid.DefaultCellStyle.Padding=new Padding(3,1,3,1);
            UiTheme.Grid(grid);
        }
    }
}
