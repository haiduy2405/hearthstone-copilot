using System;
using System.IO;
using Newtonsoft.Json;

namespace Copilot.HdtPlugin
{
    /// <summary>User settings in %APPDATA%\HearthstoneCopilot\config.json. Written with defaults on first run.</summary>
    internal sealed class PanelConfig
    {
        public double Left { get; set; } = 24;
        public double Top { get; set; } = 140;
        public double Scale { get; set; } = 1.0;
        public double Opacity { get; set; } = 0.95;
        /// <summary>F1..F12. Toggles the step-by-step details.</summary>
        public string Hotkey { get; set; } = "F8";
        /// <summary>true: mouse clicks pass through the panel to the game. false: clicking the panel toggles details.</summary>
        public bool ClickThrough { get; set; } = true;
        /// <summary>Play a beep when LETHAL / LETHAL? / DANGER first appears in a turn.</summary>
        public bool Sound { get; set; } = true;

        public int HotkeyVirtualKey()
        {
            var h = (Hotkey ?? "F8").Trim().ToUpperInvariant();
            if (h.StartsWith("F") && int.TryParse(h.Substring(1), out var n) && n >= 1 && n <= 12)
                return 0x70 + n - 1;
            return 0x77; // F8
        }

        public static PanelConfig Load(string path, Action<string> log)
        {
            try
            {
                if (File.Exists(path))
                {
                    var c = JsonConvert.DeserializeObject<PanelConfig>(File.ReadAllText(path)) ?? new PanelConfig();
                    c.Scale = Math.Max(0.5, Math.Min(3.0, c.Scale));
                    c.Opacity = Math.Max(0.2, Math.Min(1.0, c.Opacity));
                    return c;
                }
            }
            catch (Exception ex) { log("Config unreadable, using defaults: " + ex.Message); }

            var d = new PanelConfig();
            try { File.WriteAllText(path, JsonConvert.SerializeObject(d, Formatting.Indented)); }
            catch (Exception ex) { log("Could not write default config: " + ex.Message); }
            return d;
        }
    }
}
