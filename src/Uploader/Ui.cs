using UploaderCore;

namespace Uploader;

/// <summary>Küçük arayüz yardımcıları.</summary>
internal static class Ui
{
    public static string T(string turkish, params object?[] args) => L.T(turkish, args);

    public static Gtk.Box VBox(int spacing = 12) => Gtk.Box.New(Gtk.Orientation.Vertical, spacing);
    public static Gtk.Box HBox(int spacing = 8) => Gtk.Box.New(Gtk.Orientation.Horizontal, spacing);

    public static Gtk.Label Label(string text, params string[] css)
    {
        var label = Gtk.Label.New(text);
        label.SetXalign(0);
        label.SetWrap(true);
        label.SetWrapMode(Pango.WrapMode.WordChar);
        foreach (var c in css) label.AddCssClass(c);
        return label;
    }

    public static Gtk.Button Button(string text, Action onClick, params string[] css)
    {
        var button = Gtk.Button.NewWithLabel(text);
        foreach (var c in css) button.AddCssClass(c);
        button.OnClicked += (_, _) => onClick();
        return button;
    }

    public static T Margins<T>(this T widget, int all) where T : Gtk.Widget
    {
        widget.SetMarginTop(all); widget.SetMarginBottom(all); widget.SetMarginStart(all); widget.SetMarginEnd(all);
        return widget;
    }

    /// <summary>Metin alanı her değiştiğinde çağrılır (Gtk.Entry, Adw.EntryRow…).</summary>
    public static void OnText(GObject.Object widget, Action<string> changed)
    {
        widget.OnNotify += (sender, args) =>
        {
            if (args.Pspec.GetName() == "text" && sender is Gtk.Editable editable) changed(editable.GetText());
        };
    }

    public static Adw.EntryRow EntryRow(string title, string value, Action<string> changed)
    {
        var row = Adw.EntryRow.New();
        row.SetTitle(title);
        row.SetText(value);
        OnText(row, changed);
        return row;
    }

    /// <summary>Geniş ekranlarda içeriği ortalanmış, okunur genişlikte bir sütuna alır.</summary>
    public static Gtk.Widget Page(Gtk.Widget content, int maxWidth = 1100)
    {
        var clamp = Adw.Clamp.New();
        clamp.SetMaximumSize(maxWidth);
        clamp.SetChild(content);
        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetPolicy(Gtk.PolicyType.Never, Gtk.PolicyType.Automatic);
        scroll.SetChild(clamp);
        scroll.SetVexpand(true);
        return scroll;
    }

    public static void Clear(Gtk.Box box)
    {
        while (box.GetFirstChild() is { } child) box.Remove(child);
    }

    public static List<string> SplitList(string text) => text.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
}
