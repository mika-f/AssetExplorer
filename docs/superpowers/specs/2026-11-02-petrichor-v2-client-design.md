# Petrichor v2 Unity editor client — design

Date: 2026-11-02
Status: approved in conversation (structure, auth, features, GUI direction); this document is the written spec.

## 1. Goal

Turn this repository's Unity editor extension (`Assets/NatsunekoLaboratory/AssetExplorer/`, Unity 2022.3, UI Toolkit) into the official Petrichor client. It currently talks to the retired `https://assetdatabase.natsuneko.cat/v1` API, which returns 410 for submissions. After this work it uses the Petrichor v2 API at `https://api.natsuneko.com/petrichor/v2` and supports:

1. Lookup by GUID
2. Find what's missing (Missing references in an object, prefab, scene, open scene or folder)
3. Contribute (build a manifest, then submit directly with OAuth or save the JSON for the website)
4. My submissions
5. Settings and account (login/logout, endpoint overrides for staging)

Success means every feature works against the real API (lookups now; submission once the Egeria app is registered), the code compiles in Unity 2022.3, and the README describes Petrichor, the features and login.

### Decisions made with the user

| Topic | Decision |
|---|---|
| Auth | OAuth 2.0 Authorization Code + PKCE (S256) in the browser, fixed loopback redirect |
| JSON | No package dependency: a small JSON reader/writer ships inside the extension (distribution stays `.unitypackage`) |
| Folders in manifests | Excluded. Only files are listed, so a folder scan and a `.unitypackage` of the same content produce the same manifest hash |
| GUI | One `Petrichor` window with tabs; menu items open it on a given tab |

### Out of scope

- Changes to the teyvat backend or the website.
- Registering the Egeria application (a manual admin step; see §6.5).
- Reworking the teyvat C# SDK to support Unity (proposed as a separate follow-up).

## 2. API contract (summary)

Source of truth: teyvat `develop` (`packages/foreign-model/src/petrichor.ts`, `packages/api.natsuneko.com/src/app/petrichor/v2/**`).

| Call | Auth | Notes |
|---|---|---|
| `GET /petrichor/v2/assets/{guid}` | public | 200 `{ guid, paths: [{ path, type, releases: ReleaseSummary[] }] }`, 404 when unknown |
| `POST /petrichor/v2/assets/lookup` `{ guids }` | public | ≤ 1,000 GUIDs after dedupe. 200 `{ results: { [guid]: AssetLookup }, missing: string[], suggestions: [{ release, matched }] }` (suggestions: top 20, sorted by trust rank, then matched desc) |
| `POST /petrichor/v2/submissions` | Bearer | body below. 201 new / 200 resubmit `{ releaseId, manifestId, verified, submitterCount }`; 400 / 401 / 404 / 429 |
| `GET /petrichor/v2/me/submissions` | Bearer | bare array of `{ id, version, source: LEGACY|WEB|API, createdAt, release: ReleaseSummary }` |
| `GET /egeria/v1/me` | Bearer | `{ user: { screenName, displayName, … } }`, used for the account chip |

`ReleaseSummary = { id, version, status: verified|legacy|unverified, submitterCount, assetCount, manifestId, createdAt, package: { id, name } }`.

Submission body:

```json
{ "format": "petrichor/v1",
  "package": { "id": "…" }
           | { "identifier": { "provider": "package" | "booth" | "url", "value": "…" }, "name"?: "…", "description"?: "…" }
           | { "name": "…", "description"?: "…" },
  "version": "…",
  "assets": [ { "guid": "…", "path": "…", "type"?: "…" } ] }
```

Limits: version 1–64 chars; 1–30,000 assets; path 1–1,024 chars; package name ≤ 191; identifier value ≤ 512; description ≤ 10,000; type ≤ 191. GUIDs are lowercase 32-hex. Paths use `/`, contain no control characters (U+0000–U+001F, U+007F) and no lone surrogates. A GUID must not appear with two different paths.

Error bodies are `{ error: { message, code } }` or `{ message }`. Verified means 2+ distinct submitters (`VERIFIED_SUBMITTER_COUNT = 2`). Daily limit: 100 submissions per user (UTC day).

Manifest ID: sha256 hex of the concatenation of `${guid}\t${path}\n`, entries sorted by UTF-8 bytes of guid, then of path.

## 3. Authentication

### 3.1 Server facts that shape the client

- Authorization page: `https://accounts.natsuneko.com/authorize?response_type=code&client_id&redirect_uri&state&code_challenge&code_challenge_method=S256`. After consent it redirects to `redirect_uri?code=…&state=…&iss=…`.
- Token endpoint: `POST https://api.natsuneko.com/token`, `application/x-www-form-urlencoded`.
  - `grant_type=authorization_code&code&redirect_uri&client_id&code_verifier`
  - `grant_type=refresh_token&refresh_token&client_id`
  - Response: `{ access_token, refresh_token, token_type, scope, expires_at, expires_in }`. Access tokens live 7 days; every refresh rotates both tokens.
- `redirect_uri` must exactly match a registered URI.
- **The app must be a normally registered Egeria application, not a CIMD (client-metadata-URL) client.** CIMD clients must send `resource`, and resource-bound tokens are stored under `egeria:resource_access_token:*`, which `UserService.getCurrentUser` never reads, so they would be rejected by `/petrichor/v2/submissions`. A registered app that authorizes with PKCE may exchange and refresh **without** `client_secret`, and its tokens are stored under `egeria:access_token:<hash>`, which is what the REST API accepts.

### 3.2 Why not the teyvat C# SDK directly

`teyvat-project/sdk` `packages/dotnet` (CatalystSharp) targets `net10.0` only and uses System.Text.Json source generation, `SocketsHttpHandler`, `RandomNumberGenerator.GetBytes(int)`, `SHA256.HashData`, `char.IsAsciiLetterOrDigit`, records and collection expressions. None of these are available in Unity 2022.3 (.NET Standard 2.1, C# 9). It also contains no Petrichor endpoints (~800 generated Catalyst models), and its `OAuthClient` requires a non-null `clientSecret` and always sends it on refresh.

So the extension contains a C# 9 port of the SDK's OAuth surface with the same shape and names (`Pkce.Create()`, `GetAuthorizeUrl`, `GetAuthorizationCode`, `GetAccessTokenByCodeAsync`, `GetAccessTokenByRefreshTokenAsync`, `OAuthException` + `OAuthErrorKind`). It differs in two ways: no client secret is sent, and the `iss` callback parameter is checked against the authorize URL's origin when present (RFC 9207). If the SDK gains a `netstandard2.1` target later, the port can be replaced by it.

### 3.3 Client flow

1. Settings → **ログイン**: generate PKCE + random `state`, start the loopback receiver, open the authorize URL with `Application.OpenURL`.
2. `LoopbackReceiver` (`HttpListener`, prefix `http://127.0.0.1:57193/callback/`) waits for one `GET /callback` request, responds with a small HTML page ("ログインしました。Unity に戻ってください。" or the error), then stops. It times out after 5 minutes and can be cancelled from the UI. If the port is in use, it reports a clear error.
3. `GetAuthorizationCode` validates the redirect URI prefix, `state`, `error` and `iss`. Then the code is exchanged at `/token`.
4. Tokens are saved, `/egeria/v1/me` is fetched for the account chip, and the window updates.

Redirect URI: **`http://127.0.0.1:57193/callback`** (port 57193, fixed).

### 3.4 Token storage and refresh

- `TokenStore` keeps `{ accessToken, refreshToken, expiresAt }` as JSON in `EditorPrefs` under `NatsunekoLaboratory.Petrichor.Token:<apiBaseUrl>`. That is per machine (shared by all projects) and per API base URL (staging tokens never mix with production ones). The account chip's screen name is cached alongside.
- `AuthSession.GetAccessTokenAsync()` returns a token, refreshing first when `expiresAt` is unknown or less than 1 day away. Concurrent callers share a single refresh task.
- Authenticated calls go through `AuthSession.SendAsync`. On a 401 it refreshes once and replays the request. If the refresh fails with an OAuth error, the stored tokens are cleared and the UI shows "ログインし直してください". Network errors keep the tokens.
- Logout deletes the local tokens. There is no revocation endpoint; the UI points to "連携中のアプリ" in the accounts settings.

## 4. Architecture

All code stays in `Assets/NatsunekoLaboratory/AssetExplorer/`, namespace `NatsunekoLaboratory.AssetExplorer.*`, in the existing Editor-only asmdef. The UStyled reference (`GUID:a8e25f4eb2ce45342b4a847108acb2d9`, a gitignored external package not present in the repo) is removed. The old `Database/`, `Models/`, `Editors/`, `Extensions/` code and the UXML/USS assets are deleted.

```
AssetExplorer/
  NatsunekoLaboratory.AssetExplorer.asmdef
  Json/        JsonReader.cs (string → object tree), JsonWriter.cs (object tree → string), JsonNode helpers
  Api/         PetrichorClient.cs, PetrichorApiException.cs, Models.cs (POCOs + FromJson), Endpoints.cs
  Auth/        Pkce.cs, OAuthClient.cs, OAuthException.cs, LoopbackReceiver.cs, TokenStore.cs, AuthSession.cs
  Settings/    PetrichorSettings.cs (EditorPrefs-backed, defaults, reset)
  Scanning/    MissingReferenceScanner.cs, YamlGuidExtractor.cs, MissingReference.cs
  Contribute/  MetaFile.cs, ManifestSources.cs (folder / package / selection), UnityPackageReader.cs (tar.gz),
               Manifest.cs, ManifestValidator.cs, ManifestHash.cs, PackageInfoDetector.cs, PackageIdentifier.cs
  UI/          PetrichorWindow.cs, Tabs/{Lookup,Missing,Contribute,Submissions,Settings}Tab.cs,
               Components/{StatusBadge,ReleaseRow,StateView,GuidLabel,Section}.cs, Petrichor.uss, MenuItems.cs
AssetExplorer.Tests/   (outside the distributed folder)
  NatsunekoLaboratory.AssetExplorer.Tests.asmdef (Editor, test assembly)
  *Tests.cs
```

Units without Unity dependencies (Json, ManifestHash, ManifestValidator, MetaFile, UnityPackageReader, PackageIdentifier, Pkce, OAuthClient URL/callback handling, lookup batching/merging) live in plain classes, so they can be compiled and tested outside Unity too.

### 4.1 JSON

`JsonReader.Parse(string)` returns `Dictionary<string, object>` / `List<object>` / `string` / `double` / `long` / `bool` / `null`. It is strict (rejects trailing garbage) and handles `\uXXXX` including surrogate pairs. `JsonWriter.Write(object)` serializes the same shapes (dictionaries with string keys, lists, strings, numbers, booleans, null); request bodies are built as dictionaries in code. It escapes `"`, `\` and control characters, and leaves non-ASCII as UTF-8. Models expose `static T FromJson(object)` with tolerant field access (a missing optional field becomes null).

### 4.2 HTTP

`PetrichorClient` wraps one shared `HttpClient` (timeout 100 s, for bodies of up to 30k assets) and sets `User-Agent: Petrichor-Unity/<version> (Unity <Application.unityVersion>)`. It is constructed from `PetrichorSettings` (API base URL). Methods take a `CancellationToken`:

- `Task<AssetLookup?> GetAssetAsync(string guid)`: 404 → null.
- `Task<BulkLookup> LookupAsync(IReadOnlyCollection<string> guids, IProgress<(int done,int total)>)`: lowercases and dedupes, splits into batches of 1,000, sends them sequentially, and merges. `results` and `missing` are unions. Suggestions are merged by `release.id` with `matched` summed, then sorted by trust rank (verified, legacy, unverified), then matched desc, then version, and cut to 20.
- `Task<SubmissionResult> SubmitAsync(Manifest)`: via `AuthSession`.
- `Task<IReadOnlyList<Submission>> GetMySubmissionsAsync()`: via `AuthSession`.
- `Task<User> GetMeAsync()`: via `AuthSession`.

Non-2xx responses throw `PetrichorApiException(status, message, code)`. The message comes from `error.message` or `message`, falling back to the HTTP reason. UI code calls these from the main thread and `await`s without `ConfigureAwait(false)`, so continuations come back on Unity's main thread. Each tab owns a `CancellationTokenSource` that is cancelled on a new request or when the window closes, and results are ignored once the panel is detached.

## 5. Features

### 5.1 Lookup tab (検索)

- Input: a GUID text field (accepts any case and surrounding whitespace; validated as 32-hex) and an Object field. Dropping an asset fills its GUID, which answers "which package is this file from?".
- Result: GUID header (monospace, selectable); for each path, its `path` and `type`, then its releases as `ReleaseRow`s (package name, version, `StatusBadge`, submitter count). Releases sort verified → legacy → unverified, then by version desc. Package name opens `{web}/packages/{package.id}`; version opens `{web}/releases/{id}`.
- States: idle hint, loading, not found (with "登録タブで登録できます" link), error with retry.

### 5.2 Missing tab

Sources (one at a time):
- **Object field**: an asset (prefab, scene asset, material, any text-serialized asset) or a scene GameObject.
- **開いているシーン**: all loaded scenes.
- **フォルダ…**: a project folder (all text-serialized assets inside, recursively).

Collection (`MissingReferenceScanner`):
- **Assets on disk**: if the file starts with `%YAML`, extract every `guid: ([0-9a-fA-F]{32})` (`YamlGuidExtractor`). A GUID counts as missing when `AssetDatabase.GUIDToAssetPath` is empty or the file no longer exists. Built-in GUIDs (resolving to `Resources/unity_builtin_extra` / `Library/unity default resources`) are never missing. The location recorded is the asset path plus the YAML key on that line (`m_Script`, `m_Materials`, …). This also finds Missing Scripts.
- **Scene objects** (Object field on a GameObject, or the open scene): walk every component of the GameObject hierarchy with `SerializedObject`. For each `ObjectReference` property with `objectReferenceValue == null && objectReferenceInstanceIDValue != 0`, resolve the GUID via `AssetDatabase.TryGetGUIDAndLocalFileIdentifier`. The location is the hierarchy path, the component type and the property display name. For the open scene, saved scene files are also YAML-scanned so Missing Scripts are included.
- GUIDs are deduped. Each keeps the list of places it was referenced from.

Lookup: `PetrichorClient.LookupAsync`, with batch progress shown inline ("照合 2/3"). Results:
1. **Suggestions** ("このパッケージを入れると解決します"): release rows with "N 件解決" and links.
2. **Per-GUID results**: foldouts showing the GUID, where it was referenced, and paths and releases as in Lookup.
3. **Not found** ("見つからなかった GUID"): GUIDs with their locations, muted, with a copy-all button.

Empty state when nothing is missing: "Missing 参照はありません".

### 5.3 Contribute tab (登録)

Step 1, source:
- **フォルダ**: an `Assets/…` folder.
- **Packages/**: a dropdown of packages in the project (from `PackageManager.PackageInfo.GetAllRegisteredPackages()`); the physical root is `resolvedPath`, and paths are emitted as `Packages/<name>/…`.
- **選択中**: `Selection.assetGUIDs`; folders expand recursively, files are taken as-is.
- **.unitypackage**: `EditorUtility.OpenFilePanel`, read without importing.

Step 2, building the manifest. For each non-folder asset:
- `MetaFile.Parse(text)` reads the `.meta`: `guid:` and the first top-level key ending in `Importer:` as `type` (e.g. `TextureImporter`). `folderAsset: yes` marks folders, which are skipped.
- For folder, package and selection sources, `.meta` files are enumerated on disk (`*.meta`, recursive). An entry is kept only when its sibling asset file exists. Its path is project-relative with `/`.
- For `.unitypackage`, `UnityPackageReader` streams the gzip+tar: it handles ustar `prefix`, GNU `L` long names and PAX `path`, and collects `<guid>/pathname` (first line, `\r` trimmed) and `<guid>/asset.meta`. Large `asset` bodies are skipped without buffering. Entries without an `asset` body or with `folderAsset: yes` are folders and are skipped. It runs on a worker thread with cancel and progress.
- The result is sorted by path for the preview. The manifest ID is computed locally (`ManifestHash`) and shown, with a copy button.
- `ManifestValidator` checks the §2 rules and lists problems (first few, then "他 N 件"). Submitting and saving are blocked while any problem remains.

Step 3, package info:
- `PackageInfoDetector` prefills fields from `package.json` at the source root (folder/package), or from the shallowest `package.json` entry inside a `.unitypackage` (read in a second pass only when present): `name` becomes the identifier `{ provider: "package", value: name }`, `displayName` the name, plus `version` and `description`. A `.unitypackage` without `package.json` gets its name and version guessed from the file name (`Foo_1.2.3.unitypackage` → `Foo`, `1.2.3`).
- Identifier selector: **UPM/VPM 名** (`package`), **BOOTH URL** (`booth.pm/…/items/<id>` → `booth:<id>`, the same rules as `identifierFromUrl`; a non-BOOTH URL in this field is rejected inline), **URL** (`url`), **Petrichor パッケージ ID** (`{ id }`), **なし** (name only, name required).
- Name (≤ 191), description (≤ 10,000, optional), version (1–64, required).
- A short privacy note: only GUIDs, paths and types are sent; contents are never sent. Do not include private assets.

Step 4, send:
- **登録する** (requires login; otherwise the button says "ログインして登録" and jumps to Settings): `SubmitAsync`. On success it shows "登録しました" (201) or "登録済みの内容を再送しました" (200), plus "検証済みになりました" or "あと N 人の登録で検証済みになります" (N = max(0, 2 − submitterCount)), a release link, and the manifest ID. Errors: 401 → "ログインが必要です", 404 → "指定したパッケージが見つかりません", 429 → "本日の投稿上限 (100 件) に達しました", otherwise the server message.
- **JSON を保存**: `EditorUtility.SaveFilePanel` (default `<name>-<version>.petrichor.json`) writes the exact submission body, then offers to open `{web}/contribute`.

### 5.4 My submissions tab (自分の登録)

When logged out it shows a login prompt. When logged in, `GetMySubmissionsAsync` lists rows: package name, version (the submission's label), release status badge, source (API/WEB/LEGACY), local date-time, and links to the package and release. A refresh button reloads the list, and it loads automatically on first view of the tab. States: loading, empty ("まだ登録していません"), error with retry.

### 5.5 Settings tab (設定)

- Account: logged-out (ログイン button; disabled with "Client ID 未設定" while the client ID is empty), waiting for the browser (cancel button, 5-minute timeout), logged in (display name and `@screenName`, ログアウト).
- Endpoints (foldout "詳細設定"): API base URL (default `https://api.natsuneko.com`), website URL (`https://petrichor.natsuneko.com`), authorize URL (`https://accounts.natsuneko.com/authorize`), Client ID (default `PetrichorSettings.DefaultClientId`, empty until the app is registered), redirect URI (read-only display). Each has a reset button. Changes take effect immediately (a new client is built per request).

## 6. GUI

### 6.1 Window

`PetrichorWindow : EditorWindow`, title "Petrichor", minimum size 420×360. A top bar holds the product name, a segmented tab strip (検索 / Missing / 登録 / 自分の登録 / 設定) and an account chip on the right (screen name or "ログイン"). The selected tab is persisted in `[SerializeField]`. The UI is built in C# with one `Petrichor.uss` loaded by GUID. `PetrichorWindow.Open(Tab)` focuses or creates the window and switches tabs.

### 6.2 Style

- Colors only through Unity editor theme variables (`--unity-colors-window-background`, `-default-text`, `-label-text` / muted, `-highlight-background`, `-default-border`, `-helpbox-background`, `-error-text`, `-warning-text`, …), so the light and dark skins both work. Badge colors use a small set of semi-transparent tints that read on both skins (verified green, legacy blue, unverified neutral/muted).
- GUIDs and paths use the editor's monospace font (`EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf")`, falling back to the default font when missing). GUIDs are selectable (read-only `TextField` styled as a label) and have a copy button.
- Spacing on a 4 px scale; sections with a small heading; bordered rows rather than cards.
- `StateView` provides consistent idle / loading (inline spinner + text) / empty / error (message + 再試行) states. No `EditorUtility.DisplayProgressBar` for network calls.
- Contribute uses two columns when the window is ≥ 760 px wide, otherwise one stacked column (`GeometryChangedEvent`).

### 6.3 Menus

- `Window/Natsuneko Laboratory/Petrichor/Petrichor` (Lookup), `…/Find Missing References`, `…/Contribute`, `…/My Submissions`, `…/Settings`.
- `Assets/Petrichor/Find Missing References` (selected asset or folder), `Assets/Petrichor/Contribute This Folder` (validated: a folder is selected).
- `GameObject/Petrichor/Find Missing References` (selected scene object).

### 6.4 Text

UI text is Japanese, matching the website's wording (検証済み / Legacy / 未検証, あと N 人, …). Code, comments, README and commits are English.

### 6.5 Manual step for the user

Register an Egeria application in the accounts developer settings:
- Kind: normal application (not a client-metadata/CIMD client).
- Redirect URI: `http://127.0.0.1:57193/callback` (exactly).
- Name: Petrichor for Unity (shown on the consent screen).

Then set `PetrichorSettings.DefaultClientId` to the issued client ID before release. Until then, anyone can paste a client ID in Settings → 詳細設定. The client secret is not needed and must not be shipped.

## 7. Error handling

| Situation | Behavior |
|---|---|
| Network failure / timeout | Inline error with 再試行; nothing is logged as an exception except unexpected ones (`Debug.LogException`) |
| 4xx/5xx | Server message shown; 401 handled by `AuthSession` as in §3.4; 429 has its own text |
| Lookup batch fails mid-way | Stops, shows the error and keeps the batches that already finished visible with "一部のみ" |
| OAuth callback with `error` | Message shows `error` / `error_description` |
| Port 57193 in use | "ポート 57193 が使用中です。他の Unity を閉じてからやり直してください" |
| Window closed during a request | Request cancelled; continuation does nothing |
| Invalid manifest | Problems listed; send/save disabled |
| `.unitypackage` unreadable | "unitypackage を読み込めませんでした" + reason |

## 8. Testing and verification

- **EditMode tests** (`Assets/NatsunekoLaboratory/AssetExplorer.Tests`, Unity Test Framework): JSON round-trips and escapes; `ManifestHash` against vectors computed by the teyvat TS `computeManifestId` (including a non-ASCII and a supplementary-plane path, where UTF-8 and UTF-16 order differ); `ManifestValidator` rules; `MetaFile` parsing (file, folder, no importer); `UnityPackageReader` on a tar.gz generated in the test (normal, long name, folder entry); BOOTH/URL identifier parsing; lookup merge and batching; PKCE (RFC 7636 appendix B vector); authorize URL and callback validation (state mismatch, `error`, `iss` mismatch); `YamlGuidExtractor`.
- **Outside Unity**: the Unity-independent sources are compiled with the local .NET SDK (C# 9, netstandard2.1 surface) in a scratch harness. The same tests run there, plus live calls to the real lookup endpoints.
- **Compile check in Unity**: Unity 2022.3 is not installed locally (6000.6.7f1 is). A scratch copy of the project is opened with Unity 6000.6 in batch mode to compile and run the EditMode tests. Unity 6-only APIs are avoided so 2022.3 also compiles; the user confirms in 2022.3.
- **Manual click-through for the user** (listed in the README/PR): each tab's states, light/dark skin, login once the app is registered, a direct submission, JSON save + upload on the website.

## 9. Delivery

- Commits on branch `claude/petrichor-v2-e62b86`, English AngularJS-style messages, with the co-author trailer.
- README rewritten for Petrichor (features, login, the manual registration step, privacy).
- Developed on the default branch at the user's request: the worktree branch is fast-forward pushed to `origin/main` (github.com/mika-f/AssetExplorer); no PR.
