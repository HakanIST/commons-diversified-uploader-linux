# Commons Diversified Uploader (Linux)

A native Linux app (GTK 4 / libadwaita) for sports photographers who
contribute to [Wikimedia Commons](https://commons.wikimedia.org/). It reads
jersey numbers and jersey colours from match photos, maps them to players
through the team rosters on Wikipedia/Wikidata, proposes the matching Commons
categories and uploads the reviewed photos.

This is a community project. It is not affiliated with or endorsed by the
Wikimedia Foundation. The pipeline lives in the shared
`commons-diversified-uploader-core` library (git submodule `core`); the macOS
edition is in `commons-diversified-uploader-macos`.

## Installing

Ubuntu 24.04 or newer (or another distribution with GTK 4.12 and
libadwaita 1.5):

```sh
sudo apt install ./commons-diversified-uploader_0.1.0_amd64.deb
```

The package is built by CI for `amd64` and `arm64`. On first analysis the
app downloads the recognition model (about 1 GB) from Hugging Face.

## Building

Requirements: .NET 10 SDK; GTK 4 and libadwaita at run time.

```sh
git clone --recurse-submodules https://github.com/HakanIST/commons-diversified-uploader-linux.git
cd commons-diversified-uploader-linux
dotnet run --project src/Uploader
packaging/build-deb.sh amd64      # needs dpkg-deb
```

Development switches (environment): `CDU_FOLDER` opens a photo folder,
`CDU_STEP` selects `match`, `roster`, `photos` or `upload`, `CDU_LANG`
forces `tr` or `en`, `CDU_MODEL_DIR` points to an already downloaded model.

## Logging in

"Log in with Commons" opens the Wikimedia authorization page in the default
browser (OAuth 2.0 with PKCE). The browser returns to the app through the
`tr.tall.commonsdiversifieduploader` URL scheme registered by the desktop
entry; the tokens are kept in the desktop keyring (libsecret).

## Localisation

Turkish is the source language; English translations live in
`src/Uploader/Strings.cs` and in the core library.

## Privacy

The app talks only to Wikimedia sites and, once, to Hugging Face to download
the model. Photos are analysed on the device, and the telemetry of the
bundled ONNX Runtime is switched off.

## Contributing

Commit messages follow the
[Wikimedia commit message guidelines](https://www.mediawiki.org/wiki/Gerrit/Commit_message_guidelines).

## License

[Apache License 2.0](LICENSE)
