using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
public static class Assets
{
    static readonly string[] Keys={"mrttablemask","mrtfont","mrtfontall","mrtfontcheck","mrtjoin","mrtz0","mrtz0scan","mrtbreak","mrtaliases","mrttools","mrtpipe","mrtscale","mrtr0","mrtsel","mrtiso","mrtuniso","mrthelp","mrtsellayer","mrtalignr","mrtview","mrtisoadd","mrtblocklay","mrtblockmerge","mrtlegend","mrtlegedit","mrtlegsync","mrtrooms","mrtdensity","mrtz0pl"};
    static Color Blue(bool light){return ColorTranslator.FromHtml(light?"#0067A4":"#67C5FF");} static Color Green(bool light){return ColorTranslator.FromHtml(light?"#007A4A":"#74DDA5");} static Color Yellow(bool light){return ColorTranslator.FromHtml(light?"#996300":"#FFCC67");} static Color Purple(bool light){return ColorTranslator.FromHtml(light?"#71419B":"#D4A6FF");} static Color Orange(bool light){return ColorTranslator.FromHtml(light?"#A85100":"#FFB77A");}
    static void Line(Graphics g,Color c,float width,params float[] a){using(var p=new Pen(c,width){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})for(int i=0;i+3<a.Length;i+=2)g.DrawLine(p,a[i],a[i+1],a[i+2],a[i+3]);}
    static void Box(Graphics g,Color c,float x,float y,float w,float h,float thickness=1.3f){using(var p=new Pen(c,thickness))g.DrawRectangle(p,x,y,w,h);}
    static void Circle(Graphics g,Color c,float x,float y,float r,bool fill=false){using(var p=new Pen(c,1.7f))using(var b=new SolidBrush(c)){if(fill)g.FillEllipse(b,x-r,y-r,2*r,2*r);else g.DrawEllipse(p,x-r,y-r,2*r,2*r);}}
    static void Arrow(Graphics g,Color c,float x,float y,float dx,float dy){float xx=x+dx,yy=y+dy;Line(g,c,1.6f,x,y,xx,yy);double a=Math.Atan2(dy,dx);Line(g,c,1.6f,xx,yy,xx+(float)(4*Math.Cos(a+2.5)),yy+(float)(4*Math.Sin(a+2.5)));Line(g,c,1.6f,xx,yy,xx+(float)(4*Math.Cos(a-2.5)),yy+(float)(4*Math.Sin(a-2.5)));}
    static void Text(Graphics g,Color c,string s,float x,float y,float size){using(var f=new Font("Segoe UI",size,FontStyle.Bold,GraphicsUnit.Pixel))using(var b=new SolidBrush(c))g.DrawString(s,f,b,x,y,StringFormat.GenericTypographic);}
    static void Plus(Graphics g,Color c,float x,float y,bool light){Circle(g,c,x,y,4.8f,true);Color cut=light?Color.White:Color.FromArgb(255,33,43,54);Line(g,cut,1.25f,x-2.8f,y,x+2.8f,y);Line(g,cut,1.25f,x,y-2.8f,x,y+2.8f);}
    static void Draw(Graphics g,string k,bool light){
        var ink=ColorTranslator.FromHtml(light?"#253241":"#E8EDF4");var blue=Blue(light);var green=Green(light);var yellow=Yellow(light);var purple=Purple(light);var orange=Orange(light);
        switch(k){
        case "mrttablemask":Box(g,ink,3,3,21,22);Line(g,ink,1.2f,3,10,24,10);Line(g,ink,1.2f,10,10,10,25);Box(g,blue,9,17,20,12,2);Arrow(g,green,21,26,0,-12);break;
        case "mrtfont":Text(g,ink,"A",2,3,23);Arrow(g,green,18,20,10,0);break;
        case "mrtfontall":Text(g,ink,"A",2,3,23);Circle(g,purple,24,23,6);Text(g,purple,"+",20,18,10);break;
        case "mrtfontcheck":Text(g,ink,"A",2,3,23);Circle(g,green,23,22,6);Line(g,green,2,27,26,30,29);break;
        case "mrtz0":Text(g,ink,"Z",2,2,15);Arrow(g,orange,20,5,0,17);Text(g,orange,"0",17,20,10);break;
        case "mrtz0scan":Text(g,ink,"Z",2,2,15);Circle(g,green,19,19,7);Line(g,green,2,24,24,29,29);break;
        case "mrtjoin":Line(g,ink,2,3,9,12,9,15,16);Line(g,ink,2,3,23,12,23,15,16);Line(g,green,2.4f,15,16,29,16);Circle(g,green,15,16,2,true);break;
        case "mrtbreak":Line(g,ink,2,3,16,12,16);Line(g,ink,2,20,16,29,16);Line(g,orange,2,13,9,19,23);Circle(g,orange,16,16,2,true);break;
        case "mrtaliases":Box(g,ink,3,6,26,20);Text(g,green,"6",11,7,16);break;
        case "mrttools": for(int i=0;i<3;i++){Line(g,ink,1.4f,3,7+i*8,28,7+i*8);Circle(g,blue,10+i*4,7+i*8,2.4f,true);}break;
        case "mrtpipe": Line(g,ink,2.5f,3,23,13,23,13,8,27,8);Arrow(g,blue,25,21,-10,0);break;
        case "mrtscale":Line(g,ink,1.3f,4,7,28,7);Line(g,ink,3.4f,4,25,28,25);Arrow(g,blue,16,16,10,0);Arrow(g,blue,16,16,-10,0);break;
        case "mrtr0":Line(g,ink,2,3,23,29,23);Text(g,ink,"R",3,2,12);Arrow(g,yellow,14,10,13,0);Text(g,yellow,"0",23,0,10);break;
        case "mrtsel":Box(g,ink,4,4,18,18);Line(g,green,1.8f,17,15,27,24,22,25,20,29,17,15);break;
        case "mrtiso":Box(g,ink,4,4,24,24);Circle(g,green,16,16,4.5f);Line(g,green,1.6f,3,1,3,8);break;
        case "mrtuniso":Box(g,ink,4,4,24,24);for(int i=0;i<5;i++)Circle(g,green,9+(i%3)*7,10+(i/3)*9,1.9f,true);break;
        case "mrthelp":Circle(g,ink,16,16,13);Text(g,ink,"?",11,4,21);break;
        case "mrtsellayer":for(int i=0;i<3;i++){float y=5+i*6;Line(g,ink,1.2f,4,y+4,14,y,24,y+4,14,y+8,4,y+4);}Box(g,green,21,21,9,9,1.4f);Line(g,green,1.4f,22.5f,25,24.5f,27,28.5f,22.8f);break;
        case "mrtalignr":Line(g,ink,1.7f,3,7,28,7);Arrow(g,yellow,4,16,23,0);Text(g,ink,"R",12,19,11);break;
        case "mrtview":Line(g,ink,1.5f,2,16,8,10,16,7,24,10,30,16,24,22,16,25,8,22,2,16);Circle(g,green,16,16,4);break;
        case "mrtisoadd":Box(g,ink,3,3,19,19);Circle(g,green,12.5f,12.5f,3,true);Plus(g,green,24,24,light);break;
        case "mrtblocklay":Line(g,ink,1.4f,4,9,15,3,26,9,26,21,15,28,4,21,4,9,15,15,26,9);Line(g,ink,1.4f,15,15,15,28);Circle(g,purple,23,23,4.5f);Line(g,purple,2,26.4f,26.4f,30,30);break;
        case "mrtblockmerge":Box(g,ink,2,4,9,9);Box(g,ink,2,19,9,9);Arrow(g,purple,13,9,8,7);Arrow(g,purple,13,23,8,-7);Box(g,purple,23,11,7,10,1.8f);break;
        case "mrtlegend":case "mrtlegedit":case "mrtlegsync":Box(g,ink,3,3,22,25);for(int i=0;i<3;i++)Line(g,ink,1.2f,3,10+i*6,25,10+i*6);Line(g,ink,1.2f,11,10,11,28);if(k=="mrtlegend")Plus(g,orange,25,25,light);else if(k=="mrtlegsync"){Line(g,orange,2,13,19,28,19);Line(g,orange,2,24,15,28,19);Line(g,orange,2,24,23,28,19);}else Line(g,orange,3,20,28,29,19);break;
        // Pipe with repeated label blocks; the yellow dimension shows the label rhythm being tuned.
        case "mrtdensity":Line(g,ink,2.2f,2,22,6,22);Line(g,ink,2.2f,12,22,20,22);Line(g,ink,2.2f,26,22,30,22);
            Box(g,blue,6.5f,18.5f,5,7,1.6f);Box(g,blue,20.5f,18.5f,5,7,1.6f);
            Line(g,yellow,1.4f,9,6,9,13);Line(g,yellow,1.4f,23,6,23,13);Arrow(g,yellow,16,9.5f,6.5f,0);Arrow(g,yellow,16,9.5f,-6.5f,0);break;
        // Slanted 3D line dropped onto the plane and becoming one polyline.
        case "mrtz0pl":Line(g,ink,1.3f,3,27,29,27);Line(g,orange,1.8f,4,6,14,13);Arrow(g,orange,9,12,0,9);Line(g,green,2.6f,4,23,15,23,24,17);Circle(g,green,15,23,1.8f,true);break;
        case "mrtrooms":Box(g,ink,4,5,24,22);Line(g,orange,2,9,12,23,12);Line(g,orange,2,9,17,23,17);Line(g,ink,1.5f,9,22,18,22);break;
        }
    }
    public static void Main(string[] args){string root=args[0];Directory.CreateDirectory(root);foreach(string key in Keys)foreach(bool light in new[]{false,true})foreach(int size in new[]{16,32})using(var b=new Bitmap(size,size,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(b)){g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.ScaleTransform(size/32f,size/32f);Draw(g,key,light);b.Save(Path.Combine(root,key+(light?"-light":"-dark")+size+".png"),ImageFormat.Png);}
      using(var b=new Bitmap(48,48))using(var g=Graphics.FromImage(b)){g.Clear(Color.FromArgb(26,102,152));using(var f=new Font("Segoe UI",20,FontStyle.Bold))g.DrawString("T",f,Brushes.White,12,5);using(var png=new MemoryStream()){b.Save(png,ImageFormat.Png);byte[] bytes=png.ToArray();using(var output=new BinaryWriter(File.Create(Path.Combine(root,"MrT.ico")))){output.Write((short)0);output.Write((short)1);output.Write((short)1);output.Write((byte)48);output.Write((byte)48);output.Write((byte)0);output.Write((byte)0);output.Write((short)1);output.Write((short)32);output.Write(bytes.Length);output.Write(22);output.Write(bytes);}}}
    }
}
