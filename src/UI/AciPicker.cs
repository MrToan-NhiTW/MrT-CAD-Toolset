using System;
using System.Drawing;
using System.Windows.Forms;

namespace MedGas2024.UI
{
    public sealed class AciPicker : UserControl
    {
        public event EventHandler ValueChanged;
        private readonly NumericUpDown index=new NumericUpDown();
        private readonly Label selected=new Label();
        public int Value {get {return (int)index.Value;} set {index.Value=value;}}
        public AciPicker(int initial,Func<int,int?> nativePicker=null)
        {
            Height=80; Name="AciPicker";
            var flow=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=true,Margin=Padding.Empty}; Controls.Add(flow);
            flow.Controls.Add(new Label {Text="Màu ACI:",AutoSize=true,Margin=new Padding(0,6,5,0)});
            index.Name="AciIndex";index.Minimum=1;index.Maximum=255;index.Width=60;index.Value=initial;flow.Controls.Add(index);
            var choose=new Button {Name="NativeColor",Text="Bảng màu AutoCAD…",AutoSize=true,Enabled=nativePicker!=null};
            choose.Click+=delegate {var result=nativePicker(Value);if(result.HasValue)Value=result.Value;};flow.Controls.Add(choose);
            selected.AutoSize=true;selected.Margin=new Padding(6,6,0,0);flow.Controls.Add(selected);flow.SetFlowBreak(selected,true);
            for(int i=1;i<=9;i++)
            {
                int number=i;var b=new Button {Name="Aci"+i,Text=i.ToString(),Size=new Size(34,27),BackColor=BasicColor(i),ForeColor=i==5?Color.White:Color.Black,FlatStyle=FlatStyle.Flat};
                b.Click+=delegate {Value=number;};flow.Controls.Add(b);
            }
            flow.Controls.Add(new Label {Text="Chọn ô màu hoặc nhập mã 1–255",AutoSize=true,Margin=new Padding(8,6,0,0)});
            index.ValueChanged+=delegate {RefreshLabel();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);};RefreshLabel();
        }
        private void RefreshLabel() {selected.Text="Đang chọn: ACI "+Value;}
        public static Color BasicColor(int aci)
        {
            switch(aci) {case 1:return Color.Red;case 2:return Color.Yellow;case 3:return Color.Lime;case 4:return Color.Cyan;case 5:return Color.Blue;case 6:return Color.Magenta;case 7:return Color.White;case 8:return Color.Gray;case 9:return Color.Silver;default:return Color.White;}
        }
    }
}
