using UploaderCore;
using static Uploader.Ui;

namespace Uploader;

internal sealed partial class MainWindow
{
    private static readonly Dictionary<string, string> Swatches = new()
    {
        ["siyah"] = "#111111", ["beyaz"] = "#ffffff", ["gri"] = "#8e8e93", ["kırmızı"] = "#d92e21", ["turuncu"] = "#ff9500", ["sarı"] = "#fcdb33",
        ["yeşil"] = "#17a34a", ["turkuaz"] = "#32ade6", ["mavi"] = "#2663eb", ["lacivert"] = "#1f2978", ["mor"] = "#af52de", ["pembe"] = "#ff2d55", ["bordo"] = "#7a1f2b",
    };
    private static readonly string[] Licenses = ["cc-by-sa-4.0", "cc-by-4.0", "cc-zero"];

    private void BuildMatch()
    {
        Clear(_matchPage);
        var match = _session.Project.Match;

        var folder = Adw.PreferencesGroup.New();
        var folderRow = Adw.ActionRow.New();
        folderRow.SetTitle(Path.GetFileName(_session.Folder!.TrimEnd(Path.DirectorySeparatorChar)));
        folderRow.SetSubtitle($"{Path.GetDirectoryName(_session.Folder)} · {T("{0} fotoğraf", _session.Project.Photos.Count)}");
        folderRow.AddPrefix(Gtk.Image.NewFromIconName("folder-symbolic"));
        folder.Add(folderRow);
        _matchPage.Append(folder);

        var teams = HBox(18);
        teams.SetHomogeneous(true);
        for (var i = 0; i < match.Teams.Count; i++) teams.Append(TeamGroup(match.Teams[i], i));
        _matchPage.Append(teams);

        var details = Adw.PreferencesGroup.New();
        details.SetTitle(T("Maç ve yükleme bilgileri"));
        var title = EntryRow(T("Maç başlığı"), match.Title, v => { match.Title = v; _session.Save(); });
        title.SetTooltipText(T("Dosya adında ve açıklamada kullanılır"));
        details.Add(title);
        var category = EntryRow(T("Maç kategorisi"), match.Category, v => { match.Category = v; _session.Save(); });
        category.SetTooltipText(T("Boşsa takım kategorileri kullanılır"));
        details.Add(category);
        var create = Adw.SwitchRow.New();
        create.SetTitle(T("Maç kategorisi yoksa oluştur"));
        create.SetSubtitle(T("Commons'ta yoksa takım kategorilerinin altında oluşturulur"));
        create.SetActive(match.CreateCategory);
        create.OnNotify += (_, e) => { if (e.Pspec.GetName() == "active") { match.CreateCategory = create.GetActive(); _session.Save(); } };
        details.Add(create);
        details.Add(EntryRow(T("Diğer kategoriler (virgülle)"), string.Join(", ", match.ExtraCategories), v => { match.ExtraCategories = SplitList(v); _session.Save(); }));
        var template = EntryRow(T("Dosya adı şablonu"), match.TitleTemplate, v => { match.TitleTemplate = v; _session.Save(); });
        template.SetTooltipText(T("Alanlar: {oyuncular} {numaralar} {maç} {sıra}"));
        details.Add(template);
        var author = EntryRow(T("Yazar"), match.Author, v => { match.Author = v; _session.Save(); });
        author.SetTooltipText(T("Boşsa Commons kullanıcı adınız"));
        details.Add(author);
        var license = Adw.ComboRow.New();
        license.SetTitle(T("Lisans"));
        license.SetModel(Gtk.StringList.New(["CC BY-SA 4.0", "CC BY 4.0", T("CC0 (kamu malı)")]));
        license.SetSelected((uint)Math.Max(0, Array.IndexOf(Licenses, match.License)));
        license.OnNotify += (_, e) => { if (e.Pspec.GetName() == "selected") { match.License = Licenses[license.GetSelected()]; _session.Save(); } };
        details.Add(license);
        _matchPage.Append(details);
    }

    private Gtk.Widget TeamGroup(Team team, int index)
    {
        var group = Adw.PreferencesGroup.New();
        group.SetTitle(team.Label.Length > 0 ? team.Label : T("{0}. takım", index + 1));
        group.SetDescription(T("{0} oyuncu · {1}", team.Players.Count, team.Category.Length > 0 ? team.Category : T("Commons kategorisi yok")));

        var results = Gtk.ListBox.New();
        results.AddCssClass("boxed-list");
        results.SetSelectionMode(Gtk.SelectionMode.None);
        results.SetVisible(false);
        results.SetMarginBottom(12);
        var search = Adw.EntryRow.New();
        search.SetTitle(T("Takım ara (ör. Fenerbahçe basketbol)"));
        search.SetShowApplyButton(true);
        search.OnApply += async (_, _) =>
        {
            try
            {
                var hits = await Wikidata.Search(search.GetText(), L.IsTurkish ? "tr" : "en");
                results.RemoveAll();
                if (hits.Count == 0) results.Append(Label(T("Sonuç yok"), "dim-label").Margins(10));
                foreach (var hit in hits)
                {
                    var row = Adw.ActionRow.New();
                    row.SetTitle(GLib.Functions.MarkupEscapeText(hit.Label, -1));
                    row.SetSubtitle(GLib.Functions.MarkupEscapeText($"{hit.Description} · {hit.Qid}", -1));
                    row.SetActivatable(true);
                    row.OnActivated += async (_, _) =>
                    {
                        results.SetSensitive(false);
                        try { await _session.SetTeam(team.Key, hit.Qid); }
                        catch (Exception e) { Alert(T("Kadro getirilemedi: {0}", e.Message)); results.SetSensitive(true); }
                    };
                    results.Append(row);
                }
                results.SetVisible(true);
            }
            catch (Exception e) { Alert(T("Arama yapılamadı: {0}", e.Message)); }
        };
        group.Add(search);
        group.Add(results);

        var colorsRow = Adw.PreferencesRow.New();
        colorsRow.SetActivatable(false);
        var colorsBox = VBox(8).Margins(12);
        colorsBox.Append(Label(T("Bu maçtaki forma renkleri"), "caption", "dim-label"));
        var flow = Gtk.FlowBox.New();
        flow.SetSelectionMode(Gtk.SelectionMode.None);
        flow.SetMaxChildrenPerLine(7);
        flow.SetRowSpacing(6);
        flow.SetColumnSpacing(6);
        foreach (var color in JerseyColor.Names)
        {
            var toggle = Gtk.ToggleButton.New();
            var content = HBox(6);
            var swatch = Gtk.Box.New(Gtk.Orientation.Horizontal, 0);
            swatch.AddCssClass("swatch");
            swatch.SetValign(Gtk.Align.Center);
            var css = Gtk.CssProvider.New();
            css.LoadFromString($".swatch-{Array.IndexOf(JerseyColor.Names, color)} {{ background: {Swatches[color]}; }}");
            Gtk.StyleContext.AddProviderForDisplay(Gdk.Display.GetDefault()!, css, 600);
            swatch.AddCssClass($"swatch-{Array.IndexOf(JerseyColor.Names, color)}");
            content.Append(swatch);
            content.Append(Gtk.Label.New(T(color)));
            toggle.SetChild(content);
            toggle.SetActive(team.Colors.Contains(color));
            toggle.OnToggled += (_, _) =>
            {
                team.Colors.Remove(color);
                if (toggle.GetActive()) team.Colors.Add(color);
                _session.Save();
            };
            flow.Append(toggle);
        }
        colorsBox.Append(flow);
        colorsRow.SetChild(colorsBox);
        group.Add(colorsRow);
        group.Add(EntryRow(T("Formadaki yazılar (takım adı, sponsor; virgülle)"), string.Join(", ", team.Keywords), v => { team.Keywords = SplitList(v); _session.Save(); }));
        group.Add(EntryRow(T("Takımın Commons kategorisi"), team.Category, v => { team.Category = v; _session.Save(); }));
        return group;
    }

    // MARK: Kadrolar

    private void BuildRoster()
    {
        Clear(_rosterPage);
        foreach (var team in _session.Project.Match.Teams)
        {
            var group = Adw.PreferencesGroup.New();
            group.SetTitle(team.Label.Length > 0 ? team.Label : T("Takım seçilmedi"));
            if (team.RosterSource is { } source) group.SetDescription(T("Kaynak: {0}.wikipedia.org — {1}. Maç günündeki kadroyla karşılaştırıp düzeltin.", source.Lang, source.Page));
            else if (team.Players.Count == 0) group.SetDescription(T("Kadro boş. \"Maç\" adımından takımı seçin ya da oyuncuları elle ekleyin."));
            var actions = HBox(6);
            if (team.Qid is { } qid)
            {
                actions.Append(Button(T("Vikipedi'den yenile"), async () =>
                {
                    try { await _session.SetTeam(team.Key, qid); } catch (Exception e) { Alert(T("Kadro getirilemedi: {0}", e.Message)); }
                }));
            }
            actions.Append(Button(T("Oyuncu ekle"), () => { team.Players.Add(new Player()); _session.Touch(); }));
            group.SetHeaderSuffix(actions);

            foreach (var player in team.Players)
            {
                var row = Adw.PreferencesRow.New();
                row.SetActivatable(false);
                var line = HBox(8).Margins(6);
                Gtk.Entry Field(string placeholder, string value, int width, Action<string> changed)
                {
                    var entry = Gtk.Entry.New();
                    entry.SetPlaceholderText(placeholder);
                    entry.SetText(value);
                    if (width > 0) entry.SetWidthChars(width); else entry.SetHexpand(true);
                    OnText(entry, v => { changed(v); _session.Save(); });
                    return entry;
                }
                var number = Field(T("No"), player.Number, 3, v => player.Number = v.Trim());
                number.SetMaxWidthChars(3);
                line.Append(number);
                line.Append(Field(T("Ad Soyad"), player.Name, 0, v => player.Name = v));
                line.Append(Field(T("Commons kategorisi"), player.Category ?? "", 0, v => player.Category = v.Length > 0 ? v : null));
                var remove = Gtk.Button.NewFromIconName("list-remove-symbolic");
                remove.AddCssClass("flat");
                remove.SetTooltipText(T("Kaldır"));
                remove.OnClicked += (_, _) => { team.Players.Remove(player); _session.Touch(); };
                line.Append(remove);
                row.SetChild(line);
                group.Add(row);
            }
            _rosterPage.Append(group);
        }
    }
}
