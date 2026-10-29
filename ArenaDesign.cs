using System;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NexaArena
{
    internal sealed class ArenaInput : TextBox
    {
        [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wParam,string lParam);
        public string Hint;
        protected override void OnHandleCreated(EventArgs e) {base.OnHandleCreated(e);SendMessage(Handle,0x1501,IntPtr.Zero,Hint??"");}
    }

    internal static class ArenaDrawing
    {
        public static GraphicsPath Round(RectangleF box, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(box.Width, box.Height));
            GraphicsPath path = new GraphicsPath();
            if (d <= 0) { path.AddRectangle(box); return path; }
            path.AddArc(box.Left, box.Top, d, d, 180, 90);
            path.AddArc(box.Right - d, box.Top, d, d, 270, 90);
            path.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90);
            path.AddArc(box.Left, box.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }

        public static void Icon(Graphics g, string name, Rectangle box, Color color)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(box.X, box.Y); g.ScaleTransform(box.Width / 24F, box.Height / 24F);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(color, 1.6F) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (name == "home") { g.DrawRectangle(p, 3, 3, 7, 7); g.DrawRectangle(p, 14, 3, 7, 7); g.DrawRectangle(p, 3, 14, 7, 7); g.DrawRectangle(p, 14, 14, 7, 7); }
                else if (name == "audio") { g.DrawArc(p, 4, 3, 16, 18, 180, 180); g.DrawRectangle(p, 3, 12, 4, 8); g.DrawRectangle(p, 17, 12, 4, 8); }
                else if (name == "chart") { g.DrawLines(p, new[] {new Point(3,4),new Point(3,21),new Point(22,21)}); g.DrawLines(p, new[] {new Point(6,15),new Point(10,10),new Point(14,13),new Point(21,5)}); }
                else if (name == "bolt") { g.DrawPolygon(p, new[] {new Point(14,2),new Point(5,14),new Point(11,14),new Point(10,22),new Point(20,9),new Point(13,9)}); }
                else if (name == "settings") { for (int i=0;i<3;i++) { int y=5+i*7, x=i==1?8:16; g.DrawLine(p,3,y,21,y); using(var b=new SolidBrush(ModernTheme.Navy))g.FillEllipse(b,x-3,y-3,6,6); g.DrawEllipse(p,x-3,y-3,6,6); } }
                else if (name == "guide") { g.DrawRectangle(p,4,3,16,18); g.DrawLine(p,8,3,8,21); g.DrawLine(p,12,8,17,8); g.DrawLine(p,12,12,17,12); }
                else if (name == "monitor") { g.DrawRectangle(p,2,3,20,14); g.DrawLine(p,12,17,12,22); g.DrawLine(p,7,22,17,22); }
                else if (name == "arrow") { g.DrawLine(p,4,12,20,12); g.DrawLines(p,new[]{new Point(14,6),new Point(20,12),new Point(14,18)}); }
                else { g.DrawEllipse(p,5,5,14,14); g.DrawLine(p,12,1,12,8); g.DrawLine(p,12,16,12,23); g.DrawLine(p,1,12,8,12); g.DrawLine(p,16,12,23,12); }
            }
            g.Restore(state);
        }
    }

    internal class ArenaButton : Button
    {
        private bool hovered, pressed;
        public ArenaButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false;
        }
        protected override void OnMouseEnter(EventArgs e) { hovered=true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered=false; pressed=false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed=true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed=false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? ModernTheme.Background : Parent.BackColor);
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            Color fill = !Enabled ? Color.FromArgb(228,232,237) : pressed ? ControlPaint.Dark(BackColor,0.08F) : hovered ? (BackColor==Color.White?ModernTheme.AccentSoft:ControlPaint.Light(BackColor,0.08F)) : BackColor;
            using(var path=ArenaDrawing.Round(new RectangleF(0.5F,0.5F,Width-1,Height-1),8*DeviceScale))
            {
                using(var brush=new SolidBrush(fill))e.Graphics.FillPath(brush,path);
                Color border=FlatAppearance.BorderSize>0?FlatAppearance.BorderColor:fill;
                using(var pen=new Pen(border,Math.Max(1,FlatAppearance.BorderSize)))e.Graphics.DrawPath(pen,path);
            }
            Rectangle text=Rectangle.Inflate(ClientRectangle,-12,-4);
            TextFormatFlags flags=TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix;
            flags|=TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter;
            TextRenderer.DrawText(e.Graphics,Text,Font,text,Enabled?ForeColor:ModernTheme.Muted,flags);
            if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5),ForeColor,fill);
        }
        protected float DeviceScale { get { return DeviceDpi / 96F; } }
    }

    internal sealed class ArenaPresetButton : ArenaButton
    {
        public string Caption;
        public int ModeWidth,ModeHeight;
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null?Color.White:Parent.BackColor);
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=ArenaDrawing.Round(new RectangleF(1,1,Width-3,Height-3),9*DeviceScale))
            {
                using(var b=new SolidBrush(BackColor))e.Graphics.FillPath(b,path);
                using(var p=new Pen(FlatAppearance.BorderColor,FlatAppearance.BorderSize))e.Graphics.DrawPath(p,path);
            }
            int x=(int)(18*DeviceScale), icon=(int)(30*DeviceScale);
            ArenaDrawing.Icon(e.Graphics,"monitor",new Rectangle(x,(Height-icon)/2,icon,icon),ForeColor);
            using(var numberFont=new Font("Segoe UI",13F,FontStyle.Bold))
            using(var captionFont=ModernTheme.Font(8F,FontStyle.Regular))
            {
                int tx=x+icon+(int)(16*DeviceScale);
                TextRenderer.DrawText(e.Graphics,ModeWidth+" × "+ModeHeight,numberFont,new Rectangle(tx,Height/2-(int)(24*DeviceScale),Width-tx-12,(int)(28*DeviceScale)),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(e.Graphics,Caption,captionFont,new Rectangle(tx,Height/2+(int)(4*DeviceScale),Width-tx-12,(int)(22*DeviceScale)),ModernTheme.Muted,TextFormatFlags.Left|TextFormatFlags.EndEllipsis);
            }
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5));
        }
    }

    internal sealed class ArenaBrand : Control
    {
        public ArenaBrand() { Dock=DockStyle.Fill; DoubleBuffered=true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); var g=e.Graphics; float s=DeviceDpi/96F;
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=ArenaDrawing.Round(new RectangleF(22*s,25*s,36*s,36*s),10*s))
            using(var b=new SolidBrush(Color.FromArgb(248,183,157)))g.FillPath(b,path);
            using(var f=new Font("Segoe UI",17F,FontStyle.Bold))TextRenderer.DrawText(g,"N",f,new Rectangle((int)(25*s),(int)(25*s),(int)(32*s),(int)(36*s)),ModernTheme.Navy,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            using(var f=new Font("Segoe UI",16F,FontStyle.Bold))TextRenderer.DrawText(g,"NEXA",f,new Point((int)(69*s),(int)(22*s)),Color.White);
            using(var f=new Font("Segoe UI",7.5F,FontStyle.Regular))TextRenderer.DrawText(g,"A R E N A  /  TOOLKIT",f,new Point((int)(71*s),(int)(49*s)),Color.FromArgb(174,187,199));
        }
    }
    internal sealed class WorkbenchBrand:Control
    {
        internal WorkbenchBrand(){DoubleBuffered=true;Dock=DockStyle.Fill;Margin=new Padding(0);}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);float s=DeviceDpi/96F;var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            g.TranslateTransform(22*s,(Height-26*s)/2);g.ScaleTransform(s,s);
            using(var ink=new SolidBrush(ModernTheme.Text))using(var blue=new SolidBrush(ModernTheme.Accent))
            {g.FillPolygon(ink,new[]{new Point(0,26),new Point(5,0),new Point(12,0),new Point(7,26)});g.FillPolygon(blue,new[]{new Point(5,0),new Point(12,0),new Point(26,26),new Point(19,26)});g.FillPolygon(ink,new[]{new Point(19,26),new Point(24,0),new Point(31,0),new Point(26,26)});}
            g.ResetTransform();using(var font=ModernTheme.Font(14F,FontStyle.Bold))TextRenderer.DrawText(g,"Nexa Arena",font,new Rectangle((int)(64*s),0,Width-(int)(64*s),Height),ModernTheme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
    }
    internal sealed class WorkbenchGameScope:TableLayoutPanel
    {
        internal WorkbenchGameScope(ComboBox model)
        {
            Dock=DockStyle.Fill;ColumnCount=2;RowCount=2;Margin=new Padding(0);Padding=new Padding(0);RowStyles.Add(new RowStyle(SizeType.Absolute,36));RowStyles.Add(new RowStyle(SizeType.Absolute,0));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            var first=ModernTheme.Button("VALORANT",Color.White,ModernTheme.Muted);var second=ModernTheme.Button("CS2",Color.White,ModernTheme.Muted);
            first.Dock=second.Dock=DockStyle.Fill;first.Margin=second.Margin=new Padding(0);first.Font=second.Font=ModernTheme.Font(10F,FontStyle.Regular);
            Controls.Add(first,0,0);Controls.Add(second,1,0);model.Visible=false;model.TabStop=false;Controls.Add(model,0,1);SetColumnSpan(model,2);
            Action refresh=delegate{first.BackColor=Convert.ToString(model.SelectedItem)=="VALORANT"?ModernTheme.AccentSoft:Color.White;second.BackColor=Convert.ToString(model.SelectedItem)=="CS2"?ModernTheme.AccentSoft:Color.White;first.ForeColor=Convert.ToString(model.SelectedItem)=="VALORANT"?ModernTheme.Accent:ModernTheme.Muted;second.ForeColor=Convert.ToString(model.SelectedItem)=="CS2"?ModernTheme.Accent:ModernTheme.Muted;first.Enabled=second.Enabled=model.Enabled;};
            first.Click+=delegate{model.SelectedItem="VALORANT";};second.Click+=delegate{model.SelectedItem="CS2";};model.SelectedIndexChanged+=delegate{refresh();};model.EnabledChanged+=delegate{refresh();};refresh();
        }
    }
    internal sealed class WorkbenchSteps:Control
    {
        private readonly string[] titles={"核对配置","应用拉伸","恢复桌面"};
        private string[] subtitles={"检查账号与配置文件","等待修复完成","尚无显示会话"};
        private int active;
        internal WorkbenchSteps(){Dock=DockStyle.Fill;DoubleBuffered=true;ResizeRedraw=true;AccessibleName="显示操作进度";}
        internal void Update(bool repaired,bool applied,bool session,bool restored)
        {active=restored?2:applied?2:repaired?1:0;subtitles=new[]{repaired?"配置已修复，原值已保存":"检查账号与配置文件",applied?"目标模式已应用":repaired?"等待进入游戏场景":"等待修复完成",restored?"已恢复":session?"保留桌面恢复入口":"尚无显示会话"};AccessibleDescription=string.Join("；",titles.Zip(subtitles,(a,b)=>a+"："+b).ToArray());Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.Clear(ModernTheme.Background);g.SmoothingMode=SmoothingMode.AntiAlias;float s=DeviceDpi/96F;float cell=Width/3F;
            for(int i=0;i<3;i++){int x=(int)(i*cell+18*s),diam=(int)(32*s),y=(Height-diam)/2;using(var brush=new SolidBrush(i==active?ModernTheme.Accent:Color.FromArgb(208,213,219)))g.FillEllipse(brush,x,y,diam,diam);
                TextRenderer.DrawText(g,(i+1).ToString(),Font,new Rectangle(x,y,diam,diam),Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
                int tx=x+diam+(int)(12*s),tw=(int)cell-(int)(80*s);using(var bold=ModernTheme.Font(10F,FontStyle.Bold))TextRenderer.DrawText(g,titles[i],bold,new Rectangle(tx,y-2,tw,(int)(24*s)),i==active?ModernTheme.Accent:ModernTheme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                using(var small=ModernTheme.Font(8.5F,FontStyle.Regular))TextRenderer.DrawText(g,subtitles[i],small,new Rectangle(tx,y+(int)(23*s),tw,(int)(20*s)),ModernTheme.Muted,TextFormatFlags.EndEllipsis);
            }
        }
    }
    internal sealed class WorkbenchPresetChoice:RadioButton
    {
        internal WorkbenchPresetChoice(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Dock=DockStyle.Fill;Margin=new Padding(0,0,10,0);Height=72;Cursor=Cursors.Hand;}
        protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.Clear(Parent==null?Color.White:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float s=DeviceDpi/96F;
            using(var path=ArenaDrawing.Round(new RectangleF(1,1,Width-3,Height-3),6*s))using(var bg=new SolidBrush(Checked?ModernTheme.AccentSoft:Color.White))using(var pen=new Pen(Checked?ModernTheme.Accent:ModernTheme.Border,1)){g.FillPath(bg,path);g.DrawPath(pen,path);}
            var mode=(Size)Tag;int x=(int)(14*s),y=(int)(19*s),d=(int)(17*s);using(var pen=new Pen(Checked?ModernTheme.Accent:ModernTheme.Muted,2))g.DrawEllipse(pen,x,y,d,d);if(Checked)using(var b=new SolidBrush(ModernTheme.Accent))g.FillEllipse(b,x+4*s,y+4*s,d-8*s,d-8*s);
            using(var font=ModernTheme.Font(11F,FontStyle.Bold))TextRenderer.DrawText(g,mode.Width+" × "+mode.Height,font,new Rectangle(x+d+(int)(10*s),y-4,Width-x-d-(int)(20*s),(int)(28*s)),ModernTheme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);
            using(var font=ModernTheme.Font(9F,FontStyle.Regular))TextRenderer.DrawText(g,mode.Width*4==mode.Height*5?"5:4":"4:3",font,new Rectangle(x+d+(int)(10*s),y+(int)(25*s),Width-x-d-10,(int)(22*s)),ModernTheme.Muted,TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-5,-5));
        }
    }
    internal sealed class WorkbenchSectionTabs:TabControl
    {
        internal WorkbenchSectionTabs(){DrawMode=TabDrawMode.OwnerDrawFixed;SizeMode=TabSizeMode.Fixed;ItemSize=new Size(118,42);Font=ModernTheme.Font(10F,FontStyle.Regular);Padding=new Point(14,8);}
        protected override void OnDrawItem(DrawItemEventArgs e)
        {var box=GetTabRect(e.Index);using(var bg=new SolidBrush(Color.White))e.Graphics.FillRectangle(bg,box);bool chosen=e.Index==SelectedIndex;TextRenderer.DrawText(e.Graphics,TabPages[e.Index].Text,Font,box,chosen?ModernTheme.Accent:ModernTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if(chosen)using(var pen=new Pen(ModernTheme.Accent,2))e.Graphics.DrawLine(pen,box.Left+8,box.Bottom-2,box.Right-8,box.Bottom-2);if(Focused&&chosen&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(box,-4,-4));}
    }

    internal sealed class WorkbenchTabButton:ArenaButton
    {
        private bool selected;
        internal bool Selected {get{return selected;}set{selected=value;Invalidate();}}
        internal WorkbenchTabButton(){Font=ModernTheme.Font(10F,FontStyle.Regular);Cursor=Cursors.Hand;}
        protected override void OnPaint(PaintEventArgs e)
        {e.Graphics.Clear(Color.White);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,selected?ModernTheme.Accent:ModernTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if(selected)using(var pen=new Pen(ModernTheme.Accent,2))e.Graphics.DrawLine(pen,8,Height-2,Width-8,Height-2);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4));}
    }

}
