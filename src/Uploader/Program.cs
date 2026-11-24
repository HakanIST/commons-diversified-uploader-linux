using Uploader;
using UploaderCore;

Http.UserAgent = "CommonsDiversifiedUploader/0.1 (Linux; native app)";
if (Environment.GetEnvironmentVariable("CDU_LANG") is { Length: > 0 } language) L.Language = language;
Strings.Register();

var app = Adw.Application.New("tr.tall.CommonsDiversifiedUploader", Gio.ApplicationFlags.HandlesOpen);
MainWindow? window = null;

MainWindow Show()
{
    window ??= new MainWindow(app);
    window.Present();
    return window;
}

app.OnActivate += (_, _) => Show();
// Tarayıcı, oturum onayından sonra uygulamayı özel şemalı adresle açar; çalışan örneğe iletilir.
app.OnOpen += (_, e) =>
{
    var target = Show();
    foreach (var file in e.Files)
        if (file.GetUri() is { } uri && uri.StartsWith(OAuth.CallbackScheme + ":", StringComparison.Ordinal)) target.CompleteLogin(uri);
};
return app.RunWithSynchronizationContext(args);
