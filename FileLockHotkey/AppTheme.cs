namespace FileLockHotkey;

internal enum ButtonRole
{
    Secondary,
    Primary,
    Danger
}

internal static class AppTheme
{
    public static readonly Color Background = Color.FromArgb(246, 248, 251);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceAlt = Color.FromArgb(241, 245, 249);
    public static readonly Color Border = Color.FromArgb(216, 224, 235);
    public static readonly Color Text = Color.FromArgb(28, 35, 45);
    public static readonly Color Muted = Color.FromArgb(87, 99, 117);
    public static readonly Color Header = Color.FromArgb(24, 42, 55);
    public static readonly Color Accent = Color.FromArgb(27, 132, 129);
    public static readonly Color AccentDark = Color.FromArgb(18, 100, 98);
    public static readonly Color Danger = Color.FromArgb(177, 45, 57);
    public static readonly Color Warning = Color.FromArgb(186, 111, 24);

    public static Font CreateFont(float size, FontStyle style = FontStyle.Regular)
    {
        return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
    }

    public static void ApplyForm(Form form)
    {
        form.Font = CreateFont(9F);
        form.BackColor = Background;
        form.ForeColor = Text;
        form.Icon = AppIcons.AppIcon;
    }

    public static void StyleButton(Button button, ButtonRole role = ButtonRole.Secondary)
    {
        button.AutoSize = false;
        button.Cursor = Cursors.Hand;
        button.FlatStyle = FlatStyle.Flat;
        button.Font = CreateFont(9F);
        button.Margin = new Padding(8, 0, 0, 0);
        button.Padding = new Padding(10, 0, 10, 0);
        button.Size = new Size(Math.Max(button.Width, 104), 36);

        switch (role)
        {
            case ButtonRole.Primary:
                button.BackColor = Accent;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = AccentDark;
                break;
            case ButtonRole.Danger:
                button.BackColor = Color.White;
                button.ForeColor = Danger;
                button.FlatAppearance.BorderColor = Color.FromArgb(230, 178, 184);
                break;
            default:
                button.BackColor = Color.White;
                button.ForeColor = Text;
                button.FlatAppearance.BorderColor = Border;
                break;
        }

        button.FlatAppearance.BorderSize = 1;
    }
}
