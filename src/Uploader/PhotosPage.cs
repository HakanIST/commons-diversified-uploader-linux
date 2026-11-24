using UploaderCore;
using static Uploader.Ui;

namespace Uploader;

internal sealed partial class MainWindow
{
    private sealed record Card(Gtk.Widget Root, Gtk.Label Title, Gtk.Label Tag);

    private static readonly (string Key, string Title, Func<PhotoRow, bool> Matches)[] Filters =
    [
        ("all", "Tümü", _ => true),
        ("found", "Oyuncu bulundu", r => r.Players.Count > 0),
        ("unresolved", "Çözülemeyen numara", r => r.Unresolved.Count > 0),
        ("none", "Oyuncu yok", r => r.Analyzed && r.Players.Count == 0),
        ("pending", "Analiz edilmedi", r => !r.Analyzed),
    ];

    private readonly Dictionary<string, Card> _cards = [];
    private readonly Dictionary<string, Gtk.ToggleButton> _filterButtons = [];
    private Gtk.FlowBox? _grid;
    private Gtk.Box? _inspector;
    private Gtk.Button? _analyzeButton;
    private string _filter = "all";
    private string? _selected;
    private string? _inspectorSignature;

    private void BuildPhotosShell()
    {
        Clear(_photosPage);
        var bar = HBox(8).Margins(12);
        _analyzeButton = Button("", () => _ = Run((p, ct) => _session.Analyze(null, p, ct), "Analiz başlatılamadı: {0}"), "suggested-action");
        bar.Append(_analyzeButton);
        _filterButtons.Clear();
        Gtk.ToggleButton? group = null;
        foreach (var (key, _, _) in Filters)
        {
            var toggle = Gtk.ToggleButton.New();
            if (group == null) group = toggle; else toggle.SetGroup(group);
            toggle.SetActive(key == _filter);
            toggle.OnToggled += (_, _) => { if (toggle.GetActive()) { _filter = key; RefreshPhotos(); } };
            _filterButtons[key] = toggle;
            bar.Append(toggle);
        }
        _photosPage.Append(bar);

        _grid = Gtk.FlowBox.New();
        _grid.SetSelectionMode(Gtk.SelectionMode.Single);
        _grid.SetActivateOnSingleClick(true);
        _grid.SetHomogeneous(true);
        _grid.SetMinChildrenPerLine(2);
        _grid.SetMaxChildrenPerLine(4);
        _grid.SetRowSpacing(10);
        _grid.SetColumnSpacing(10);
        _grid.SetValign(Gtk.Align.Start);
        _grid.OnChildActivated += (_, e) =>
        {
            _selected = e.Child.GetChild()?.GetName();
            RefreshPhotos();
        };
        var gridScroll = Gtk.ScrolledWindow.New();
        gridScroll.SetPolicy(Gtk.PolicyType.Never, Gtk.PolicyType.Automatic);
        gridScroll.SetChild(_grid.Margins(12));
        gridScroll.SetHexpand(true);

        _inspector = VBox(12).Margins(16);
        var inspectorScroll = Gtk.ScrolledWindow.New();
        inspectorScroll.SetPolicy(Gtk.PolicyType.Never, Gtk.PolicyType.Automatic);
        inspectorScroll.SetChild(_inspector);
        inspectorScroll.SetSizeRequest(460, -1);

        var paned = Gtk.Paned.New(Gtk.Orientation.Horizontal);
        paned.SetStartChild(gridScroll);
        paned.SetEndChild(inspectorScroll);
        paned.SetResizeEndChild(false);
        paned.SetShrinkEndChild(false);
        paned.SetVexpand(true);
        _photosPage.Append(paned);
    }

    private Card MakeCard(PhotoRow row)
    {
        var root = VBox(0);
        root.AddCssClass("photo-card");
        root.SetName(row.Name);
        var picture = Gtk.Picture.New();
        picture.SetContentFit(Gtk.ContentFit.Cover);
        picture.SetSizeRequest(200, 140);
        picture.SetCanShrink(true);
        root.Append(picture);
        var text = VBox(2).Margins(8);
        var title = Label("", "heading");
        title.SetWrap(false);
        title.SetEllipsize(Pango.EllipsizeMode.End);
        text.Append(title);
        var line = HBox(6);
        line.Append(Label(row.Name, "caption", "dim-label"));
        var tag = Label("", "tag");
        tag.SetVisible(false);
        line.Append(tag);
        text.Append(line);
        root.Append(text);
        _ = LoadThumbnail(picture, Path.Combine(_session.Folder!, row.Name));
        return new Card(root, title, tag);
    }

    private static async Task LoadThumbnail(Gtk.Picture picture, string path)
    {
        if (await Thumbs.Thumbnail(path) is { } thumbnail) picture.SetFilename(thumbnail);
    }

    private void RefreshPhotos()
    {
        if (_grid == null || _cards.Count == 0) BuildPhotosShell();
        var rows = _session.Rows();
        foreach (var row in rows)
        {
            if (!_cards.TryGetValue(row.Name, out var card))
            {
                card = _cards[row.Name] = MakeCard(row);
                _grid!.Append(card.Root);
            }
            card.Title.SetText(row.Players.Count > 0 ? string.Join(", ", row.Players.Select(p => $"{p.Number} {p.Name}")) : T(row.Analyzed ? "Oyuncu bulunamadı" : "Analiz bekliyor"));
            card.Root.SetOpacity(row.Include ? 1 : 0.45);
            card.Tag.SetVisible(row.Uploaded != null || row.UploadError != null);
            card.Tag.SetText(T(row.Uploaded != null ? "Yüklendi" : "Hata"));
            card.Tag.RemoveCssClass("ok"); card.Tag.RemoveCssClass("bad");
            card.Tag.AddCssClass(row.Uploaded != null ? "ok" : "bad");
            card.Root.GetParent()?.SetVisible(Filters.First(f => f.Key == _filter).Matches(row));
        }
        foreach (var (key, title, matches) in Filters) _filterButtons[key].SetLabel($"{T(title)} {rows.Count(matches)}");
        var pending = rows.Count(r => !r.Analyzed);
        _analyzeButton!.SetLabel(pending > 0 ? T("Analiz et ({0})", pending) : T("Tümü analiz edildi"));
        _analyzeButton.SetSensitive(pending > 0 && !_busy);
        _selected ??= Environment.GetEnvironmentVariable("CDU_SELECT") ?? rows.FirstOrDefault()?.Name;
        BuildInspector(rows.FirstOrDefault(r => r.Name == _selected));
    }

    private void BuildInspector(PhotoRow? row)
    {
        // Yazarken odak kaybolmasın: içerik değişmediyse yeniden kurma.
        var signature = row == null ? "" : System.Text.Json.JsonSerializer.Serialize(row) + _busy;
        if (signature == _inspectorSignature) return;
        _inspectorSignature = signature;
        var box = _inspector!;
        Clear(box);
        if (row == null)
        {
            box.Append(Label(T("Ayrıntı için bir fotoğraf seçin."), "dim-label"));
            return;
        }
        var record = _session.Project.Photos[row.Name];
        var path = Path.Combine(_session.Folder!, row.Name);

        var picture = Gtk.Picture.New();
        picture.SetContentFit(Gtk.ContentFit.Contain);
        picture.SetSizeRequest(-1, 280);
        picture.SetCanShrink(true);
        box.Append(picture);
        _ = LoadPreview(picture, path, row);

        var include = Gtk.CheckButton.NewWithLabel($"{T("Bu fotoğrafı yükle")} · {row.Name}{(row.Date != null ? " · " + row.Date : "")}");
        include.SetActive(row.Include);
        include.OnToggled += (_, _) => { record.Include = include.GetActive(); _session.Touch(); };
        box.Append(include);

        var playersHeader = HBox(8);
        playersHeader.Append(Label(T(row.Manual ? "Oyuncular (elle düzenlendi)" : "Oyuncular"), "caption", "dim-label"));
        if (row.Manual) playersHeader.Append(Button(T("Otomatiğe dön"), () => { record.Manual = null; _session.Touch(); }, "flat"));
        box.Append(playersHeader);
        List<ManualPlayer> Current() => record.Manual ?? row.Players.Select(p => new ManualPlayer(p.Team, p.Number)).ToList();
        var list = Gtk.ListBox.New();
        list.AddCssClass("boxed-list");
        list.SetSelectionMode(Gtk.SelectionMode.None);
        for (var i = 0; i < row.Players.Count; i++)
        {
            var player = row.Players[i];
            var index = i;
            var item = Adw.ActionRow.New();
            item.SetTitle(GLib.Functions.MarkupEscapeText($"{player.Number}  {player.Name}", -1));
            var team = _session.Project.Match.Teams.FirstOrDefault(t => t.Key == player.Team)?.Label ?? player.Team;
            var details = new List<string> { team };
            if (player.Confidence is { } confidence) details.Add(T("güven %{0}", (int)(confidence * 100)));
            if (player.Category == null) details.Add(T("kategorisi yok"));
            item.SetSubtitle(GLib.Functions.MarkupEscapeText(string.Join(" · ", details), -1));
            var remove = Gtk.Button.NewFromIconName("list-remove-symbolic");
            remove.AddCssClass("flat");
            remove.SetValign(Gtk.Align.Center);
            remove.SetTooltipText(T("Kaldır"));
            remove.OnClicked += (_, _) => { var manual = Current(); manual.RemoveAt(index); record.Manual = manual; _session.Touch(); };
            item.AddSuffix(remove);
            list.Append(item);
        }
        if (row.Players.Count == 0) list.Append(Label(T("Oyuncu yok"), "dim-label").Margins(10));
        box.Append(list);

        var teams = _session.Project.Match.Teams;
        var add = HBox(6);
        var teamDrop = Gtk.DropDown.NewFromStrings(teams.Select(t => t.Label.Length > 0 ? t.Label : t.Key.ToUpperInvariant()).ToArray());
        var playerDrop = Gtk.DropDown.NewFromStrings([]);
        void FillPlayers() => playerDrop.SetModel(Gtk.StringList.New(teams[(int)teamDrop.GetSelected()].Players.Select(p => $"{p.Number} {p.Name}").ToArray()));
        FillPlayers();
        teamDrop.OnNotify += (_, e) => { if (e.Pspec.GetName() == "selected") FillPlayers(); };
        playerDrop.SetHexpand(true);
        add.Append(teamDrop);
        add.Append(playerDrop);
        add.Append(Button(T("Ekle"), () =>
        {
            var team = teams[(int)teamDrop.GetSelected()];
            if (playerDrop.GetSelected() >= team.Players.Count) return;
            var manual = Current();
            var chosen = new ManualPlayer(team.Key, team.Players[(int)playerDrop.GetSelected()].Number);
            if (!manual.Contains(chosen)) manual.Add(chosen);
            record.Manual = manual;
            _session.Touch();
        }));
        box.Append(add);

        box.Append(Label(T("Commons dosya adı"), "caption", "dim-label"));
        var name = Gtk.Entry.New();
        name.SetText(row.Title);
        OnText(name, v => { record.Title = v.Length > 0 && v != row.Title ? v : record.Title; _session.Save(); });
        box.Append(name);
        if (row.CustomTitle) box.Append(Button(T("Şablondaki ada dön"), () => { record.Title = null; _session.Touch(); }, "flat"));

        box.Append(Label(T("Kategoriler"), "caption", "dim-label"));
        if (row.Categories.Count == 0) box.Append(Label(T("Kategori yok: maç ya da takım kategorisi girin."), "warning"));
        foreach (var category in row.Categories) box.Append(Label("• " + category));

        if (row.Uploaded is { } uploaded)
        {
            var link = uploaded.Url != null ? Gtk.LinkButton.NewWithLabel(uploaded.Url, uploaded.Title) : (Gtk.Widget)Label(uploaded.Title, "success");
            link.SetHalign(Gtk.Align.Start);
            box.Append(link);
        }
        if (row.UploadError is { } uploadError) box.Append(Label(uploadError, "error"));
        if (row.Error is { } error) box.Append(Label(T("Analiz hatası: {0}", error), "error"));
        var again = Button(T("Yeniden analiz et"), () =>
        {
            record.Analysis = null;
            _ = Run((p, ct) => _session.Analyze([row.Name], p, ct), "Analiz başlatılamadı: {0}");
        });
        again.SetHalign(Gtk.Align.Start);
        again.SetSensitive(!_busy);
        box.Append(again);
    }

    private static async Task LoadPreview(Gtk.Picture picture, string path, PhotoRow row)
    {
        if (await Thumbs.Thumbnail(path) is { } quick) picture.SetFilename(quick);
        if (await Thumbs.Preview(path, row) is { } preview) picture.SetFilename(preview);
    }
}
