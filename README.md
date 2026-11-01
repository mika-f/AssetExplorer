# Petrichor for Unity

The official Unity editor extension for [Petrichor](https://petrichor.natsuneko.com), the database that maps Unity asset GUIDs and paths to the packages and versions that contain them.
It finds out which package a Missing reference came from, and lets you contribute the packages you own so others can do the same.

This repository was previously AssetExplorer, the client for the retired AssetDatabase (`assetdatabase.natsuneko.cat`). It now talks to the Petrichor v2 API (`https://api.natsuneko.com/petrichor/v2`).

Requires Unity 2022.3 or later. No other packages are needed.

## Install

Download the `.unitypackage` from the [contribute page](https://petrichor.natsuneko.com/contribute) and import it into your project, then open **Window > Natsuneko Laboratory > Petrichor**.

## Features

Everything lives in one window with five tabs. Each tab can also be opened directly from **Window > Natsuneko Laboratory > Petrichor**.

### 検索 (Lookup)

Enter a GUID, or drop an asset, to see every known path for it and the releases (package and version) that contain it.
Releases are marked **検証済み** (verified: two or more people submitted the same content), **Legacy** (migrated from AssetDatabase), or **未検証** (unverified, listed last).
Package names and versions link to their pages on the website.

### Missing

Collects Missing references and looks up the packages that would resolve them.

- Pick a prefab, scene, material or any other asset, or a GameObject in an open scene, and press **調べる**.
- Or scan **開いているシーン** (all open scenes) or a whole folder (**フォルダ…**).
- Right-click shortcuts: **Assets > Petrichor > Find Missing References** and **GameObject > Petrichor > Find Missing References**.

Results show the packages to install first ("このパッケージを入れると解決します" with how many references each one resolves), then each GUID with where it was referenced from, then the GUIDs nobody has registered yet.
Text-serialized assets are read as YAML, so Missing Scripts are found as well.

### 登録 (Contribute)

Registers the GUIDs and paths of a package you own. When two different people register the same content for the same package, the release becomes verified.

1. Choose the source: a folder under `Assets/`, a package under `Packages/`, the current selection, or a `.unitypackage` file (read without importing it). **Assets > Petrichor > Contribute This Folder** does the same from the Project window.
2. Check the preview. The manifest ID shown there is the same one the website shows, so you can compare.
3. Fill in the package. A `package.json` at the root prefills the UPM/VPM name, display name, version and description; a `.unitypackage` file name such as `Foo_1.2.3.unitypackage` prefills the name and version. The identifier can be a UPM/VPM name, a BOOTH item URL, any URL, an existing Petrichor package ID, or none (name only).
4. Either **登録する** (submit directly; requires login) or **JSON を保存** and upload the file on the [contribute page](https://petrichor.natsuneko.com/contribute).

Only folders are left out of the manifest; every file under the source is included with its GUID, project-relative path, and importer type.
Do not include assets you are not allowed to redistribute information about.

### 自分の登録 (My Submissions)

Lists what you have submitted, with each release's status. Requires login.

### 設定 (Settings)

Log in and out, and override the API, website and authorization URLs or the client ID for testing against staging (under **詳細設定**).

## Login

Direct submission and My Submissions use your natsuneko.com account through OAuth 2.0 (authorization code with PKCE):

1. **ログイン** opens the authorization page in your browser.
2. After you allow access, the browser is redirected to `http://127.0.0.1:57193/callback`, which the extension listens on while it waits (up to five minutes).
3. The tokens are kept in `EditorPrefs`, so the login is shared by every project on the machine. Access tokens are refreshed automatically before they expire.

Logging out removes the tokens from this machine. To revoke the extension's access entirely, use "連携中のアプリ" in your natsuneko.com account settings.

If port 57193 is already in use (for example by another Unity editor waiting for a login), close it and try again.

## Privacy

Petrichor receives only asset GUIDs (for lookups) and, when you contribute, GUIDs, project-relative paths, importer types, and the package information you enter. File contents are never sent.

## For maintainers

### Registering the OAuth application

Direct submission needs an Egeria application registered in the natsuneko.com developer settings:

- Kind: a normal application (not a client-metadata / CIMD client; tokens for those are bound to a resource and rejected by the REST API).
- Redirect URI: `http://127.0.0.1:57193/callback`, exactly.
- Name: shown on the consent screen, e.g. "Petrichor for Unity".

Put the issued client ID in `PetrichorSettings.DefaultClientId` (`Assets/NatsunekoLaboratory/AssetExplorer/Integration/PetrichorSettings.cs`) before building the `.unitypackage`.
The extension is a public client: it never sends the client secret, so do not ship it. Until the default is set, a client ID can be entered under **設定 > 詳細設定**.

### Layout

- `Assets/NatsunekoLaboratory/AssetExplorer/Core/`: code without Unity dependencies (JSON, API client, OAuth/PKCE, manifests, `.meta` and `.unitypackage` reading). The OAuth part is a C# 9 port of the [teyvat SDK](https://github.com/teyvat-project/sdk)'s, which targets .NET 10 and cannot be used from Unity.
- `Integration/`: Unity glue (settings, token storage, Missing reference scanning, manifest sources).
- `UI/`: the window, built with UI Toolkit.
- `Assets/NatsunekoLaboratory/AssetExplorer.Tests/`: EditMode tests (not part of the distributed package).

### Tests

```bash
dotnet test tools/PureTests
```

Compiles `Core/` against .NET Standard 2.1 with C# 9 (as Unity 2022.3 does) and runs the tests under `AssetExplorer.Tests/Core`.

```bash
dotnet test tools/PureTests --filter Category=Live
```

Calls the real API.

```bash
tools/unity-check.sh
```

Copies the project to a scratch directory, compiles it and runs every EditMode test in Unity batch mode (set `UNITY` to the editor binary), then copies back `.meta` files Unity generated for new assets.

## License

MIT by [@6jz](https://twitter.com/6jz)
