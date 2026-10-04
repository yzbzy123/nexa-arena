using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace NexaArena
{
    public sealed class DesktopIconPosition
    {
        public string Identity;
        public int X,Y;
    }

    public sealed class DesktopIconSnapshot
    {
        public int Version=1;
        public long SessionTicks;
        public string Topology;
        public bool AutoArrange;
        public int SpacingX,SpacingY;
        public List<DesktopIconPosition> Icons=new List<DesktopIconPosition>();
    }

    internal interface IDesktopIconView : IDisposable
    {
        bool AutoArrange {get;}
        Point Spacing {get;}
        List<DesktopIconPosition> Read();
        void Position(IList<DesktopIconPosition> icons);
    }

    internal static class DesktopIconPolicy
    {
        public static List<DesktopIconPosition> Match(DesktopIconSnapshot saved,List<DesktopIconPosition> current,string topology)
        {
            if(saved==null||saved.Version!=1||saved.Icons==null)throw new InvalidOperationException("桌面图标备份无效。");
            if(saved.Topology!=topology)throw new InvalidOperationException("显示器布局或工作区尚未回到备份状态，已保留图标备份。");
            if(saved.AutoArrange)throw new InvalidOperationException("备份时桌面启用了自动排列，Windows 会自行管理图标位置。");
            var oldUnique=saved.Icons.Where(p=>p!=null&&!string.IsNullOrEmpty(p.Identity)).GroupBy(p=>p.Identity,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
            var result=new List<DesktopIconPosition>();
            foreach(var group in current.GroupBy(p=>p.Identity,StringComparer.OrdinalIgnoreCase))
            {
                DesktopIconPosition old;
                if(group.Count()!=1||!oldUnique.TryGetValue(group.Key,out old))continue;
                if(Math.Abs((long)old.X)>100000||Math.Abs((long)old.Y)>100000)throw new InvalidOperationException("图标备份坐标无效。");
                result.Add(old);
            }
            return result;
        }
    }

    internal static class DesktopIconLayout
    {
        internal static string FilePath {get{return Path.Combine(SavedState.DirectoryPath,"desktop-icons.xml");}}
        internal static string Topology()
        {
            return string.Join("|",Screen.AllScreens.OrderBy(s=>s.DeviceName,StringComparer.Ordinal).Select(s=>s.DeviceName+":"+s.Bounds.X+","+s.Bounds.Y+","+s.Bounds.Width+","+s.Bounds.Height+":"+s.WorkingArea.X+","+s.WorkingArea.Y+","+s.WorkingArea.Width+","+s.WorkingArea.Height+":"+s.Primary));
        }
        internal static DesktopIconSnapshot ReadSnapshot()
        {
            var settings=new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4000000};
            using(var reader=XmlReader.Create(FilePath,settings))return (DesktopIconSnapshot)new XmlSerializer(typeof(DesktopIconSnapshot)).Deserialize(reader);
        }
        internal static void Capture(SavedState state)
        {
            if(Program.UiPreview)return;
            using(var view=new ShellDesktopIconView())
            {
                Point spacing=view.Spacing;
                var snapshot=new DesktopIconSnapshot {SessionTicks=state.SavedAt.Ticks,Topology=Topology(),AutoArrange=view.AutoArrange,SpacingX=spacing.X,SpacingY=spacing.Y,Icons=view.Read()};
                Directory.CreateDirectory(SavedState.DirectoryPath);
                string temporary=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";
                try
                {
                    using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    {new XmlSerializer(typeof(DesktopIconSnapshot)).Serialize(stream,snapshot);stream.Flush(true);}
                    if(File.Exists(FilePath))File.Replace(temporary,FilePath,FilePath+".bak");else File.Move(temporary,FilePath);
                    if(ReadSnapshot().SessionTicks!=state.SavedAt.Ticks)throw new IOException("图标备份写入后校验失败。");
                }
                finally{if(File.Exists(temporary))File.Delete(temporary);}
                RestoreTrace.Write("Desktop icons captured: count="+snapshot.Icons.Count+", autoArrange="+snapshot.AutoArrange);
            }
        }

        internal static void Restore(SavedState state)
        {
            if(Program.UiPreview||state==null||!File.Exists(FilePath))return;
            DesktopIconSnapshot snapshot=ReadSnapshot();
            if(snapshot.SessionTicks!=state.SavedAt.Ticks)
                throw new InvalidOperationException("图标备份不属于本次显示会话，未套用旧排列。");
            if(snapshot.AutoArrange)
            {RestoreTrace.Write("Desktop auto-arrange was enabled; no manual positions applied");return;}
            Exception failure=null;
            // Explorer may relayout after the display mode has settled. Require two stable checks.
            int stable=0;
            for(int attempt=0;attempt<6;attempt++)
            {
                Thread.Sleep(attempt==0?1000:500);
                try
                {
                    using(var view=new ShellDesktopIconView())
                    {
                        if(view.AutoArrange)throw new InvalidOperationException("桌面当前启用了自动排列，无法还原手动位置；备份仍保留。");
                        Point spacing=view.Spacing;
                        if(spacing.X!=snapshot.SpacingX||spacing.Y!=snapshot.SpacingY)throw new InvalidOperationException("图标尺寸、网格间距或缩放与备份不一致，已保留备份。");
                        var current=view.Read();
                        var targets=DesktopIconPolicy.Match(snapshot,current,Topology());
                        if(snapshot.Icons.Count>0&&targets.Count==0)throw new InvalidOperationException("未找到与备份匹配的桌面图标，备份仍保留。");
                        var byIdentity=current.ToDictionary(p=>p.Identity,StringComparer.OrdinalIgnoreCase);
                        bool matches=targets.All(p=>byIdentity[p.Identity].X==p.X&&byIdentity[p.Identity].Y==p.Y);
                        if(matches){if(++stable>=2){RestoreTrace.Write("Desktop icons restored and verified: "+targets.Count);return;}}
                        else {stable=0;view.Position(targets);}
                    }
                    failure=null;
                }
                catch(Exception ex){failure=ex;stable=0;}
            }
            throw new InvalidOperationException("显示已恢复，但桌面图标位置尚未验证通过；图标备份已保留。"+(failure==null?"":failure.Message));
        }
    }

    // Supported Shell COM API; no Explorer process memory or private ListView messages.
    internal sealed class ShellDesktopIconView : IDesktopIconView
    {
        private object windows,desktop,browserObject,viewObject;
        private IFolderView view;
        [StructLayout(LayoutKind.Sequential)]private struct NativePoint {public int X,Y;}
        [DllImport("shell32.dll")]private static extern int SHGetNameFromIDList(IntPtr pidl,uint name,out IntPtr text);
        [ComImport,Guid("6D5140C1-7436-11CE-8034-00AA006009FA"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IServiceProvider
        {[PreserveSig]int QueryService(ref Guid service,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object result);}
        [ComImport,Guid("000214E2-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellBrowser
        {
            void GetWindow();void ContextSensitiveHelp();void InsertMenus();void SetMenu();void RemoveMenus();void SetStatusText();void EnableModeless();void TranslateAccelerator();void BrowseObject();void GetViewStateStream();void GetControlWindow();void SendControlMsg();
            [PreserveSig]int QueryActiveShellView([MarshalAs(UnmanagedType.Interface)]out object shellView);
        }
        [ComImport,Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFolderView
        {
            void GetCurrentViewMode();void SetCurrentViewMode();void GetFolder();
            [PreserveSig]int Item(int index,out IntPtr pidl);
            [PreserveSig]int ItemCount(uint flags,out int count);
            void Items();void GetSelectionMarkedItem();void GetFocusedItem();
            [PreserveSig]int GetItemPosition(IntPtr pidl,out NativePoint point);
            [PreserveSig]int GetSpacing(out NativePoint point);
            void GetDefaultSpacing();
            [PreserveSig]int GetAutoArrange();
            void SelectItem();
            [PreserveSig]int SelectAndPositionItems(uint count,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)]IntPtr[] pidls,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)]NativePoint[] points,uint flags);
        }
        public ShellDesktopIconView()
        {
            try
            {
                windows=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"),true));
                object[] arguments={0,null,8,0,1};
                desktop=windows.GetType().InvokeMember("FindWindowSW",BindingFlags.InvokeMethod,null,windows,arguments);
                if(desktop==null)throw new InvalidOperationException("Windows 桌面尚未就绪。");
                Guid service=new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837"),iid=typeof(IShellBrowser).GUID;
                Marshal.ThrowExceptionForHR(((IServiceProvider)desktop).QueryService(ref service,ref iid,out browserObject));
                Marshal.ThrowExceptionForHR(((IShellBrowser)browserObject).QueryActiveShellView(out viewObject));
                view=(IFolderView)viewObject;
            }
            catch{Dispose();throw;}
        }
        public bool AutoArrange
        {
            get{int hr=view.GetAutoArrange();Marshal.ThrowExceptionForHR(hr);return hr==0;}
        }
        public Point Spacing {get{NativePoint point;Marshal.ThrowExceptionForHR(view.GetSpacing(out point));return new Point(point.X,point.Y);}}
        private string Identity(IntPtr pidl)
        {
            IntPtr name=IntPtr.Zero;
            try{Marshal.ThrowExceptionForHR(SHGetNameFromIDList(pidl,0x80028000,out name));return Marshal.PtrToStringUni(name);}
            finally{if(name!=IntPtr.Zero)Marshal.FreeCoTaskMem(name);}
        }
        public List<DesktopIconPosition> Read()
        {
            int count;Marshal.ThrowExceptionForHR(view.ItemCount(2,out count));
            if(count<0||count>10000)throw new InvalidOperationException("桌面图标数量异常。");
            var result=new List<DesktopIconPosition>();
            for(int i=0;i<count;i++)
            {
                IntPtr pidl=IntPtr.Zero;
                try
                {
                    Marshal.ThrowExceptionForHR(view.Item(i,out pidl));NativePoint point;
                    Marshal.ThrowExceptionForHR(view.GetItemPosition(pidl,out point));
                    result.Add(new DesktopIconPosition {Identity=Identity(pidl),X=point.X,Y=point.Y});
                }
                finally{if(pidl!=IntPtr.Zero)Marshal.FreeCoTaskMem(pidl);}
            }
            if(result.Any(p=>string.IsNullOrEmpty(p.Identity))||result.Select(p=>p.Identity).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=result.Count)
                throw new InvalidOperationException("无法唯一识别桌面图标，已停止操作。");
            return result;
        }
        public void Position(IList<DesktopIconPosition> positions)
        {
            var target=positions.ToDictionary(p=>p.Identity,StringComparer.OrdinalIgnoreCase);
            var pidls=new List<IntPtr>();var points=new List<NativePoint>();
            try
            {
                int count;Marshal.ThrowExceptionForHR(view.ItemCount(2,out count));
                for(int i=0;i<count;i++)
                {
                    IntPtr pidl=IntPtr.Zero;
                    try
                    {
                        Marshal.ThrowExceptionForHR(view.Item(i,out pidl));DesktopIconPosition position;
                        if(target.TryGetValue(Identity(pidl),out position))
                        {pidls.Add(pidl);pidl=IntPtr.Zero;points.Add(new NativePoint {X=position.X,Y=position.Y});}
                    }
                    finally{if(pidl!=IntPtr.Zero)Marshal.FreeCoTaskMem(pidl);}
                }
                if(pidls.Count>0)Marshal.ThrowExceptionForHR(view.SelectAndPositionItems((uint)pidls.Count,pidls.ToArray(),points.ToArray(),0x80));
            }
            finally{foreach(IntPtr pidl in pidls)Marshal.FreeCoTaskMem(pidl);}
        }
        public void Dispose()
        {
            view=null;
            Release(ref viewObject);Release(ref browserObject);Release(ref desktop);Release(ref windows);
        }
        private static void Release(ref object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);value=null;}
    }
}
