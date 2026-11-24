using System.Diagnostics;
using UploaderCore;
using static Uploader.Ui;

namespace Uploader;

internal sealed partial class MainWindow
{
    private OAuthRequest? _pendingLogin;
    private Adw.AlertDialog? _loginDialog;

    private void BuildUpload()
    {
        Clear(_uploadPage);
        var rows = _session.Rows();
        var ready = Session.Ready(rows);
        var failed = rows.Where(r => r.UploadError != null).ToList();

        var account = Adw.PreferencesGroup.New();
        account.SetTitle(T("Commons oturumu"));
        if (_session.Username is { } user)
        {
            var row = Adw.ActionRow.New();
            row.SetTitle(GLib.Functions.MarkupEscapeText(T("Commons oturumu: {0}", user), -1));
            row.AddPrefix(Gtk.Image.NewFromIconName("avatar-default-symbolic"));
            var logout = Button(T("Oturumu kapat"), _session.Logout);
            logout.SetValign(Gtk.Align.Center);
            row.AddSuffix(logout);
            account.Add(row);
        }
        else
        {
            account.SetDescription(T("Wikimedia hesabınızla oturum açın. Parolanız uygulamaya verilmez; yalnızca dosya yükleme ve sayfa düzenleme izni istenir."));
            var login = Button(T("Commons ile giriş yap"), StartLogin, "suggested-action", "pill");
            login.SetHalign(Gtk.Align.Start);
            account.Add(login);
        }
        _uploadPage.Append(account);

        var stats = HBox(36);
        void Stat(int value, string label, string css)
        {
            var box = VBox(0);
            var number = Label(value.ToString(), "stat", css);
            box.Append(number);
            box.Append(Label(T(label), "caption", "dim-label"));
            stats.Append(box);
        }
        Stat(ready.Count, "yüklenecek", "accent");
        Stat(rows.Count(r => r.Uploaded != null), "yüklendi", "success");
        Stat(failed.Count, "hatalı", "error");
        Stat(rows.Count(r => !r.Include), "hariç tutuldu", "dim-label");
        _uploadPage.Append(stats);

        var actions = HBox(8);
        actions.Append(Button(T("Kategorileri ve dosya adlarını denetle"), async () =>
        {
            try { _check = await _session.Check(); BuildUpload(); }
            catch (Exception e) { Alert(T("Denetim yapılamadı: {0}", e.Message)); }
        }));
        var upload = Button(T("{0} fotoğrafı yükle", ready.Count), () => ConfirmUpload(ready.Count), "suggested-action");
        upload.SetSensitive(_session.LoggedIn && ready.Count > 0 && !_busy);
        upload.SetHalign(Gtk.Align.End);
        upload.SetHexpand(true);
        actions.Append(upload);
        _uploadPage.Append(actions);

        if (_check is { } check)
        {
            var box = VBox(4);
            if (check.MissingCategories.Count == 0) box.Append(Label(T("Tüm kategoriler Commons'ta mevcut."), "success"));
            else
            {
                box.Append(Label(T("Commons'ta bulunmayan kategoriler ({0}):", check.MissingCategories.Count), "warning"));
                foreach (var c in check.MissingCategories) box.Append(Label("• " + c));
            }
            if (check.TakenTitles.Count == 0) box.Append(Label(T("Dosya adları kullanılabilir."), "success"));
            else
            {
                box.Append(Label(T("Bu adlarla dosya zaten var ({0}):", check.TakenTitles.Count), "error"));
                foreach (var t in check.TakenTitles.Take(20)) box.Append(Label("• " + t));
            }
            _uploadPage.Append(box);
        }

        if (ready.FirstOrDefault() is { } first)
        {
            var sample = Adw.PreferencesGroup.New();
            sample.SetTitle(GLib.Functions.MarkupEscapeText(T("Örnek sayfa metni: {0}", first.Title), -1));
            var text = Label(_session.Wikitext(first), "monospace");
            text.SetSelectable(true);
            var frame = Adw.PreferencesRow.New();
            frame.SetActivatable(false);
            frame.SetChild(text.Margins(12));
            sample.Add(frame);
            _uploadPage.Append(sample);
        }

        if (failed.Count > 0)
        {
            var results = VBox(4);
            results.Append(Label(T("Sonuçlar"), "heading"));
            foreach (var r in failed) results.Append(Label($"{r.Name}: {r.UploadError}", "error"));
            _uploadPage.Append(results);
        }
    }

    private void ConfirmUpload(int count)
    {
        var dialog = Adw.AlertDialog.New(
            T("{0} fotoğraf {1} hesabıyla Wikimedia Commons'a herkese açık olarak yüklenecek.", count, _session.Username),
            T("Yüklenen dosyalar seçtiğiniz özgür lisansla yayımlanır; bu işlem geri alınamaz."));
        dialog.AddResponse("cancel", T("Vazgeç"));
        dialog.AddResponse("upload", T("Yükle"));
        dialog.SetResponseAppearance("upload", Adw.ResponseAppearance.Suggested);
        dialog.SetDefaultResponse("cancel");
        dialog.SetCloseResponse("cancel");
        dialog.OnResponse += (sender, e) =>
        {
            if (e.Response == "upload") _ = Run((p, ct) => _session.Upload(p, warning => Alert(warning), ct), "{0}");
        };
        dialog.Present(_window);
    }

    // MARK: Oturum

    /// <summary>Yetkilendirme sayfasını varsayılan tarayıcıda açar; tarayıcı uygulamaya özel şemalı adresle döner.</summary>
    private void StartLogin()
    {
        _pendingLogin = OAuth.MakeRequest();
        try
        {
            var info = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
            info.ArgumentList.Add(_pendingLogin.Url);
            Process.Start(info)?.Dispose();
        }
        catch (Exception e) { Alert(e.Message); return; }

        var dialog = Adw.AlertDialog.New(T("Commons ile giriş yap"), T("Tarayıcıda izin verin; bu pencere kendiliğinden devam eder."));
        var paste = Gtk.Entry.New();
        paste.SetPlaceholderText(OAuth.RedirectUri + "?code=…");
        var extra = VBox(6);
        extra.Append(Label(T("Tarayıcı uygulamaya geri dönmediyse, adres çubuğundaki adresi buraya yapıştırın"), "caption", "dim-label"));
        extra.Append(paste);
        dialog.SetExtraChild(extra);
        dialog.AddResponse("cancel", T("Vazgeç"));
        dialog.AddResponse("continue", T("Devam"));
        dialog.SetCloseResponse("cancel");
        dialog.OnResponse += (_, e) =>
        {
            _loginDialog = null;
            if (e.Response == "continue" && paste.GetText().Trim() is { Length: > 0 } pasted) CompleteLogin(pasted);
            else if (e.Response == "cancel") _pendingLogin = null;
        };
        _loginDialog = dialog;
        dialog.Present(_window);
    }

    /// <summary>Tarayıcıdan dönen adresle oturumu tamamlar.</summary>
    public async void CompleteLogin(string callback)
    {
        if (_pendingLogin is not { } request) return;
        _pendingLogin = null;
        _loginDialog?.ForceClose();
        _loginDialog = null;
        try
        {
            var tokens = await OAuth.Exchange(OAuth.Code(callback, request), request);
            await _session.Login(new CommonsAuth.OAuth(OAuth.ClientId, tokens));
            _window.Present();
        }
        catch (Exception e) { Alert(e.Message); }
    }
}
