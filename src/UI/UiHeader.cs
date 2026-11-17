using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MedGas2024.UI
{
    // Dark title band shown at the top of every Mr.T dialog:
    //   [blue icon badge]  MR.T CAD TOOLSET
    //                      Title of the dialog                 subtitle · version
    //   ───────────────────────── Autodesk-blue accent line ─────────────────────────
    // Title comes from the dialog's big in-layout title (moved here) or Form.Text.
    public sealed class UiHeader : Control
    {
        readonly Form form;
        Label titleSource;
        public UiHeader(Form owner)
        {
            form=owner;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            float scale=owner.DeviceDpi/96F;Height=(int)Math.Round(64*scale);
            BackColor=UiTheme.Banner;TabStop=false;AccessibleRole=AccessibleRole.TitleBar;
            owner.TextChanged+=delegate{Invalidate();};
        }
        public Label TitleSource
        {
            get{return titleSource;}
            set{titleSource=value;if(value!=null)value.TextChanged+=delegate{Invalidate();};Invalidate();}
        }
        public string IconKey{get{return UiTheme.IconFor(form);}}

        // Form.Text is "Mr.T Title | context | version"; the band shows the title large and
        // the rest as a quiet subtitle on the right.
        public string TitleText
        {
            get
            {
                string t=titleSource!=null&&!String.IsNullOrWhiteSpace(titleSource.Text)?titleSource.Text:First(form.Text);
                return StripBrand(t);
            }
        }
        public string Subtitle
        {
            get
            {
                var parts=(form.Text??"").Split('|');var rest=new List<string>();
                for(int i=1;i<parts.Length;i++)
                {
                    string p=parts[i].Trim();
                    if(p.Length==0||StripBrand(p)==TitleText||p.StartsWith("Mr.T CAD tool set",StringComparison.OrdinalIgnoreCase))continue;
                    rest.Add(StripBrand(p));
                }
                return String.Join("  ·  ",rest.ToArray());
            }
        }
        static string First(string s){s=s??"";int bar=s.IndexOf('|');return (bar>=0?s.Substring(0,bar):s).Trim();}
        static string StripBrand(string s)
        {
            s=(s??"").Trim();
            foreach(var p in new[]{"Mr.T - ","Mr.T – ","Mr.T "})if(s.StartsWith(p,StringComparison.OrdinalIgnoreCase))return s.Substring(p.Length).Trim();
            return s;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float s=DeviceDpi/96F;
            using(var back=new LinearGradientBrush(ClientRectangle.Width>0?ClientRectangle:new Rectangle(0,0,1,1),Color.FromArgb(38,46,58),UiTheme.Banner,LinearGradientMode.Horizontal))g.FillRectangle(back,ClientRectangle);
            int bar=(int)Math.Max(2,Math.Round(3*s));
            using(var accent=new SolidBrush(UiTheme.Accent))g.FillRectangle(accent,0,Height-bar,Width,bar);

            int pad=(int)(16*s),badge=(int)(40*s),top=(Height-bar-badge)/2;
            var badgeRect=new Rectangle(pad,top,badge,badge);
            DrawBadge(g,badgeRect,IconKey);

            int x=badgeRect.Right+(int)(12*s);
            using(var tag=new Font("Segoe UI Semibold",8F))using(var title=new Font("Segoe UI Semibold",15F))using(var sub=new Font(UiTheme.Family,9.5F))
            {
                string subtitle=Subtitle;
                Size subSize=subtitle.Length==0?Size.Empty:TextRenderer.MeasureText(g,subtitle,sub);
                int subWidth=Math.Min(subSize.Width,Width/3);
                int titleRight=Width-pad-(subWidth>0?subWidth+(int)(16*s):0);
                TextRenderer.DrawText(g,"MR.T CAD TOOLSET",tag,new Rectangle(x,top-(int)(2*s),titleRight-x,(int)(16*s)),UiTheme.BannerTag,TextFormatFlags.Left|TextFormatFlags.Top|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);
                TextRenderer.DrawText(g,TitleText,title,new Rectangle(x,top+(int)(11*s),titleRight-x,(int)(30*s)),Color.White,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
                if(subWidth>0)
                    TextRenderer.DrawText(g,subtitle,sub,new Rectangle(Width-pad-subWidth,0,subWidth,Height-bar),UiTheme.BannerSub,TextFormatFlags.Right|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            }
        }

        public static void DrawBadge(Graphics g,Rectangle r,string key)
        {
            var mode=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Rounded(r,Math.Max(4,r.Width/5)))
            {
                using(var fill=new LinearGradientBrush(r,Color.FromArgb(28,170,232),UiTheme.AccentDark,LinearGradientMode.ForwardDiagonal))g.FillPath(fill,path);
                using(var rim=new Pen(Color.FromArgb(70,255,255,255)))g.DrawPath(rim,path);
            }
            int inset=r.Width/6;
            RibbonIcons.Draw(g,key,new Rectangle(r.Left+inset,r.Top+inset,r.Width-2*inset,r.Height-2*inset),Color.White,false,true);
            g.SmoothingMode=mode;
        }
        static GraphicsPath Rounded(Rectangle r,int radius)
        {
            int d=radius*2;var p=new GraphicsPath();
            p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }

        static readonly Dictionary<string,Icon> icons=new Dictionary<string,Icon>();
        // Window/taskbar icon: same badge, drawn once per key and kept for the session.
        public static Icon WindowIcon(string key)
        {
            Icon icon;if(icons.TryGetValue(key,out icon))return icon;
            using(var bmp=new Bitmap(32,32))
            {
                using(var g=Graphics.FromImage(bmp)){g.Clear(Color.Transparent);DrawBadge(g,new Rectangle(0,0,31,31),key);}
                icon=Icon.FromHandle(bmp.GetHicon());
            }
            icons[key]=icon;return icon;
        }
    }
}
