using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text;
using Microsoft.Win32;

namespace NexaArena
{
    internal static class DeviceTuning
    {
        private static string Encode(string value){return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));}
        private static string Decode(string value){string text=Encoding.UTF8.GetString(Convert.FromBase64String(value));if(text.Length>2048||text.Contains("\0")||!(text.StartsWith("PCI\\",StringComparison.OrdinalIgnoreCase)||text.StartsWith("HDAUDIO\\",StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("只处理枚举得到的 PCI / HDAUDIO 设备。");return text;}
        private static string Key(string instance){return @"SYSTEM\CurrentControlSet\Enum\"+instance+@"\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties";}
        private static string AffinityKey(string instance){return @"SYSTEM\CurrentControlSet\Enum\"+instance+@"\Device Parameters\Interrupt Management\Affinity Policy";}
        private static string RegistryValue(RegistryKey key,string name)
        {
            object value=key==null?null:key.GetValue(name);if(value==null)return "missing";
            var kind=key.GetValueKind(name);
            if(kind==RegistryValueKind.DWord)return "d:"+Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture);
            if(kind==RegistryValueKind.QWord)return "q:"+Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture);
            if(kind==RegistryValueKind.Binary&&((byte[])value).Length<=8)return "b:"+Convert.ToBase64String((byte[])value);
            return "unsupported";
        }
        private static void RegistryWrite(RegistryKey key,string name,string value)
        {
            if(value=="missing")key.DeleteValue(name,false);
            else if(value.StartsWith("d:"))key.SetValue(name,int.Parse(value.Substring(2),CultureInfo.InvariantCulture),RegistryValueKind.DWord);
            else if(value.StartsWith("q:"))key.SetValue(name,long.Parse(value.Substring(2),CultureInfo.InvariantCulture),RegistryValueKind.QWord);
            else if(value.StartsWith("b:")){byte[] bytes=Convert.FromBase64String(value.Substring(2));if(bytes.Length>8)throw new ArgumentException("中断掩码备份长度无效。");key.SetValue(name,bytes,RegistryValueKind.Binary);}
            else throw new ArgumentException("中断参数备份类型无效。");
        }
        private static ManagerChoice Choice(string value,string label){return new ManagerChoice {value=value,label=label};}
        internal static List<ManagerItem> Scan(string module)
        {
            var items=new List<ManagerItem>();
            using(var searcher=new ManagementObjectSearcher(module=="devices"?"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE 'PCI%' OR DeviceID LIKE 'HDAUDIO%'":"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE 'PCI%'"))using(var rows=searcher.Get())
                foreach(ManagementObject row in rows)using(row)
                {
                    string instance=Convert.ToString(row["PNPDeviceID"]),category=Convert.ToString(row["PNPClass"]),title=Convert.ToString(row["Name"]);
                    if(string.IsNullOrEmpty(instance))continue;
                    string id=Encode(instance),identity=WindowsManagerBackend.Hash(instance+"|"+Convert.ToString(row["ClassGuid"]));
                    if(module=="device-msi")
                    {
                        bool eligible=category=="Net"||category=="Display";object flag=null;
                        using(var key=Registry.LocalMachine.OpenSubKey(Key(instance)))if(key!=null&&key.GetValue("MSISupported")!=null&&key.GetValueKind("MSISupported")==RegistryValueKind.DWord)flag=key.GetValue("MSISupported");
                        string raw=flag==null?"missing":Convert.ToInt32(flag).ToString(CultureInfo.InvariantCulture);
                        bool supported=eligible&&(raw=="0"||raw=="1");
                        items.Add(new ManagerItem {id=id,title=title,detail=category+" · "+instance,current=raw=="missing"?"驱动未声明此参数":raw=="1"?"MSI 参数已配置为启用（不是运行时验证）":"MSI 参数已配置为关闭",Raw=raw,Identity=identity,supported=supported,blocked=supported?null:"仅修改已有 DWORD 参数的 PCI 网卡/显卡；不创建未知中断键，不更改存储或输入设备。",effect="实验性驱动参数，重启后由驱动决定；可能导致设备异常或显示问题。须具备安全模式/备份恢复能力。",choices=supported?new List<ManagerChoice>{Choice("1","配置 MSI 启用（需要重启核对）"),Choice("0","配置 MSI 关闭（需要重启核对）")}:new List<ManagerChoice>()});
                    }
                    else if(module=="devices")
                    {
                        int problem=Convert.ToInt32(row["ConfigManagerErrorCode"]);
                        bool child=(category=="MEDIA"||category=="Media")&&instance.StartsWith("HDAUDIO\\",StringComparison.OrdinalIgnoreCase)&&instance.IndexOf("VEN_10DE",StringComparison.OrdinalIgnoreCase)>=0;
                        string[] compatible=row["CompatibleID"] as string[]??new string[0];
                        bool controller=instance.StartsWith(@"PCI\VEN_10DE",StringComparison.OrdinalIgnoreCase)&&compatible.Any(x=>x.StartsWith(@"PCI\CC_0403",StringComparison.OrdinalIgnoreCase));
                        bool eligible=child||controller;
                        items.Add(new ManagerItem {id=id,title=title,detail=category+" · "+instance,current=problem==22?"设备已禁用":problem==0?"设备已启用":"设备问题代码 "+problem,Raw=problem==22?"disabled":problem==0?"enabled":"problem:"+problem,Identity=identity,supported=eligible&&(problem==0||problem==22),blocked=eligible?null:"显示、监视器、网络、存储、蓝牙及输入设备受保护；这里只控制 NVIDIA HDMI/DP 音频。",effect="影响显示器 HDMI/DP 音频及默认音频路由，不更改视频输出；先确认有其他可用音频设备。",choices=new List<ManagerChoice>{Choice("disabled","禁用 NVIDIA 显示器音频"),Choice("enabled","启用 NVIDIA 显示器音频")}});
                    }
                    else if(module=="interrupt-affinity")
                    {
                        string policy="missing",mask="missing";bool exists=false;
                        using(var key=Registry.LocalMachine.OpenSubKey(AffinityKey(instance)))if(key!=null){exists=true;policy=RegistryValue(key,"DevicePolicy");mask=RegistryValue(key,"AssignmentSetOverride");}
                        bool categoryAllowed=category=="Net"||category=="Display"||category=="USB";
                        bool supported=exists&&categoryAllowed&&Environment.ProcessorCount<=64&&(policy=="missing"||policy.StartsWith("d:"))&&mask!="unsupported";
                        var choices=new List<ManagerChoice>();
                        if(supported){choices.Add(Choice("d:0|missing","使用系统默认中断策略（建议保留）"));for(int cpu=0;cpu<Environment.ProcessorCount;cpu++)choices.Add(Choice("d:4|b:"+Convert.ToBase64String(BitConverter.GetBytes(1UL<<cpu)),"指定逻辑 CPU "+cpu+"（实验性）"));}
                        items.Add(new ManagerItem {id=id,title=title,detail=category+" · "+instance,current="DevicePolicy="+policy+" · AssignmentSetOverride="+mask,Raw=policy+"|"+mask,Identity=identity,supported=supported,blocked=supported?null:"仅管理已有 Affinity Policy 键的 PCI 显卡/网卡/USB 控制器；不处理存储或多处理器组。",effect="实验性中断路由，重启后由驱动应用；可能降低性能或导致设备异常。USB 控制器设置会影响该控制器上的所有外设，不是只改鼠标。",choices=choices});
                    }
                }
            return items;
        }
        internal static ManagerItem Read(string module,string id)
        {Decode(id);var item=Scan(module).FirstOrDefault(x=>x.id==id);if(item==null)throw new InvalidOperationException("PCI 设备已消失。");return item;}
        internal static void Write(string module,string id,string value)
        {
            var item=Read(module,id);if(!item.supported)throw new InvalidOperationException(item.blocked);string instance=Decode(id);
            if(module=="device-msi")
            {
                if(value!="0"&&value!="1")throw new ArgumentException("MSI 备份参数无效。");
                using(var key=Registry.LocalMachine.OpenSubKey(Key(instance),true))
                {if(key==null||key.GetValueKind("MSISupported")!=RegistryValueKind.DWord)throw new InvalidOperationException("驱动参数类型已改变。");key.SetValue("MSISupported",int.Parse(value),RegistryValueKind.DWord);}return;
            }
            if(module=="interrupt-affinity")
            {
                string[] parts=value.Split('|');if(parts.Length!=2||!(parts[0]=="missing"||parts[0].StartsWith("d:")))throw new ArgumentException("中断备份格式无效。");
                using(var key=Registry.LocalMachine.OpenSubKey(AffinityKey(instance),true)){if(key==null)throw new InvalidOperationException("中断参数位置已改变。");RegistryWrite(key,"AssignmentSetOverride",parts[1]);RegistryWrite(key,"DevicePolicy",parts[0]);}return;
            }
            if(value!="enabled"&&value!="disabled")throw new ArgumentException("设备目标状态无效。");
            using(var searcher=new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE 'PCI%' OR DeviceID LIKE 'HDAUDIO%'"))using(var rows=searcher.Get())
                foreach(ManagementObject row in rows)using(row)if(string.Equals(Convert.ToString(row["PNPDeviceID"]),instance,StringComparison.OrdinalIgnoreCase))
                {using(var result=row.InvokeMethod(value=="enabled"?"Enable":"Disable",null,null)){uint status=Convert.ToUInt32(result["ReturnValue"]);if(status!=0)throw new InvalidOperationException("Windows 设备操作失败："+status);}return;}
            throw new InvalidOperationException("设备已消失。");
        }
    }
}
