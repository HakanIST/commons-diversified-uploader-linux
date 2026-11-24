using UploaderCore;
using static Uploader.Ui;

namespace Uploader;

internal sealed partial class MainWindow
{
    private readonly Adw.ApplicationWindow _window;
    private readonly Session _session = new(new SecretStore());
    private readonly Gtk.Stack _root = Gtk.Stack.New();
    private readonly Adw.ViewStack _steps = Adw.ViewStack.New();
    private readonly Gtk.Box _matchPage = VBox(18), _rosterPage = VBox(18), _photosPage = VBox(0), _uploadPage = VBox(18);
    private readonly Gtk.Revealer _activityBar = Gtk.Revealer.New();
    private readonly Gtk.ProgressBar _progress = Gtk.ProgressBar.New();
    private readonly Gtk.Label _activityLabel = Label("");
    private readonly Gtk.Label _account = Label("", "dim-label");
    private readonly Gtk.Button _folderButton;
    private CancellationTokenSource? _work;
    private UploadCheck? _check;
    private bool _busy;

    public MainWindow(Adw.Application app)
    {
        _window = Adw.ApplicationWindow.New(app);
        _window.SetTitle("Commons Diversified Uploader");
        _window.SetDefaultSize(1240, 820);
        _window.SetIconName("tr.tall.CommonsDiversifiedUploader");
        LoadCss();

        var header = Adw.HeaderBar.New();
        var switcher = Adw.ViewSwitcher.New();
        switcher.SetStack(_steps);
        switcher.SetPolicy(Adw.ViewSwitcherPolicy.Wide);
        header.SetTitleWidget(switcher);
        _folderButton = Button(T("Klasör değiştir"), ChooseFolder);
        header.PackStart(_folderButton);
        header.PackEnd(_account);

        _steps.AddTitledWithIcon(Page(_matchPage.Margins(24)), "match", T("Maç"), "emblem-system-symbolic");
        _steps.AddTitledWithIcon(Page(_rosterPage.Margins(24)), "roster", T("Kadrolar"), "system-users-symbolic");
        _steps.AddTitledWithIcon(_photosPage, "photos", T("Fotoğraflar"), "image-x-generic-symbolic");
        _steps.AddTitledWithIcon(Page(_uploadPage.Margins(24)), "upload", T("Yükleme"), "document-send-symbolic");
        _steps.OnNotify += (_, e) => { if (e.Pspec.GetName() == "visible-child") RefreshCurrent(); };

        var bar = HBox(12).Margins(10);
        _progress.SetHexpand(true);
        _progress.SetValign(Gtk.Align.Center);
        _activityLabel.SetWrap(false);
        _activityLabel.SetEllipsize(Pango.EllipsizeMode.Middle);
        _activityLabel.SetMaxWidthChars(60);
        bar.Append(_activityLabel);
        bar.Append(_progress);
        bar.Append(Button(T("Durdur"), () => _work?.Cancel()));
        _activityBar.SetChild(bar);
        _activityBar.SetTransitionType(Gtk.RevealerTransitionType.SlideUp);

        var main = Adw.ToolbarView.New();
        main.AddTopBar(header);
        main.SetContent(_steps);
        main.AddBottomBar(_activityBar);

        _root.SetTransitionType(Gtk.StackTransitionType.Crossfade);
        _root.SetTransitionDuration(600);
        _root.AddNamed(BuildSplash(), "splash");
        _root.AddNamed(BuildWelcome(), "welcome");
        _root.AddNamed(main, "main");
        _window.SetContent(_root);

        var context = SynchronizationContext.Current;
        _session.Changed += () => context?.Post(_ => { UpdateAccount(); RefreshCurrent(); }, null);
        UpdateAccount();
        _ = Start();
    }

    public void Present() => _window.Present();

    private static void LoadCss()
    {
        var provider = Gtk.CssProvider.New();
        provider.LoadFromString("""
            .splash { background: linear-gradient(135deg, #241f6b, #3a3fd0 60%, #4a5be6); color: white; }
            .splash-title { font-size: 28px; font-weight: 800; }
            .splash progressbar trough { background: alpha(white, 0.25); min-height: 4px; }
            .splash progressbar progress { background: #f6b545; min-height: 4px; }
            .photo-card { border-radius: 12px; background: alpha(currentColor, 0.06); }
            .photo-card picture { border-radius: 12px 12px 0 0; }
            .swatch { border-radius: 99px; min-width: 14px; min-height: 14px; border: 1px solid alpha(currentColor, 0.3); }
            .stat { font-size: 30px; font-weight: 700; }
            .tag { border-radius: 99px; padding: 1px 8px; font-size: 11px; font-weight: 700; color: white; }
            .tag.ok { background: #26a269; } .tag.bad { background: #c01c28; }
            """);
        Gtk.StyleContext.AddProviderForDisplay(Gdk.Display.GetDefault()!, provider, 600);
    }

    // MARK: Açılış

    private static string? AssetPath(string name)
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "assets", name),
            Path.Combine("/usr/share/commons-diversified-uploader", name),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private readonly Gtk.ProgressBar _splashProgress = Gtk.ProgressBar.New();
    private readonly Gtk.Revealer _splashIcon = Gtk.Revealer.New(), _splashTitle = Gtk.Revealer.New(), _splashTagline = Gtk.Revealer.New();

    private Gtk.Widget BuildSplash()
    {
        var box = VBox(14);
        box.SetValign(Gtk.Align.Center);
        box.SetHalign(Gtk.Align.Center);
        var icon = AssetPath("icon.png") is { } path ? Gtk.Picture.NewForFilename(path) : Gtk.Picture.New();
        icon.SetSizeRequest(190, 190);
        icon.SetCanShrink(true);
        foreach (var (revealer, child, type) in new (Gtk.Revealer, Gtk.Widget, Gtk.RevealerTransitionType)[]
        {
            (_splashIcon, icon, Gtk.RevealerTransitionType.Crossfade),
            (_splashTitle, Label("Commons Diversified Uploader", "splash-title"), Gtk.RevealerTransitionType.SlideUp),
            (_splashTagline, Label(T("Forma numarasından oyuncuya, oyuncudan kategoriye")), Gtk.RevealerTransitionType.Crossfade),
        })
        {
            revealer.SetTransitionType(type);
            revealer.SetTransitionDuration(900);
            revealer.SetChild(child);
            revealer.SetHalign(Gtk.Align.Center);
            box.Append(revealer);
        }
        _splashIcon.SetSizeRequest(190, 190);
        _splashProgress.SetSizeRequest(260, -1);
        _splashProgress.SetHalign(Gtk.Align.Center);
        _splashProgress.SetMarginTop(10);
        box.Append(_splashProgress);
        var frame = VBox(0);
        frame.AddCssClass("splash");
        box.SetVexpand(true);
        frame.Append(box);
        return frame;
    }

    /// <summary>Yaklaşık 3,6 saniyelik açılış: simge, başlık, alt yazı sırayla belirir; bu sırada oturum geri yüklenir.</summary>
    private async Task Start()
    {
        var restore = _session.RestoreLogin();
        const int total = 3600, tick = 30;
        for (var t = 0; t <= total; t += tick)
        {
            if (t >= 150) _splashIcon.SetRevealChild(true);
            if (t >= 900) _splashTitle.SetRevealChild(true);
            if (t >= 1500) _splashTagline.SetRevealChild(true);
            _splashProgress.SetFraction(Math.Min(1, t / (double)(total - 300)));
            await Task.Delay(tick);
        }
        try { await restore; } catch (Exception) { }
        UpdateAccount();
        if (Environment.GetEnvironmentVariable("CDU_FOLDER") is { Length: > 0 } folder && Directory.Exists(folder)) OpenFolder(folder);
        else _root.SetVisibleChildName("welcome");
    }

    private Gtk.Widget BuildWelcome()
    {
        var status = Adw.StatusPage.New();
        status.SetTitle(T("Maç fotoğraflarınızı Commons'a taşıyın"));
        status.SetDescription(T("Bir maçın fotoğraflarını içeren klasörü seçin. Forma numaraları bu bilgisayarda okunur, oyuncular kadrodan bulunur ve Commons kategorileri önerilir."));
        if (AssetPath("icon.png") is { } path) status.SetPaintable(Gdk.Texture.NewFromFilename(path));
        var button = Button(T("Klasör seç"), ChooseFolder, "suggested-action", "pill");
        button.SetHalign(Gtk.Align.Center);
        status.SetChild(button);
        var view = Adw.ToolbarView.New();
        view.AddTopBar(Adw.HeaderBar.New());
        view.SetContent(status);
        return view;
    }

    // MARK: Klasör ve ortak işler

    private async void ChooseFolder()
    {
        try
        {
            var dialog = Gtk.FileDialog.New();
            dialog.SetTitle(T("Maç fotoğraflarının bulunduğu klasörü seçin"));
            if (await dialog.SelectFolderAsync(_window) is { } file && file.GetPath() is { } path) OpenFolder(path);
        }
        catch (Exception) { /* pencere kapatıldı */ }
    }

    private void OpenFolder(string folder)
    {
        try
        {
            _check = null;
            _cards.Clear();
            _selected = null;
            _session.Open(folder);
            _root.SetVisibleChildName("main");
            if (Environment.GetEnvironmentVariable("CDU_STEP") is { Length: > 0 } step) _steps.SetVisibleChildName(step);
            RefreshCurrent();
        }
        catch (Exception e) { Alert(T("Proje dosyası okunamadı: {0}", e.Message)); }
    }

    private void RefreshCurrent()
    {
        if (_session.Folder == null) return;
        switch (_steps.GetVisibleChildName())
        {
            case "match": BuildMatch(); break;
            case "roster": BuildRoster(); break;
            case "photos": RefreshPhotos(); break;
            case "upload": BuildUpload(); break;
        }
    }

    private void UpdateAccount() => _account.SetText(_session.Username ?? T("Oturum açılmadı"));

    private void Alert(string message)
    {
        var dialog = Adw.AlertDialog.New(T("Bir sorun oluştu"), message);
        dialog.AddResponse("ok", T("Tamam"));
        dialog.Present(_window);
    }

    /// <summary>Uzun işi (analiz, yükleme) alttaki ilerleme çubuğuyla çalıştırır.</summary>
    private async Task Run(Func<IProgress<Activity>, CancellationToken, Task> work, string failure)
    {
        if (_busy) return;
        _busy = true;
        _work = new CancellationTokenSource();
        _activityBar.SetRevealChild(true);
        var progress = new Progress<Activity>(a =>
        {
            _activityLabel.SetText(a.Message);
            if (a.Fraction is { } f) _progress.SetFraction(f); else _progress.Pulse();
        });
        try { await work(progress, _work.Token); }
        catch (OperationCanceledException) { }
        catch (Exception e) { Alert(T(failure, e.Message)); }
        finally
        {
            _busy = false;
            _activityBar.SetRevealChild(false);
            _work.Dispose();
            _work = null;
            RefreshCurrent();
        }
    }
}
