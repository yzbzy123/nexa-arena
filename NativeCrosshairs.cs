using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace NexaArena
{
    internal static class NativeCrosshairRenderer
    {
        internal static Cs2CrosshairData Rescale(Cs2CrosshairData data,int height)
        {
            var result=Cs2ShareCode.Decode(Cs2ShareCode.Encode(data));float factor=(float)height/data.screenHeight;
            Func<int,bool,int> scale=(value,negative)=>value>0?Math.Max(1,(int)Math.Round((float)(value*factor),MidpointRounding.AwayFromZero)):negative&&value<0?Math.Min(-1,(int)Math.Round((float)(value*factor),MidpointRounding.AwayFromZero)):value;
            result.screenHeight=height;result.thickness=scale(data.thickness,false);result.length=scale(data.length,false);result.gap=scale(data.gap,true);result.dynamicSpreadLimit=scale(data.dynamicSpreadLimit,false);result.splitDistance=scale(data.splitDistance,false);return result;
        }
        internal static List<Rectangle> Rectangles(Cs2CrosshairData data,int center)
        {
            if(data.style!=4&&data.style!=6)throw new InvalidOperationException("此样式需要动态或曲线渲染，未显示近似预览。");
            var result=new List<Rectangle>();int t=data.thickness,l=data.length,g=Math.Max(0,data.gap),before=(t+1)/2,after=t-before,odd=before==after?0:-1;
            if(t<=0)return result;
            if(data.centerDot||data.style==6)result.Add(new Rectangle(center-before,center-before,t,t));
            if(data.style==4&&l>0){result.Add(new Rectangle(center-g-l,center-before,l,t));result.Add(new Rectangle(center+g+odd,center-before,l,t));if(!data.tStyle)result.Add(new Rectangle(center-before,center-g-l,t,l));result.Add(new Rectangle(center-before,center+g+odd,t,l));}
            return result;
        }
        private static double Linear(int value){double x=value/255.0;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}
        private static int Srgb(double value){return (int)Math.Round(Math.Max(0,Math.Min(1,value<=.0031308?value*12.92:1.055*Math.Pow(value,1/2.4)-.055))*255);}
        internal static Bitmap Pixels(Cs2CrosshairData data,int size)
        {
            var rectangles=Rectangles(data,size/2);var image=new Bitmap(size,size);int leading=data.outlineMode>0?1:0,trailing=data.outlineMode==1?1:0;
            double[] fillColor={Linear(data.r),Linear(data.g),Linear(data.b)},outlineColor={Linear(data.outlineR),Linear(data.outlineG),Linear(data.outlineB)},ground={Linear(32),Linear(41),Linear(54)};
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                double fill=rectangles.Any(r=>r.Contains(x,y))?data.a/255.0:0;
                double outline=data.outlineMode>0&&rectangles.Any(r=>x>=r.Left-leading&&x<r.Right+trailing&&y>=r.Top-leading&&y<r.Bottom+trailing)?data.outlineA/255.0:0;
                double behind=outline*(1-fill)*(1-fill),alpha=fill+behind;int[] rgb=new int[3];for(int c=0;c<3;c++)rgb[c]=Srgb(fillColor[c]*fill+outlineColor[c]*behind+ground[c]*(1-alpha));image.SetPixel(x,y,Color.FromArgb(rgb[0],rgb[1],rgb[2]));
            }
            return image;
        }
    }
    internal sealed class NativeCrosshairCanvas:Control
    {
        internal CrosshairPreset Preset;
        internal int GameWidth=1440,GameHeight=1080,OutputWidth=1920,OutputHeight=1080,Zoom=2;
        internal bool Stretched=true;
        internal NativeCrosshairCanvas(){Dock=DockStyle.Fill;DoubleBuffered=true;ResizeRedraw=true;BackColor=Color.FromArgb(32,41,54);AccessibleName="按当前分享码绘制的静态准心预览";}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);if(Preset==null)return;
            try
            {
                Cs2CrosshairData data;
                if(!Preset.Valorant)data=NativeCrosshairRenderer.Rescale(Cs2ShareCode.Decode(Preset.Code),GameHeight);
                else data=new Cs2CrosshairData {screenHeight=1080,style=Preset.Dot?6:4,r=Preset.Color.R,g=Preset.Color.G,b=Preset.Color.B,a=255,outlineA=255,thickness=Preset.Thickness,length=Preset.Length,gap=Preset.Gap,centerDot=Preset.Dot,tStyle=Preset.TShape};
                int crop=Math.Max(32,(data.length+Math.Max(0,data.gap)+data.thickness+3)*2);
                using(var pixels=NativeCrosshairRenderer.Pixels(data,crop))
                {
                    float sx=(Stretched?OutputWidth/(float)GameWidth:1)*Zoom,sy=(Stretched?OutputHeight/(float)GameHeight:1)*Zoom;float w=crop*sx,h=crop*sy;
                    e.Graphics.InterpolationMode=sx==(int)sx&&sy==(int)sy?InterpolationMode.NearestNeighbor:InterpolationMode.Bilinear;e.Graphics.PixelOffsetMode=PixelOffsetMode.Half;
                    e.Graphics.DrawImage(pixels,new RectangleF((Width-w)/2,(Height-h)/2,w,h));
                }
            }
            catch(Exception ex){TextRenderer.DrawText(e.Graphics,ex.Message,Font,ClientRectangle,Color.White,TextFormatFlags.WordBreak|TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
        }
    }
    internal sealed class NativeCrosshairLibrary:UserControl
    {
        internal NativeCrosshairLibrary(bool valorant)
        {
            Dock=DockStyle.Fill;Padding=new Padding(20,12,20,16);BackColor=ModernTheme.Background;
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));Controls.Add(root);
            var list=new ListBox {Dock=DockStyle.Fill,Font=ModernTheme.Font(11F,FontStyle.Regular),IntegralHeight=false,ItemHeight=30};root.Controls.Add(list,0,0);
            var inspector=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Padding=new Padding(18,0,0,0),Margin=new Padding(0)};
            foreach(int height in new[]{34,76,0,64,42,45,50})inspector.RowStyles.Add(new RowStyle(height==0?SizeType.Percent:SizeType.Absolute,height==0?100:height));root.Controls.Add(inspector,1,0);
            var title=ModernTheme.Label(valorant?"VALORANT 准心":"CS2 准星",12F,FontStyle.Bold,ModernTheme.Text);inspector.Controls.Add(title,0,0);
            ComboBox resolution=ToolUi.Choice("1440 × 1080","1280 × 960","1280 × 1024","1920 × 1080"),zoom=ToolUi.Choice("1× 像素观察","2× 像素观察","4× 像素观察");zoom.SelectedIndex=1;
            var output=ToolUi.Choice("1920 × 1080","2560 × 1440","3840 × 2160","2560 × 1080","3440 × 1440");var stretched=ToolUi.Check("全屏拉伸预览（关闭为原始像素）",true);
            resolution.AccessibleName="游戏分辨率";output.AccessibleName="显示器输出分辨率";zoom.AccessibleName="预览观察倍率";
            try{var original=SavedState.Load();var current=DisplayModeService.GetCurrent();string initial=(original==null?current.Width:original.Width)+" × "+(original==null?current.Height:original.Height);if(!output.Items.Contains(initial))output.Items.Add(initial);output.SelectedItem=initial;}catch{}
            var controls=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=3,Margin=new Padding(0)};controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,24));controls.RowStyles.Add(new RowStyle(SizeType.Absolute,18));controls.RowStyles.Add(new RowStyle(SizeType.Absolute,30));controls.RowStyles.Add(new RowStyle(SizeType.Percent,100));controls.Controls.Add(ToolUi.Text("游戏分辨率",9F),0,0);controls.Controls.Add(ToolUi.Text("显示器输出",9F),1,0);controls.Controls.Add(ToolUi.Text("观察倍率",9F),2,0);resolution.Dock=output.Dock=zoom.Dock=DockStyle.Top;controls.Controls.Add(resolution,0,1);controls.Controls.Add(output,1,1);controls.Controls.Add(zoom,2,1);controls.Controls.Add(stretched,0,2);controls.SetColumnSpan(stretched,3);inspector.Controls.Add(controls,0,1);
            var canvas=new NativeCrosshairCanvas();inspector.Controls.Add(canvas,0,2);var code=ToolUi.Output();code.WordWrap=true;code.Font=new Font("Consolas",9F);inspector.Controls.Add(code,0,3);
            var copy=ToolUi.Button("复制分享码",delegate{ToolUi.Copy(code.Text);});inspector.Controls.Add(copy,0,4);
            var more=new LinkLabel {Text=valorant?"查看更多 VALORANT 准心":"查看更多 CS2 准心",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,LinkColor=ModernTheme.Accent};more.LinkClicked+=delegate{if(!Program.UiPreview)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(valorant?"https://www.valorantcrosshairdb.com/crosshairs/professional/":"https://crosshair.club/builder") {UseShellExecute=true});};inspector.Controls.Add(more,0,5);
            var hint=ToolUi.Text(valorant?"仅静态尺寸示意，不模拟移动或开火误差；以靶场实际效果为准。":"按分享码解析并绘制静态像素；动态扩散与后坐力需要游戏内核对。",9F);hint.ForeColor=ModernTheme.Muted;inspector.Controls.Add(hint,0,6);
            var presets=valorant?CrosshairLibraryPage.ValorantPresets():CrosshairLibraryPage.Cs2Presets();foreach(var preset in presets)list.Items.Add(preset.Name);
            Action refresh=delegate{if(list.SelectedIndex<0)return;var preset=presets[list.SelectedIndex];canvas.Preset=preset;string[] size=Convert.ToString(resolution.SelectedItem).Split('×'),screen=Convert.ToString(output.SelectedItem).Split('×');canvas.GameWidth=int.Parse(size[0]);canvas.GameHeight=int.Parse(size[1]);canvas.OutputWidth=int.Parse(screen[0]);canvas.OutputHeight=int.Parse(screen[1]);canvas.Stretched=stretched.Checked;canvas.Zoom=zoom.SelectedIndex==0?1:zoom.SelectedIndex==1?2:4;canvas.Invalidate();title.Text=preset.Name;code.Text=preset.Code;};
            list.SelectedIndexChanged+=delegate{refresh();};resolution.SelectedIndexChanged+=delegate{refresh();};output.SelectedIndexChanged+=delegate{refresh();};stretched.CheckedChanged+=delegate{refresh();};zoom.SelectedIndexChanged+=delegate{refresh();};if(list.Items.Count>0)list.SelectedIndex=0;copy.Enabled=!Program.UiPreview;
        }
    }
}
