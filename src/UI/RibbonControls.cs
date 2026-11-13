using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MedGas2024.UI
{
    // Compact, code-drawn icons and always-visible groups; no Office dependency.
    public static class RibbonIcons
    {
        public static void Draw(Graphics graphics,string key,Rectangle bounds,Color ink,bool muted=false,bool light=false)
        {
            var saved=graphics.Save();
            try
            {
                graphics.SmoothingMode=SmoothingMode.AntiAlias;
                graphics.TranslateTransform(bounds.Left,bounds.Top);graphics.ScaleTransform(bounds.Width/32F,bounds.Height/32F);
                Color pale=muted?Color.FromArgb(238,240,242):light?Color.FromArgb(55,128,173):Color.FromArgb(230,240,250);
                Color green=muted?ink:light?Color.White:Color.FromArgb(38,143,100);
                using(var pen=new Pen(ink,1.7F))using(var fill=new SolidBrush(pale))using(var accent=new Pen(green,2F))
                {
                    pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;accent.StartCap=accent.EndCap=LineCap.Round;
                    switch(key)
                    {
                        case "direction":
                            graphics.DrawLine(pen,3,9,25,9);graphics.DrawLines(accent,new[]{new Point(20,4),new Point(25,9),new Point(20,14)});graphics.DrawLine(pen,9,17,9,29);graphics.DrawLines(accent,new[]{new Point(4,24),new Point(9,29),new Point(14,24)});break;
                        case "assign":
                            graphics.DrawLine(pen,3,7,29,7);graphics.DrawLine(pen,3,25,29,25);graphics.DrawLine(accent,16,11,16,22);graphics.DrawLines(accent,new[]{new Point(11,17),new Point(16,22),new Point(21,17)});break;
                        case "pick":
                            graphics.DrawLine(pen,3,26,29,26);graphics.DrawPolygon(pen,new[]{new Point(10,20),new Point(7,17),new Point(21,3),new Point(24,6)});graphics.DrawLine(accent,16,5,25,14);graphics.DrawLine(accent,4,23,8,19);break;
                        case "open":
                            graphics.FillPolygon(fill,new[]{new Point(4,9),new Point(13,9),new Point(16,12),new Point(28,12),new Point(26,26),new Point(4,26)});
                            graphics.DrawLines(pen,new[]{new Point(4,26),new Point(4,9),new Point(13,9),new Point(16,12),new Point(28,12)});
                            graphics.DrawPolygon(pen,new[]{new Point(4,26),new Point(8,15),new Point(29,15),new Point(25,26)});break;
                        case "save":
                            graphics.FillRectangle(fill,5,5,22,23);graphics.DrawPolygon(pen,new[]{new Point(5,5),new Point(24,5),new Point(28,9),new Point(28,28),new Point(5,28)});
                            graphics.DrawRectangle(pen,10,5,12,8);graphics.DrawLine(pen,19,7,19,11);graphics.DrawRectangle(pen,10,19,13,9);break;
                        case "import":case "export":
                            graphics.DrawLines(pen,new[]{new Point(5,20),new Point(5,27),new Point(27,27),new Point(27,20)});graphics.DrawLine(pen,5,20,11,20);graphics.DrawLine(pen,21,20,27,20);
                            graphics.DrawLine(accent,16,5,16,21);
                            if(key=="import")graphics.DrawLines(accent,new[]{new Point(11,16),new Point(16,21),new Point(21,16)});
                            else graphics.DrawLines(accent,new[]{new Point(11,10),new Point(16,5),new Point(21,10)});break;
                        case "new":
                            graphics.FillRectangle(fill,6,4,18,24);graphics.DrawLines(pen,new[]{new Point(24,11),new Point(24,4),new Point(6,4),new Point(6,28),new Point(17,28)});
                            graphics.DrawLine(pen,10,10,18,10);graphics.DrawLine(pen,10,15,17,15);graphics.DrawLine(accent,24,18,24,29);graphics.DrawLine(accent,18,24,30,24);break;
                        case "color":
                            Color[] colors=muted?new[]{pale,pale,pale,pale}:new[]{Color.FromArgb(81,139,201),Color.FromArgb(53,165,126),Color.FromArgb(242,181,63),Color.FromArgb(214,105,104)};
                            for(int i=0;i<4;i++){int x=5+(i%2)*12,y=5+(i/2)*12;using(var b=new SolidBrush(colors[i]))graphics.FillRectangle(b,x,y,10,10);graphics.DrawRectangle(pen,x,y,10,10);}break;
                        case "fit":
                            graphics.DrawLine(pen,3,5,3,27);graphics.DrawLine(pen,29,5,29,27);graphics.DrawLines(pen,new[]{new Point(11,23),new Point(16,8),new Point(21,23)});graphics.DrawLine(pen,13,18,19,18);
                            graphics.DrawLine(accent,5,16,9,16);graphics.DrawLine(accent,23,16,27,16);break;
                        case "undo":
                            graphics.DrawLines(pen,new[]{new Point(11,6),new Point(5,12),new Point(12,18)});graphics.DrawBezier(pen,5,12,27,5,31,24,20,28);break;
                        case "inspect":
                            graphics.FillRectangle(fill,5,4,17,24);graphics.DrawLines(pen,new[]{new Point(21,11),new Point(21,4),new Point(5,4),new Point(5,28),new Point(15,28)});graphics.DrawLine(pen,9,9,16,9);graphics.DrawLine(pen,9,14,14,14);
                            graphics.DrawEllipse(accent,15,14,11,11);graphics.DrawLine(accent,24,24,30,30);break;
                        case "apply":
                            graphics.FillPolygon(fill,new[]{new Point(3,10),new Point(15,4),new Point(27,10),new Point(15,16)});graphics.DrawPolygon(pen,new[]{new Point(3,10),new Point(15,4),new Point(27,10),new Point(15,16)});
                            graphics.DrawLines(pen,new[]{new Point(3,16),new Point(15,22),new Point(23,18)});graphics.DrawLines(pen,new[]{new Point(3,22),new Point(15,28)});graphics.DrawLines(accent,new[]{new Point(19,25),new Point(23,29),new Point(30,20)});break;
                        case "samples":
                            graphics.DrawLine(pen,4,10,28,10);graphics.DrawLines(pen,new[]{new Point(24,6),new Point(28,10),new Point(24,14)});graphics.DrawLine(pen,4,24,28,24);graphics.DrawLines(pen,new[]{new Point(8,20),new Point(4,24),new Point(8,28)});break;
                        case "edit":
                            graphics.DrawPolygon(pen,new[]{new Point(6,22),new Point(21,7),new Point(26,12),new Point(11,27),new Point(5,28)});graphics.DrawLine(pen,18,10,23,15);break;
                        case "delete":
                            graphics.DrawLine(pen,6,9,26,9);graphics.DrawLines(pen,new[]{new Point(12,9),new Point(12,5),new Point(20,5),new Point(20,9)});graphics.DrawPolygon(pen,new[]{new Point(9,9),new Point(23,9),new Point(21,28),new Point(11,28)});graphics.DrawLine(pen,14,14,14,23);graphics.DrawLine(pen,18,14,18,23);break;
                    }
                }
            }
            finally{graphics.Restore(saved);}
        }
    }
    public sealed class RibbonButton : Button
    {
        public string IconKey {get;set;}
        private bool hover,pressed;
        public RibbonButton(string icon)
        {
            IconKey=icon;Font=UiStyle.Emphasis(10.5F);AutoSize=false;FlatStyle=FlatStyle.Flat;BackColor=Color.White;ForeColor=Color.FromArgb(32,65,91);Margin=new Padding(2);TabStop=true;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        }
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            float scale=DeviceDpi/96F;bool blue=Enabled&&BackColor.B>BackColor.R+30&&BackColor.R<100;
            Color face=!Enabled?Color.FromArgb(239,242,245):pressed?Color.FromArgb(211,229,243):hover&&!blue?Color.FromArgb(233,243,252):BackColor;
            Color ink=!Enabled?Color.FromArgb(149,159,170):blue?Color.White:ForeColor;
            using(var b=new SolidBrush(face))e.Graphics.FillRectangle(b,ClientRectangle);
            using(var p=new Pen(!Enabled?Color.FromArgb(225,230,235):hover?Color.FromArgb(102,155,197):blue?BackColor:Color.FromArgb(233,237,242)))e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1);
            int size=(int)(28*scale);var icon=new Rectangle((Width-size)/2,(int)(7*scale),size,size);RibbonIcons.Draw(e.Graphics,IconKey,icon,ink,!Enabled,blue);
            var label=new Rectangle(3,icon.Bottom+(int)(5*scale),Width-6,Math.Max(10,Height-icon.Bottom-(int)(7*scale)));
            TextRenderer.DrawText(e.Graphics,Text,Font,label,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,Width-7,Height-7));
        }
    }
    public sealed class RibbonGroup : UserControl
    {
        private readonly TableLayoutPanel content=new TableLayoutPanel();
        private readonly Label caption=new Label();
        public RibbonGroup(string name,string title)
        {
            Name=name;AccessibleName=title;AccessibleRole=AccessibleRole.Grouping;Dock=DockStyle.Fill;BackColor=Color.White;Margin=new Padding(0,0,8,0);Padding=new Padding(5,5,5,0);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,25));Controls.Add(root);
            content.Dock=DockStyle.Fill;content.RowCount=1;content.RowStyles.Add(new RowStyle(SizeType.Percent,100));content.Margin=Padding.Empty;root.Controls.Add(content,0,0);
            caption.Name=name+"Caption";caption.Text=title;caption.Dock=DockStyle.Fill;caption.Margin=Padding.Empty;caption.TextAlign=ContentAlignment.MiddleCenter;caption.BackColor=Color.FromArgb(235,242,248);caption.ForeColor=Color.FromArgb(45,76,99);caption.Font=UiStyle.Emphasis(10F);root.Controls.Add(caption,0,1);
        }
        public void AddCommands(params Button[] buttons){foreach(var b in buttons)AddCommand(b);}
        public void AddCommand(Button button,float weight=100)
        {
            int index=content.ColumnCount;content.ColumnCount++;content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,weight));
            var host=new Panel{Name=button.Name+"Host",Dock=DockStyle.Fill,Margin=new Padding(2,0,2,4)};
            button.Dock=DockStyle.Fill;button.Margin=Padding.Empty;host.Controls.Add(button);content.Controls.Add(host,index,0);
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(Color.FromArgb(190,207,222)))e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);}
    }
}
