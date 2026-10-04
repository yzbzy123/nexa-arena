using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace NexaArena
{
    internal interface IHotkeyBackend
    {
        bool Register(int id, uint modifiers, int key);
        void Unregister(int id);
    }
    internal sealed class WindowsHotkeyBackend : IHotkeyBackend
    {
        private readonly IntPtr window;
        public WindowsHotkeyBackend(IntPtr window) { this.window = window; }
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        public bool Register(int id, uint modifiers, int key) { return RegisterHotKey(window, id, modifiers | 0x4000, (uint)key); }
        public void Unregister(int id) { UnregisterHotKey(window, id); }
    }
    internal sealed class HotkeyManager : IDisposable
    {
        public const int FirstId = 0x6201;
        private readonly IHotkeyBackend backend;
        private List<HotkeyOption> active = new List<HotkeyOption>();
        public HotkeyManager(IHotkeyBackend backend) { this.backend = backend; }
        public string ActionFor(int id)
        {
            int index = id - FirstId;
            return index >= 0 && index < HotkeyRules.Actions.Length && active.Any(k => k.Enabled && k.Action == HotkeyRules.Actions[index])
                ? HotkeyRules.Actions[index] : null;
        }
        private void UnregisterAll()
        {
            for (int i = 0; i < HotkeyRules.Actions.Length; i++) backend.Unregister(FirstId + i);
        }
        private void RegisterAll(List<HotkeyOption> keys)
        {
            foreach (HotkeyOption key in keys.Where(k => k.Enabled))
            {
                int id = FirstId + Array.IndexOf(HotkeyRules.Actions, key.Action);
                if (!backend.Register(id, key.Modifiers, key.Key)) throw new InvalidOperationException("快捷键 " + key + " 被占用或系统不允许注册。");
            }
        }
        public void Apply(List<HotkeyOption> keys)
        {
            HotkeyRules.Validate(keys);
            if (active.Count == keys.Count && active.All(a => keys.Any(k => k.Action == a.Action && k.Enabled == a.Enabled && k.Key == a.Key && k.Modifiers == a.Modifiers))) return;
            List<HotkeyOption> previous = active.Select(k => k.Copy()).ToList();
            UnregisterAll();
            try { RegisterAll(keys); active = keys.Select(k => k.Copy()).ToList(); }
            catch (Exception original)
            {
                UnregisterAll();
                try { RegisterAll(previous); active = previous; }
                catch { UnregisterAll(); active.Clear(); throw new InvalidOperationException(original.Message + " 原快捷键也未能重新注册，请重新设置。"); }
                throw;
            }
        }
        public void Dispose() { UnregisterAll(); active.Clear(); }
    }
}
