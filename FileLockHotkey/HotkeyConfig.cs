using System.Text.Json;

namespace FileLockHotkey;

internal sealed record HotkeyConfig(uint Modifiers, Keys Key)
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModNoRepeat = 0x4000;

    public static HotkeyConfig Default { get; } = new(ModControl, Keys.Space);

    public string DisplayText
    {
        get
        {
            var parts = new List<string>();
            if ((Modifiers & ModControl) != 0)
            {
                parts.Add("Ctrl");
            }

            if ((Modifiers & ModAlt) != 0)
            {
                parts.Add("Alt");
            }

            if ((Modifiers & ModShift) != 0)
            {
                parts.Add("Shift");
            }

            parts.Add(GetKeyText(Key));
            return string.Join("+", parts);
        }
    }

    public uint RegisterModifiers => Modifiers | ModNoRepeat;

    public bool IsValid(out string? error)
    {
        if (Key == Keys.None || IsModifierKey(Key))
        {
            error = "请按下一个完整组合，例如 Ctrl+Alt+L。";
            return false;
        }

        if ((Modifiers & (ModControl | ModAlt)) == 0)
        {
            error = "快捷键至少需要包含 Ctrl 或 Alt。";
            return false;
        }

        error = null;
        return true;
    }

    public static HotkeyConfig FromKeyData(Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        uint modifiers = 0;

        if ((keyData & Keys.Control) == Keys.Control)
        {
            modifiers |= ModControl;
        }

        if ((keyData & Keys.Alt) == Keys.Alt)
        {
            modifiers |= ModAlt;
        }

        if ((keyData & Keys.Shift) == Keys.Shift)
        {
            modifiers |= ModShift;
        }

        return new HotkeyConfig(modifiers, key);
    }

    private static bool IsModifierKey(Keys key)
    {
        return key is Keys.ControlKey
            or Keys.LControlKey
            or Keys.RControlKey
            or Keys.Menu
            or Keys.LMenu
            or Keys.RMenu
            or Keys.ShiftKey
            or Keys.LShiftKey
            or Keys.RShiftKey;
    }

    private static string GetKeyText(Keys key)
    {
        return key switch
        {
            >= Keys.A and <= Keys.Z => key.ToString(),
            >= Keys.D0 and <= Keys.D9 => ((int)(key - Keys.D0)).ToString(),
            >= Keys.NumPad0 and <= Keys.NumPad9 => $"Num{(int)(key - Keys.NumPad0)}",
            >= Keys.F1 and <= Keys.F24 => key.ToString(),
            Keys.Space => "Space",
            Keys.Return => "Enter",
            Keys.Escape => "Esc",
            Keys.Back => "Backspace",
            Keys.Delete => "Delete",
            Keys.Insert => "Insert",
            Keys.Home => "Home",
            Keys.End => "End",
            Keys.PageUp => "PageUp",
            Keys.PageDown => "PageDown",
            Keys.Up => "Up",
            Keys.Down => "Down",
            Keys.Left => "Left",
            Keys.Right => "Right",
            Keys.OemMinus => "-",
            Keys.Oemplus => "=",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.OemPipe => "\\",
            Keys.OemSemicolon => ";",
            Keys.OemQuotes => "'",
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemQuestion => "/",
            Keys.Oemtilde => "`",
            _ => key.ToString()
        };
    }
}

internal static class HotkeySettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static HotkeyConfig Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return HotkeyConfig.Default;
            }

            var json = File.ReadAllText(SettingsPath);
            var data = JsonSerializer.Deserialize<HotkeySettingsData>(json, JsonOptions);
            var config = data?.ToConfig() ?? HotkeyConfig.Default;
            return config.IsValid(out _) ? config : HotkeyConfig.Default;
        }
        catch
        {
            return HotkeyConfig.Default;
        }
    }

    public static void Save(HotkeyConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var data = new HotkeySettingsData
        {
            Modifiers = config.Modifiers,
            Key = (int)config.Key
        };
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data, JsonOptions));
    }

    private static string SettingsPath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "FileLockHotkey", "settings.json");
        }
    }

    private sealed class HotkeySettingsData
    {
        public uint Modifiers { get; set; }
        public int Key { get; set; }

        public HotkeyConfig ToConfig()
        {
            return new HotkeyConfig(Modifiers, (Keys)Key);
        }
    }
}
