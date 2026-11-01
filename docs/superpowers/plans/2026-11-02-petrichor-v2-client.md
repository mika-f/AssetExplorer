# Petrichor v2 Unity Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the dead AssetDatabase v1 editor extension with a Petrichor v2 client: GUID lookup, Missing reference resolution, manifest contribution (OAuth direct submit or JSON export), my submissions, and settings, in one tabbed UI Toolkit window.

**Architecture:** Unity-independent logic (JSON, API models and client, OAuth/PKCE, manifest building/hashing/validation, `.meta` and `.unitypackage` parsing, YAML GUID extraction) lives under `Core/` and is compiled and tested both by Unity and by a plain .NET harness. `Integration/` holds the thin Unity glue (EditorPrefs, AssetDatabase, scene walking). `UI/` builds the window in C# with one USS.

**Tech Stack:** Unity 2022.3 (C# 9, .NET Standard 2.1 API surface), UI Toolkit, `System.Net.Http.HttpClient`, `System.Net.HttpListener`, `System.IO.Compression.GZipStream`, NUnit (Unity Test Framework 1.1.33 / NUnit 3 in the harness), .NET SDK 8 for the harness.

**Spec:** `docs/superpowers/specs/2026-11-02-petrichor-v2-client-design.md`

## Global Constraints

- Unity 2022.3.4f1; C# 9 only (no records, no file-scoped namespaces, no collection expressions, no `required`, no raw strings); APIs must exist in .NET Standard 2.1 (no `RandomNumberGenerator.GetBytes(int)`, no `SHA256.HashData`, no `Convert.ToHexString`, no `string.Contains(char)`-only-on-netcore overloads beyond netstandard2.1).
- Never call `ConfigureAwait(false)` outside `Task.Run` bodies; UI continuations must resume on Unity's main thread.
- No package dependencies; remove asmdef reference `GUID:a8e25f4eb2ce45342b4a847108acb2d9` (UStyled).
- Namespace root `NatsunekoLaboratory.AssetExplorer`; product name in UI is "Petrichor".
- File header: the existing 4-line copyright comment block (`Copyright (c) Natsuneko. All rights reserved.` / MIT) on every new `.cs` file.
- Defaults: API base `https://api.natsuneko.com`, Petrichor path `/petrichor/v2`, web `https://petrichor.natsuneko.com`, authorize `https://accounts.natsuneko.com/authorize`, token `<api base>/token`, redirect URI `http://127.0.0.1:57193/callback`, `DefaultClientId = ""`.
- Limits: lookup batch 1,000 GUIDs; suggestions cut to 20; version 1–64 chars; 1–30,000 assets; path 1–1,024 chars; package name ≤ 191; identifier value ≤ 512; description ≤ 10,000; type ≤ 191; verified needs 2 submitters; daily limit 100.
- UI text Japanese (wording from spec §5); code, comments, commits English; commits AngularJS style ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Folders are never manifest entries.
- New assets need `.meta` files committed. Unity generates them in the check copy (Task 1's script copies them back); the only hand-written meta is `UI/Petrichor.uss.meta` (its GUID is referenced from code).

## Review Focus

1. A `.unitypackage` whose tar entries come in any order (`pathname` after `asset`, `asset.meta` first) and entries with no `asset` body (folders): must still pair correctly and skip folders. → Task 6 tests `Reads_entries_in_any_order` and `Skips_folder_entries`.
2. The window being closed (or the tab switched) while a request is in flight: the continuation must not touch disposed UI or throw. → Task 10 `AsyncView` guard; Task 9 `Cancelled_request_throws_OperationCanceled`.
3. A refresh token that the server rejects (rotated elsewhere, revoked app): the user must end up logged out with a clear message, not stuck retrying. → Task 8 test `Refresh_rejected_clears_tokens_and_throws_AuthRequired`.
4. GUIDs pasted with uppercase letters, whitespace or duplicates across batches: lookups must normalize and dedupe, and >1,000 must batch. → Task 4 tests `Split_normalizes_and_dedupes` and `Split_batches_by_1000`.
5. Paths with non-ASCII characters (Japanese folder names are common): hash must match the server byte-for-byte. → Task 5 test vector B.

---

## File Structure

```
Assets/NatsunekoLaboratory/AssetExplorer/
  NatsunekoLaboratory.AssetExplorer.asmdef        (modify: drop UStyled ref)
  AssemblyInfo.cs                                  InternalsVisibleTo for tests + harness
  Core/Json/JsonReader.cs, JsonWriter.cs, JsonException.cs, JsonAccess.cs
  Core/Api/Models.cs, ReleaseOrdering.cs, VersionComparer.cs, PetrichorApiException.cs, LookupMerger.cs, PetrichorClient.cs
  Core/Auth/Pkce.cs, Token.cs, OAuthException.cs, OAuthClient.cs, LoopbackReceiver.cs, StoredToken.cs, ITokenStorage.cs, AuthSession.cs
  Core/Contribute/ManifestAsset.cs, PackageSpec.cs, Manifest.cs, ManifestHash.cs, ManifestValidator.cs, MetaFile.cs,
                  MetaScanner.cs, UnityPackageReader.cs, PackageIdentifier.cs, PackageInfoDetector.cs
  Core/Scanning/YamlGuidExtractor.cs, MissingReference.cs
  Integration/PetrichorSettings.cs, EditorPrefsTokenStorage.cs, PetrichorServices.cs,
              MissingReferenceScanner.cs, ManifestSources.cs
  UI/PetrichorWindow.cs, PetrichorTab.cs, MenuItems.cs, Petrichor.uss (+ hand-written .meta)
  UI/Components/StatusBadge.cs, ReleaseRow.cs, StateView.cs, GuidLabel.cs, Section.cs, AsyncView.cs, Mono.cs
  UI/Tabs/LookupTab.cs, MissingTab.cs, ContributeTab.cs, SubmissionsTab.cs, SettingsTab.cs
  (delete) Database/, Models/, Editors/, Extensions/, Assets/   (old code, UXML, USS, and their .meta files)
Assets/NatsunekoLaboratory/AssetExplorer.Tests/
  NatsunekoLaboratory.AssetExplorer.Tests.asmdef
  Core/**/*Tests.cs                                pure tests, compiled by Unity and by the harness
tools/PureLib/PureLib.csproj                       netstandard2.1, LangVersion 9, compiles Core/** + AssemblyInfo.cs
tools/PureTests/PureTests.csproj                   net8.0, NUnit, references PureLib, compiles AssetExplorer.Tests/Core/**
tools/unity-check.sh                               copy project to scratch, run EditMode tests in Unity batch mode, copy new .meta back
```

The spec's §4 folder list maps onto `Core/` (pure) and `Integration/` (Unity) as above; behavior is unchanged.

---

### Task 1: Scaffolding, cleanup, and both test harnesses

**Files:**
- Modify: `Assets/NatsunekoLaboratory/AssetExplorer/NatsunekoLaboratory.AssetExplorer.asmdef` (`"references": []`, add `"rootNamespace": "NatsunekoLaboratory.AssetExplorer"`)
- Delete: `Assets/NatsunekoLaboratory/AssetExplorer/{Database,Models,Editors,Extensions,Assets}` and their folder `.meta` files
- Create: `Assets/NatsunekoLaboratory/AssetExplorer/AssemblyInfo.cs`
- Create: `Assets/NatsunekoLaboratory/AssetExplorer.Tests/NatsunekoLaboratory.AssetExplorer.Tests.asmdef`
- Create: `Assets/NatsunekoLaboratory/AssetExplorer.Tests/Core/SmokeTests.cs`
- Create: `tools/PureLib/PureLib.csproj`, `tools/PureTests/PureTests.csproj`, `tools/unity-check.sh`
- Modify: `.gitignore` (add `tools/**/bin/`, `tools/**/obj/`)

**Interfaces:**
- Produces: `[assembly: InternalsVisibleTo("NatsunekoLaboratory.AssetExplorer.Tests")]` and `[assembly: InternalsVisibleTo("PureTests")]`; all later types may be `internal`.
- Produces: `tools/unity-check.sh` usage `tools/unity-check.sh` → exit 0 and prints `RESULT passed=<n> failed=0` when EditMode tests pass.

- [ ] **Step 1:** Delete the old folders and files; set asmdef references to `[]`.
- [ ] **Step 2:** Test asmdef: `"references": ["NatsunekoLaboratory.AssetExplorer","UnityEngine.TestRunner","UnityEditor.TestRunner"]`, `"includePlatforms": ["Editor"]`, `"overrideReferences": true`, `"precompiledReferences": ["nunit.framework.dll"]`, `"autoReferenced": false`, `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`.
- [ ] **Step 3:** `SmokeTests.Harness_runs` asserting `Assert.That(1 + 1, Is.EqualTo(2))` (namespace `NatsunekoLaboratory.AssetExplorer.Tests`).
- [ ] **Step 4:** `PureLib.csproj`: `TargetFramework netstandard2.1`, `LangVersion 9`, `Nullable disable`, `AssemblyName PureLib`, `EnableDefaultCompileItems false`, `<Compile Include="../../Assets/NatsunekoLaboratory/AssetExplorer/Core/**/*.cs;../../Assets/NatsunekoLaboratory/AssetExplorer/AssemblyInfo.cs" />`. Because the assembly name differs, also add `[assembly: InternalsVisibleTo("PureTests")]` (already in AssemblyInfo.cs). `PureTests.csproj`: `net8.0`, `LangVersion 9`, `NUnit 3.14.0`, `NUnit3TestAdapter 4.5.0`, `Microsoft.NET.Test.Sdk 17.10.0`, `ProjectReference ../PureLib/PureLib.csproj`, `Compile Include="../../Assets/NatsunekoLaboratory/AssetExplorer.Tests/Core/**/*.cs"`.
- [ ] **Step 5:** Run `dotnet test tools/PureTests` → expected `Passed: 1`.
- [ ] **Step 6:** `tools/unity-check.sh`: `UNITY=${UNITY:-/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity}`; `DEST=${DEST:-$TMPDIR/petrichor-unity-check}`; `rsync -a --delete --exclude Library --exclude Temp --exclude Logs --exclude .git --exclude tools ./ "$DEST/"` (keep `$DEST/Library` between runs by excluding it from `--delete` via `--filter 'P Library/'`); run `"$UNITY" -batchmode -nographics -projectPath "$DEST" -runTests -testPlatform EditMode -testResults "$DEST/results.xml" -logFile "$DEST/editor.log"`; on compile errors print `grep -E "error CS" "$DEST/editor.log"`; parse `results.xml` `total/passed/failed` and print `RESULT passed=… failed=…`; then copy back every `.meta` under `$DEST/Assets/NatsunekoLaboratory` that does not exist in the repo (`rsync -a --ignore-existing --include '*/' --include '*.meta' --exclude '*'`). Exit non-zero on failure.
- [ ] **Step 7:** Run `tools/unity-check.sh` (background; first import is slow) → `RESULT passed=1 failed=0`. Note: Unity 6000.6 upgrades `ProjectSettings` in the copy only; nothing in the repo changes except new metas.
- [ ] **Step 8:** Commit `chore: drop legacy AssetDatabase client and add test harnesses`.

### Task 2: JSON reader and writer

**Files:**
- Create: `Core/Json/JsonReader.cs`, `JsonWriter.cs`, `JsonException.cs`, `JsonAccess.cs`
- Test: `AssetExplorer.Tests/Core/Json/JsonTests.cs`

**Interfaces:**
- Produces: `static object JsonReader.Parse(string json)`; objects are `Dictionary<string, object>`, arrays `List<object>`, numbers `long` when integral and in range else `double`, plus `string`, `bool`, `null`. Throws `JsonException(string message, int position)`.
- Produces: `static string JsonWriter.Write(object value)` for `IDictionary<string, object>`, `IEnumerable` (non-string), `string`, `bool`, `int`, `long`, `double`, `float`, `null`; invariant culture; no whitespace.
- Produces (extension methods on `object`, returning null/default when missing or wrong type): `JsonAccess.Str(this object obj, string key)`, `Long(...) -> long?`, `Bool(...) -> bool?`, `Obj(...) -> Dictionary<string, object>`, `Arr(...) -> List<object>`.

- [ ] **Step 1:** Tests: `Parses_nested_structures` (`{"a":[1,2.5,"x",true,null],"b":{"c":"d"}}` → `a[0]` is `1L`, `a[1]` is `2.5d`); `Parses_unicode_escapes_including_surrogate_pairs` (`"あ𠮷"` → `"あ𠮷"`); `Rejects_trailing_garbage` (`{} x` throws `JsonException`); `Rejects_unterminated_string`; `Writes_escapes` (`Write(new Dictionary<string,object>{{"p","a\"b\\c\n\u0001あ"}})` == `{"p":"a\"b\\c\n\u0001あ"}` with `\u0001` literally escaped as `\u0001`); `Round_trips` (parse → write → parse equal); `Writes_numbers_invariant` (`1.5` → `1.5` under `de-DE` culture).
- [ ] **Step 2:** `dotnet test tools/PureTests --filter JsonTests` → FAIL (types missing).
- [ ] **Step 3:** Implement a recursive-descent reader over the string and a `StringBuilder` writer.
- [ ] **Step 4:** `dotnet test tools/PureTests --filter JsonTests` → PASS.
- [ ] **Step 5:** Commit `feat(json): add dependency-free json reader and writer`.

### Task 3: API models, ordering, and error parsing

**Files:**
- Create: `Core/Api/Models.cs`, `ReleaseOrdering.cs`, `VersionComparer.cs`, `PetrichorApiException.cs`
- Test: `AssetExplorer.Tests/Core/Api/ModelsTests.cs`, `ReleaseOrderingTests.cs`, `PetrichorApiExceptionTests.cs`

**Interfaces:**
- Produces (all `internal sealed class` with public fields/properties and `static T FromJson(object json)`):
  - `enum ReleaseStatus { Verified, Legacy, Unverified }` (`"verified"|"legacy"|"unverified"`; unknown → `Unverified`)
  - `PackageRef { string Id; string Name; }`
  - `ReleaseSummary { string Id; string Version; ReleaseStatus Status; int SubmitterCount; int AssetCount; string ManifestId; string CreatedAt; PackageRef Package; }`
  - `AssetPathEntry { string Path; string Type; List<ReleaseSummary> Releases; }`
  - `AssetLookup { string Guid; List<AssetPathEntry> Paths; }`
  - `Suggestion { ReleaseSummary Release; int Matched; }`
  - `BulkLookup { Dictionary<string, AssetLookup> Results; List<string> Missing; List<Suggestion> Suggestions; }`
  - `SubmissionResult { string ReleaseId; string ManifestId; bool Verified; int SubmitterCount; bool Created; }` (`Created` set by the client from 201)
  - `Submission { string Id; string Version; string Source; string CreatedAt; ReleaseSummary Release; }`
  - `User { string ScreenName; string DisplayName; }` (`FromJson` reads the `user` wrapper)
- Produces: `static int ReleaseOrdering.TrustRank(ReleaseStatus s)` (0/1/2), `static List<ReleaseSummary> ReleaseOrdering.Sort(IEnumerable<ReleaseSummary>)` (trust rank, then `VersionComparer` desc, then `CreatedAt` desc), `static int VersionComparer.Compare(string a, string b)` (numeric runs compared as numbers, other runs ordinal).
- Produces: `class PetrichorApiException : Exception { int StatusCode; string ServerCode; static PetrichorApiException FromResponse(int status, string reasonPhrase, string body) }` — message from `error.message`, else `message`, else `reasonPhrase`; `ServerCode` from `error.code` when a string or number, else null.

- [ ] **Step 1:** Tests: `ReleaseSummary_parses_all_fields` (sample JSON with every field, status `"legacy"`); `Unknown_status_is_unverified`; `BulkLookup_parses_results_missing_suggestions`; `Submission_list_parses_bare_array` (via `JsonReader.Parse("[...]")` then mapping each); `Sort_puts_verified_then_legacy_then_unverified`; `Sort_orders_versions_naturally` (`1.10.0` before `1.9.0`); `FromResponse_reads_error_message` (`{"error":{"message":"x","code":400}}` → `"x"`); `FromResponse_reads_flat_message` (`{"message":"unauthorized"}`); `FromResponse_falls_back_to_reason_on_non_json` (`"<html>"` → reason).
- [ ] **Step 2:** `dotnet test tools/PureTests --filter "ModelsTests|ReleaseOrderingTests|PetrichorApiExceptionTests"` → FAIL.
- [ ] **Step 3:** Implement using `JsonAccess`.
- [ ] **Step 4:** Same command → PASS.
- [ ] **Step 5:** Commit `feat(api): add petrichor v2 response models`.

### Task 4: Lookup batching and merging

**Files:**
- Create: `Core/Api/LookupMerger.cs`
- Test: `AssetExplorer.Tests/Core/Api/LookupMergerTests.cs`

**Interfaces:**
- Consumes: `BulkLookup`, `Suggestion`, `ReleaseOrdering` (Task 3).
- Produces: `const int LookupMerger.BatchSize = 1000`, `const int LookupMerger.MaxSuggestions = 20`, `static List<List<string>> LookupMerger.Split(IEnumerable<string> guids)` (trim, lowercase, dedupe keeping first order, chunk), `static BulkLookup LookupMerger.Merge(IEnumerable<BulkLookup> parts)`.

- [ ] **Step 1:** Tests: `Split_normalizes_and_dedupes` (`[" ABC…", "abc…"]` → one batch with one lowercase guid); `Split_batches_by_1000` (2,001 distinct → sizes 1000, 1000, 1); `Merge_unions_results_and_missing`; `Merge_sums_matched_for_same_release` (release `r1` matched 3 in part A and 2 in part B → 5); `Merge_sorts_by_trust_then_matched` (unverified matched 9 sorts after verified matched 1); `Merge_cuts_to_20`.
- [ ] **Step 2:** `dotnet test tools/PureTests --filter LookupMergerTests` → FAIL.
- [ ] **Step 3:** Implement. Merge sort key: `TrustRank`, then `Matched` desc, then `VersionComparer` desc.
- [ ] **Step 4:** Same command → PASS.
- [ ] **Step 5:** Commit `feat(api): batch and merge bulk lookups`.

### Task 5: Manifest model, hash, and validation

**Files:**
- Create: `Core/Contribute/ManifestAsset.cs`, `PackageSpec.cs`, `Manifest.cs`, `ManifestHash.cs`, `ManifestValidator.cs`
- Test: `AssetExplorer.Tests/Core/Contribute/ManifestHashTests.cs`, `ManifestValidatorTests.cs`, `ManifestJsonTests.cs`

**Interfaces:**
- Produces: `sealed class ManifestAsset { string Guid; string Path; string Type; }` (ctor `(string guid, string path, string type)`).
- Produces: `enum IdentifierProvider { Package, Booth, Url }` (JSON `"package"|"booth"|"url"`); `sealed class PackageSpec` with factories `ById(string id)`, `ByIdentifier(IdentifierProvider provider, string value, string name, string description)`, `ByName(string name, string description)`, and `Dictionary<string, object> ToJsonObject()` (omits null/empty `name`/`description`).
- Produces: `sealed class Manifest { const string Format = "petrichor/v1"; PackageSpec Package; string Version; List<ManifestAsset> Assets; Dictionary<string, object> ToJsonObject(); string ToJson(); }` (asset `type` omitted when null/empty).
- Produces: `static string ManifestHash.Compute(IEnumerable<ManifestAsset> assets)` (lowercase hex sha256; sort by UTF-8 bytes of guid then path; uses `SHA256.Create()`).
- Produces: `static List<ManifestAsset> ManifestValidator.Normalize(IEnumerable<ManifestAsset> assets)` (lowercase guid, `\` → `/`, merge identical (guid, path) keeping first non-empty type; keeps conflicting pairs so `Validate` can report them) and `static List<string> ManifestValidator.Validate(Manifest manifest)` (Japanese messages; empty list = valid).

Validation messages (exact):
- `バージョンを入力してください` (empty), `バージョンは 64 文字以内にしてください`
- `アセットがありません`, `アセットは 30,000 件までです (現在 {n:N0} 件)`
- `GUID の形式が正しくありません: {guid}`
- `パスが長すぎます (1,024 文字まで): {path}`, `パスに使えない文字が含まれています: {guid}` (control chars or lone surrogates)
- `同じ GUID に別のパスがあります: {guid}`
- `パッケージ名を入力してください` (ByName with empty name), `パッケージ名は 191 文字以内にしてください`, `説明は 10,000 文字以内にしてください`, `識別子を入力してください`, `識別子は 512 文字以内にしてください`, `パッケージ ID を入力してください`
- `種類が長すぎます: {guid}` (type > 191)

- [ ] **Step 1:** Hash tests with vectors computed from teyvat's algorithm:
  - A: `{bbbb…b (32), "Assets/B.png"}, {aaaa…a (32), "Assets/A.mat"}` → `7a63a8f15e7b645b81f28e404d95d3da9e922ce9bb8b9278d8025301ac0d988e` (input order must not matter: also assert the reversed list).
  - B: `{0123456789abcdef0123456789abcdef, "Assets/テクスチャ/𠮷.png"}, {ffff…f (32), "Assets/z.txt"}` → `a4178d5670e56ece444a0caedfbb682a952bd39bea8afbba02e69ab96310ff54`.
  - Single: `{aaaa…a, "Assets/A.mat"}` → `9e710bb63a10f3025d1aea88f20955d849c5c312019caaa94aa52940c286d246`.
- [ ] **Step 2:** Validator tests: `Normalize_lowercases_and_fixes_slashes`; `Normalize_merges_duplicates_keeping_type`; `Validate_reports_conflicting_path`; `Validate_rejects_control_characters` (`"Assets/a\tb"`); `Validate_rejects_lone_surrogate` (`"Assets/\ud800.png"`); `Validate_rejects_bad_guid`; `Validate_limits_version_length` (65 chars); `Validate_requires_name_for_name_only_package`; `Validate_accepts_valid_manifest` (empty list).
- [ ] **Step 3:** JSON tests: `ToJson_matches_submission_shape` — parse `ToJson()` and assert `format == "petrichor/v1"`, `package.identifier.provider == "booth"`, `package.identifier.value == "123"`, no `description` key when null, asset without type has no `type` key.
- [ ] **Step 4:** `dotnet test tools/PureTests --filter "Manifest"` → FAIL; implement; → PASS.
- [ ] **Step 5:** Commit `feat(contribute): add manifest model, hash, and validation`.

### Task 6: `.meta` parsing, folder scanning, `.unitypackage` reading, package info

**Files:**
- Create: `Core/Contribute/MetaFile.cs`, `MetaScanner.cs`, `UnityPackageReader.cs`, `PackageIdentifier.cs`, `PackageInfoDetector.cs`
- Test: `AssetExplorer.Tests/Core/Contribute/MetaFileTests.cs`, `MetaScannerTests.cs`, `UnityPackageReaderTests.cs`, `PackageIdentifierTests.cs`, `PackageInfoDetectorTests.cs`, plus `TarBuilder.cs` (test helper writing ustar + gzip)

**Interfaces:**
- Produces: `sealed class MetaFile { string Guid; string ImporterType; bool IsFolder; static MetaFile Parse(string text) }` — `Guid` from a top-level `guid:` line (lowercased; null if absent → `Parse` returns null); `ImporterType` = first top-level (column 0) key ending in `Importer` (without the colon), else null; `IsFolder` when a top-level `folderAsset: yes` exists.
- Produces: `static List<ManifestAsset> MetaScanner.Scan(string physicalRoot, string projectPrefix)` — enumerate `*.meta` recursively under `physicalRoot`; skip when the sibling asset file does not exist as a file, when `MetaFile.Parse` is null or `IsFolder`, and skip entries whose name starts with `.` or whose directory segment ends with `~`; path = `projectPrefix + "/" + relative path with "/"`; sorted by path (ordinal).
- Produces: `sealed class UnityPackageEntry { string Guid; string Path; MetaFile Meta; bool HasAsset; long AssetSize; }`; `static List<UnityPackageEntry> UnityPackageReader.Read(Stream gzipStream, IProgress<long> compressedBytesRead, CancellationToken ct)`; `static byte[] UnityPackageReader.ReadAsset(Stream gzipStream, string guid, CancellationToken ct)` (null when absent); `static List<ManifestAsset> UnityPackageReader.ToManifestAssets(IEnumerable<UnityPackageEntry> entries)` (skip `!HasAsset` or `Meta == null` or `Meta.IsFolder`; type from `Meta.ImporterType`; sorted by path). Tar handling: 512-byte headers, size in octal, `prefix` (ustar, offset 345) + `name`, typeflag `L` (GNU long name for next entry), `x` (PAX, `path=` record for next entry), skip `g`; entries named `<guid>/pathname` (first line, trim `\r`, stop at `\n`), `<guid>/asset.meta`, `<guid>/asset` (not buffered by `Read`; only size recorded). Progress is reported from the underlying file stream position (wrap with a counting stream).
- Produces: `static bool PackageIdentifier.TryParseBooth(string url, out string itemId)` (host `booth.pm` or `*.booth.pm`, path `^(?:/[a-z]{2}(?:-[a-z]+)?)?/items/(\d+)/?$` case-insensitive) and `static (IdentifierProvider Provider, string Value) PackageIdentifier.FromUrl(string url)`.
- Produces: `sealed class DetectedPackageInfo { string Name; string DisplayName; string Version; string Description; }`; `static DetectedPackageInfo PackageInfoDetector.FromPackageJson(string json)` (null on invalid JSON); `static DetectedPackageInfo PackageInfoDetector.FromFileName(string fileName)` — strip `.unitypackage`, version = first match of `v?(\d+(?:\.\d+)+(?:[-+][0-9A-Za-z.-]+)?)`, name = text before it trimmed of `_- v`, or whole stem when no version.

- [ ] **Step 1:** `MetaFileTests`: `Parses_guid_and_importer` (`fileFormatVersion: 2\nguid: ABCDEF…\nTextureImporter:\n  serializedVersion: 12` → guid lowercase, `TextureImporter`); `Detects_folder` (`folderAsset: yes` + `DefaultImporter:`); `Indented_importer_keys_are_ignored`; `Missing_guid_returns_null`.
- [ ] **Step 2:** `MetaScannerTests` (temp dir): files `A.mat`+`A.mat.meta`, folder `Sub` + `Sub.meta` (folder), `Sub/B.png`+meta, orphan `C.txt.meta` without `C.txt`, `Hidden~/D.txt`+meta → scan with prefix `Assets/Pkg` returns exactly `Assets/Pkg/A.mat`, `Assets/Pkg/Sub/B.png` in that order with their guids.
- [ ] **Step 3:** `UnityPackageReaderTests` (archives built by `TarBuilder`): `Reads_entries_in_any_order` (`asset` then `pathname` then `asset.meta`, and the reverse); `Skips_folder_entries` (entry with only `pathname`+`asset.meta` with `folderAsset: yes`); `Reads_gnu_long_names`; `Reads_pax_path`; `Trims_pathname_trailer` (`"Assets/A.mat\n00"` → `Assets/A.mat`); `ReadAsset_returns_bytes_for_guid`; `Cancellation_throws` (pre-cancelled token → `OperationCanceledException`).
- [ ] **Step 4:** `PackageIdentifierTests`: `https://booth.pm/ja/items/123456` → booth `123456`; `https://foo.booth.pm/items/42` → booth `42`; `https://example.com/x` → url same string; `not a url` → url same string.
- [ ] **Step 5:** `PackageInfoDetectorTests`: package.json `{"name":"jp.lilxyzw.liltoon","displayName":"lilToon","version":"1.8.3","description":"d"}` → all four; `lilToon_1.8.3.unitypackage` → `lilToon`, `1.8.3`; `MyAvatar v2.0.unitypackage` → `MyAvatar`, `2.0`; `Thing.unitypackage` → `Thing`, null.
- [ ] **Step 6:** `dotnet test tools/PureTests --filter "MetaFile|MetaScanner|UnityPackageReader|PackageIdentifier|PackageInfoDetector"` → FAIL; implement; → PASS.
- [ ] **Step 7:** Commit `feat(contribute): read .meta files, folders, and .unitypackage archives`.

### Task 7: PKCE, OAuth client, loopback receiver

**Files:**
- Create: `Core/Auth/Pkce.cs`, `Token.cs`, `OAuthException.cs`, `OAuthClient.cs`, `LoopbackReceiver.cs`
- Test: `AssetExplorer.Tests/Core/Auth/PkceTests.cs`, `OAuthClientTests.cs`, `LoopbackReceiverTests.cs`, `MockHandler.cs` (test helper: `HttpMessageHandler` that records requests and returns queued responses)

**Interfaces:**
- Produces: `sealed class Pkce { string Verifier; string Challenge; string Method => "S256"; static Pkce Create(); static Pkce FromVerifier(string verifier); }` (32 random bytes via `RandomNumberGenerator.Create().GetBytes`, base64url without padding).
- Produces: `sealed class Token { string AccessToken; string RefreshToken; string TokenType; string Scope; DateTimeOffset? ExpiresAt; }` — `ExpiresAt` from `expires_at` (ISO 8601) or `now + expires_in`.
- Produces: `enum OAuthErrorKind { AuthorizationRequestFailed, InvalidAuthorizationResponse, InvalidTokenResponse }`; `sealed class OAuthException : Exception { OAuthErrorKind Kind; string Error; string ErrorDescription; int? StatusCode; }`.
- Produces: `sealed class OAuthClient(string clientId, string authorizeEndpoint, string tokenEndpoint, HttpClient http, Func<DateTimeOffset> clock)` with `Uri GetAuthorizeUrl(string redirectUri, Pkce pkce, string state)`, `string GetAuthorizationCode(Uri callbackUrl, string expectedState, string expectedRedirectUri, string expectedIssuer)`, `Task<Token> GetAccessTokenByCodeAsync(string code, string redirectUri, Pkce pkce, CancellationToken ct)`, `Task<Token> GetAccessTokenByRefreshTokenAsync(string refreshToken, CancellationToken ct)`. Form encoding matches the SDK (`A-Z a-z 0-9 * - . _` literal, space `+`). No `client_secret` is ever sent. `GetAuthorizationCode` order: redirect prefix → state → `error` → `iss` (only when present and `expectedIssuer` non-null: must equal it) → `code`.
- Produces: `sealed class LoopbackReceiver : IDisposable { const int DefaultPort = 57193; const string RedirectUri = "http://127.0.0.1:57193/callback"; LoopbackReceiver(int port = DefaultPort); void Start(); Task<Uri> WaitForCallbackAsync(TimeSpan timeout, CancellationToken ct); }` — prefix `http://127.0.0.1:{port}/callback/`; `Start` throws `OAuthException(InvalidAuthorizationResponse)` with message `ポート {port} が使用中です。他の Unity を閉じてからやり直してください` when `HttpListenerException` occurs; non-`/callback` paths get 404 and keep waiting; the callback request gets `text/html; charset=utf-8` with title "Petrichor" and body `ログインしました。Unity に戻ってください。` (or `ログインできませんでした: {error}` when the query has `error`); timeout → `TimeoutException`. The returned `Uri` is rebuilt as `RedirectUri + "?" + query` (port-independent of the request's Host header).

- [ ] **Step 1:** `PkceTests.Rfc7636_vector`: `FromVerifier("dBjftJeZ4CVP-mJ92K1-6Ub6p2KG2AYwKdAI4y5h6cU").Challenge == "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"`; `Create_produces_43_char_verifier`.
- [ ] **Step 2:** `OAuthClientTests`: `Authorize_url_has_all_params` (`response_type=code`, `client_id`, `redirect_uri=http%3A%2F%2F127.0.0.1%3A57193%2Fcallback`, `state`, `code_challenge`, `code_challenge_method=S256`); `Callback_state_mismatch_throws_InvalidAuthorizationResponse`; `Callback_error_throws_AuthorizationRequestFailed` (carries `error`); `Callback_iss_mismatch_throws`; `Callback_returns_code`; `Code_exchange_posts_form_without_secret` (body contains `grant_type=authorization_code`, `code_verifier=`, `client_id=`, not `client_secret`); `Refresh_posts_refresh_grant`; `Token_expiry_from_expires_in` (clock fixed, `expires_in: 604800` → +7 days); `Non_2xx_token_response_throws_InvalidTokenResponse_with_status` (400 `{"error":"invalid_grant"}` → `Error == "invalid_grant"`, `StatusCode == 400`).
- [ ] **Step 3:** `LoopbackReceiverTests` (each test uses its own fixed high port, 57290–57293, never the production 57193): `Returns_callback_uri` (GET `/callback?code=c&state=s` with `HttpClient` → result query contains `code=c`, response body contains `ログインしました`); `Ignores_other_paths` (GET `/favicon.ico` → 404, then callback still completes); `Times_out` (100 ms → `TimeoutException`); `Second_receiver_on_same_port_reports_port_in_use`.
- [ ] **Step 4:** `dotnet test tools/PureTests --filter "Pkce|OAuthClient|LoopbackReceiver"` → FAIL; implement; → PASS.
- [ ] **Step 5:** Commit `feat(auth): port the sdk oauth flow with pkce and a loopback receiver`.

### Task 8: Token storage abstraction and AuthSession

**Files:**
- Create: `Core/Auth/StoredToken.cs`, `ITokenStorage.cs`, `AuthSession.cs`
- Test: `AssetExplorer.Tests/Core/Auth/AuthSessionTests.cs` (with an in-memory `ITokenStorage`)

**Interfaces:**
- Consumes: `OAuthClient`, `Token`, `LoopbackReceiver`, `Pkce` (Task 7); `User` (Task 3).
- Produces: `sealed class StoredToken { string AccessToken; string RefreshToken; DateTimeOffset? ExpiresAt; string ScreenName; string DisplayName; string ToJson(); static StoredToken FromJson(string json); }` (null on bad JSON).
- Produces: `interface ITokenStorage { StoredToken Load(); void Save(StoredToken token); void Clear(); }`.
- Produces: `sealed class AuthRequiredException : Exception` (message `ログインし直してください`).
- Produces: `sealed class AuthSession(ITokenStorage storage, Func<OAuthClient> oauth, Func<DateTimeOffset> clock)` with `bool IsLoggedIn`, `StoredToken Current`, `event Action Changed`, `Task<string> GetAccessTokenAsync(CancellationToken ct)` (refresh when `ExpiresAt` null or `< now + 1 day`; concurrent callers share one refresh `Task`), `Task<HttpResponseMessage> SendAsync(HttpClient http, Func<string, HttpRequestMessage> createRequest, CancellationToken ct)` (factory receives the access token; on 401 force-refresh once and resend), `Task LoginAsync(Action<Uri> openBrowser, CancellationToken ct)` (Pkce + `Guid.NewGuid().ToString("N")` state, `LoopbackReceiver.Start()` before opening the browser, 5-minute wait, exchange, save, raise `Changed`), `void SetProfile(User user)` (save names, raise `Changed`), `void Logout()` (clear, raise `Changed`). The issuer passed to `GetAuthorizationCode` is the authorize endpoint's `scheme://host[:port]`; `OAuthClient` exposes it as `string Issuer`.
- Refresh failure rule: `OAuthException` with `Kind == InvalidTokenResponse` and `StatusCode` 400 or 401 → `storage.Clear()`, raise `Changed`, throw `AuthRequiredException`; any other exception propagates and keeps tokens.

- [ ] **Step 1:** Tests: `Not_logged_in_throws_AuthRequired`; `Fresh_token_is_returned_without_refresh` (expires in 3 days, no HTTP calls); `Token_near_expiry_is_refreshed_and_saved` (expires in 2 h → one `/token` call, storage holds the new tokens); `Concurrent_callers_share_one_refresh` (two `GetAccessTokenAsync` calls → one `/token` request); `Send_retries_once_after_401` (API 401 then 200 → one refresh, two API calls, result 200); `Refresh_rejected_clears_tokens_and_throws_AuthRequired` (`/token` 400 `invalid_grant` → storage cleared, `Changed` raised); `Network_error_on_refresh_keeps_tokens` (handler throws `HttpRequestException` → tokens kept, exception propagates).
- [ ] **Step 2:** `dotnet test tools/PureTests --filter AuthSessionTests` → FAIL; implement; → PASS.
- [ ] **Step 3:** Commit `feat(auth): add auth session with refresh and retry`.

### Task 9: PetrichorClient

**Files:**
- Create: `Core/Api/PetrichorClient.cs`
- Test: `AssetExplorer.Tests/Core/Api/PetrichorClientTests.cs`, `PetrichorLiveTests.cs` (`[Category("Live")]`, `[Explicit]`)

**Interfaces:**
- Consumes: models (Task 3), `LookupMerger` (Task 4), `Manifest` (Task 5), `AuthSession` (Task 8), `JsonReader`/`JsonWriter` (Task 2).
- Produces: `sealed class PetrichorClient(HttpClient http, Func<string> apiBaseUrl, AuthSession auth)` with
  - `const string PetrichorPath = "/petrichor/v2"`
  - `Task<AssetLookup> GetAssetAsync(string guid, CancellationToken ct)` (null on 404; guid trimmed + lowercased + URL-escaped)
  - `Task<BulkLookup> LookupAsync(IEnumerable<string> guids, IProgress<(int Done, int Total)> progress, CancellationToken ct)` (empty input → empty result without a request)
  - `Task<SubmissionResult> SubmitAsync(Manifest manifest, CancellationToken ct)` (`Created = status == 201`)
  - `Task<List<Submission>> GetMySubmissionsAsync(CancellationToken ct)`
  - `Task<User> GetMeAsync(CancellationToken ct)` (`GET {api}/egeria/v1/me`)
  - Requests carry `User-Agent` from the static `PetrichorClient.UserAgent` (set by the Unity side to `Petrichor-Unity/<version> (Unity <unityVersion>)`; default `Petrichor-Unity`), `Accept: application/json`; JSON bodies `application/json; charset=utf-8`. Non-2xx → `PetrichorApiException.FromResponse`.

- [ ] **Step 1:** Tests with `MockHandler`: `GetAsset_returns_null_on_404`; `GetAsset_hits_lowercased_guid_url` (`https://api.test/petrichor/v2/assets/abc…`); `Lookup_sends_batches_sequentially_and_reports_progress` (2,500 guids → 3 POSTs with 1000/1000/500 guids; progress `(1,3)`, `(2,3)`, `(3,3)`); `Submit_sends_bearer_and_body` (Authorization `Bearer at`, body parses with `format == "petrichor/v1"`); `Submit_maps_201_to_Created`; `Submit_429_throws_with_status`; `MySubmissions_parses_array`; `Cancelled_request_throws_OperationCanceled`.
- [ ] **Step 2:** Live tests (explicit, against `https://api.natsuneko.com`): `Live_lookup_unknown_guid_is_missing` (`Guid.NewGuid().ToString("N")` → appears in `Missing`, `Results` empty); `Live_get_unknown_asset_returns_null`.
- [ ] **Step 3:** `dotnet test tools/PureTests --filter PetrichorClientTests` → FAIL; implement; → PASS. Then `dotnet test tools/PureTests --filter Category=Live` → PASS (requires network).
- [ ] **Step 4:** Commit `feat(api): add petrichor v2 http client`.

### Task 10: Unity integration and missing-reference scanning

**Files:**
- Create: `Core/Scanning/YamlGuidExtractor.cs`, `MissingReference.cs`
- Create: `Integration/PetrichorSettings.cs`, `EditorPrefsTokenStorage.cs`, `PetrichorServices.cs`, `MissingReferenceScanner.cs`, `ManifestSources.cs`
- Test: `AssetExplorer.Tests/Core/Scanning/YamlGuidExtractorTests.cs`

**Interfaces:**
- Produces: `static List<(string Guid, string Key)> YamlGuidExtractor.Extract(string yaml)` — every `guid: [0-9a-fA-F]{32}` (lowercased, order of appearance, duplicates kept); `Key` is the mapping key on the same line before the first `:` that precedes `{` (`m_Script: {fileID: 11500000, guid: …}` → `m_Script`); for list items (`- {fileID: …, guid: …}`) it is the nearest preceding line with a smaller indent whose content ends with `:` (`m_Materials:` → `m_Materials`); `static bool YamlGuidExtractor.IsYaml(string text)` (starts with `%YAML`).
- Produces: `sealed class MissingReference { string Guid; List<string> Locations; }`.
- Produces: `static class PetrichorSettings` with constants from Global Constraints, properties `ApiBaseUrl`, `WebBaseUrl`, `AuthorizeUrl`, `ClientId` (EditorPrefs keys `NatsunekoLaboratory.Petrichor.<Name>`, empty → default), derived `TokenEndpoint`, `RedirectUri` (= `LoopbackReceiver.RedirectUri`), `static void ResetAll()`, `static event Action Changed`; helpers `PackageUrl(string id)` → `{web}/packages/{id}`, `ReleaseUrl(string id)` → `{web}/releases/{id}`, `ContributeUrl` → `{web}/contribute`.
- Produces: `sealed class EditorPrefsTokenStorage : ITokenStorage` (key `NatsunekoLaboratory.Petrichor.Token:` + `PetrichorSettings.ApiBaseUrl`).
- Produces: `static class PetrichorServices { static HttpClient Http; static AuthSession Auth; static PetrichorClient Client; }` — one `HttpClient` (`Timeout = 100 s`); `Auth` uses `new OAuthClient(PetrichorSettings.ClientId, PetrichorSettings.AuthorizeUrl, PetrichorSettings.TokenEndpoint, Http, () => DateTimeOffset.UtcNow)` built per call; sets `PetrichorClient.UserAgent` with `Application.unityVersion`; lazy initialization. `AuthSession` gains `void Reload()` (re-read storage, raise `Changed`), called on `PetrichorSettings.Changed` so switching the API base URL switches the stored account.
- Produces: `static class MissingReferenceScanner { List<MissingReference> ScanObject(UnityEngine.Object target); List<MissingReference> ScanOpenScenes(); List<MissingReference> ScanFolder(string assetFolder); }` — spec §5.2 rules; results merged by GUID (locations deduped, in discovery order) and sorted by first location. Missing = `GUIDToAssetPath` empty, or the path is neither an existing file nor folder and does not start with `Library/` or `Resources/unity_builtin_extra`. Location strings: assets `"{assetPath} · {key}"`; scene objects `"{hierarchyPath} · {ComponentType} · {property.displayName}"`.
- Produces: `static class ManifestSources { List<ManifestAsset> FromFolder(string assetFolder); List<ManifestAsset> FromPackage(UnityEditor.PackageManager.PackageInfo package); List<ManifestAsset> FromSelection(); string FindPackageJson(string assetFolderOrPackageRoot); }` — wrap `MetaScanner.Scan` (Task 6); selection expands folders, single files use their own `.meta`; results normalized with `ManifestValidator.Normalize`.

- [ ] **Step 1:** `YamlGuidExtractorTests`: `Extracts_inline_reference_key` (`m_Script: {fileID: 11500000, guid: 0123…, type: 3}` → key `m_Script`); `Extracts_list_item_key` (`  m_Materials:\n  - {fileID: 2100000, guid: abcd…, type: 2}` → key `m_Materials`); `Lowercases_guids`; `IsYaml_detects_header`.
- [ ] **Step 2:** `dotnet test tools/PureTests --filter YamlGuidExtractorTests` → FAIL; implement; → PASS.
- [ ] **Step 3:** Implement the `Integration/` classes.
- [ ] **Step 4:** `tools/unity-check.sh` → `RESULT passed=<all> failed=0` (Unity compiles `Integration/`).
- [ ] **Step 5:** Commit `feat: add unity settings, token storage, and missing reference scanning` (include copied-back `.meta` files).

### Task 11: Window shell, components, Lookup and Missing tabs

**Files:**
- Create: `UI/PetrichorWindow.cs`, `UI/PetrichorTab.cs`, `UI/Petrichor.uss`, `UI/Petrichor.uss.meta` (template: the `ScriptedImporter` meta of the old `globals.uss.meta` with a new random 32-hex GUID, referenced as `PetrichorWindow.StyleSheetGuid`)
- Create: `UI/Components/{StatusBadge,ReleaseRow,StateView,GuidLabel,Section,AsyncView,Mono}.cs`
- Create: `UI/Tabs/LookupTab.cs`, `UI/Tabs/MissingTab.cs`

**Interfaces:**
- Produces: `enum PetrichorTab { Lookup, Missing, Contribute, Submissions, Settings }`; `static PetrichorWindow PetrichorWindow.Open(PetrichorTab tab)`; `void PetrichorWindow.Select(PetrichorTab tab)`; tabs implement `abstract class TabView : VisualElement { virtual void OnShown(); virtual void OnClosed(); }`.
- Produces: `MissingTab.Scan(UnityEngine.Object target)`, `MissingTab.ScanFolder(string assetFolder)` (used by menus in Task 13); `ContributeTab.SetFolder(string assetFolder)` is declared in Task 12.
- Produces components: `StatusBadge(ReleaseStatus status)` (text 検証済み/Legacy/未検証, classes `petrichor-badge--verified|legacy|unverified`); `ReleaseRow(ReleaseSummary release, string trailingText = null)` (package name → `Application.OpenURL(PetrichorSettings.PackageUrl)`, version → `ReleaseUrl`, badge, trailing text); `StateView` with `ShowIdle(string)`, `ShowLoading(string)`, `ShowEmpty(string title, string body, string actionLabel = null, Action action = null)`, `ShowError(string message, Action retry)`, `ShowContent()`; `GuidLabel(string text)` (read-only selectable `TextField` + copy button, monospace); `Section(string title)` with `Content`; `Mono.Apply(VisualElement)` sets the RobotoMono font when available; `AsyncView` helper: `Task RunAsync(Func<CancellationToken, Task> work)` cancels the previous run, skips continuation when `panel == null`, routes `PetrichorApiException`/`HttpRequestException`/`TaskCanceledException` (timeout) to `StateView.ShowError` and logs anything else with `Debug.LogException`.
- USS: only `var(--unity-colors-…)` variables for colors, except badge tints, which use one mid tone that reads on both skins: verified background `rgba(46, 160, 67, 0.22)` text `#2ea043`; legacy background `rgba(56, 132, 255, 0.20)` text `#4c8dff`; unverified transparent background, `var(--unity-colors-default-border)` border, `var(--unity-colors-label-text)` text with the row at `opacity: 0.75`. Class prefix `petrichor-`.

- [ ] **Step 1:** Window: top bar (title, segmented `ToolbarToggle`s for the five tabs, account chip button bound to `PetrichorServices.Auth.Changed`), body hosting the selected `TabView`; `[SerializeField] PetrichorTab _tab`; `minSize = new Vector2(420, 360)`; unsubscribe events in `OnDisable` and call `OnClosed` on every tab.
- [ ] **Step 2:** Lookup tab per spec §5.1 (GUID field validates `^[0-9a-fA-F]{32}$` after trim; Object field fills `AssetDatabase.AssetPathToGUID`; Enter key or 検索 button; not-found empty state with action "登録タブを開く").
- [ ] **Step 3:** Missing tab per spec §5.2 (Object field + 開いているシーン + フォルダ… buttons; inline status line `Missing 参照 {n} 件 · 照合 {done}/{total}`; Suggestions section; per-GUID foldouts; not-found section with "すべてコピー" (`EditorGUIUtility.systemCopyBuffer`)). Partial failure keeps finished batches visible with `一部のみ表示しています`.
- [ ] **Step 4:** `tools/unity-check.sh` → compiles, `failed=0`.
- [ ] **Step 5:** Commit `feat(ui): add petrichor window with lookup and missing tabs`.

### Task 12: Contribute tab

**Files:**
- Create: `UI/Tabs/ContributeTab.cs`

**Interfaces:**
- Consumes: `ManifestSources` (Task 10), `UnityPackageReader`, `PackageInfoDetector`, `PackageIdentifier` (Task 6), `Manifest`, `ManifestValidator`, `ManifestHash` (Task 5), `PetrichorServices.Client.SubmitAsync` (Task 9).
- Produces: `void ContributeTab.SetFolder(string assetFolder)` (selects the folder source and builds immediately).

- [ ] **Step 1:** Source row (フォルダ ObjectField of `DefaultAsset` limited to folders, Packages/ `PopupField<string>` from `PackageInfo.GetAllRegisteredPackages()`, 選択中 button, .unitypackage button with `EditorUtility.OpenFilePanel("unitypackage を選ぶ", "", "unitypackage")`). `.unitypackage` reading runs in `Task.Run` with progress `読み込み中… {MB} MB` and a cancel button; a second pass reads the shallowest `package.json` asset when present.
- [ ] **Step 2:** Preview: count, `ListView` of paths (virtualized, monospace, `type` dimmed), manifest ID via `GuidLabel`, problem list (first 5, then `他 {n} 件`).
- [ ] **Step 3:** Package info form: identifier `PopupField` (UPM/VPM 名, BOOTH URL, URL, Petrichor パッケージ ID, なし) + value field, name, description (multiline), version; prefilled from `PackageInfoDetector`; BOOTH URL that fails `TryParseBooth` shows `BOOTH の商品ページの URL を入力してください` inline; privacy note text: `送信されるのはアセットの GUID・パス・種類だけで、ファイルの中身は送られません。配布されていない (非公開の) アセットを含めないでください。`
- [ ] **Step 4:** Actions: 登録する / ログインして登録 (opens Settings tab), JSON を保存 (`EditorUtility.SaveFilePanel("マニフェストを保存", "", "<name>-<version>.petrichor.json", "json")`, UTF-8 without BOM, then `EditorUtility.DisplayDialog` offering to open `ContributeUrl`). Result panel per spec §5.3 exact strings, including `あと {n} 人の登録で検証済みになります`. Error mapping 401/404/429 strings from the spec.
- [ ] **Step 5:** Two-column layout at width ≥ 760 via `GeometryChangedEvent`.
- [ ] **Step 6:** `tools/unity-check.sh` → compiles, `failed=0`.
- [ ] **Step 7:** Commit `feat(ui): add contribute tab`.

### Task 13: Submissions and Settings tabs, menus

**Files:**
- Create: `UI/Tabs/SubmissionsTab.cs`, `UI/Tabs/SettingsTab.cs`, `UI/MenuItems.cs`

**Interfaces:**
- Consumes: `PetrichorServices` (Task 10), `PetrichorWindow.Open`, `MissingTab.Scan/ScanFolder`, `ContributeTab.SetFolder`.

- [ ] **Step 1:** Submissions tab per spec §5.4 (auto-load on first `OnShown` when logged in; rows: package, version, `StatusBadge`, source badge, `DateTimeOffset.Parse(createdAt).ToLocalTime():yyyy/MM/dd HH:mm`, links).
- [ ] **Step 2:** Settings tab per spec §5.5 (login flow via `PetrichorServices.Auth.LoginAsync(Application.OpenURL, ct)` then `GetMeAsync` → `SetProfile`; waiting state with キャンセル; logout note `連携の解除は accounts.natsuneko.com の「連携中のアプリ」から行えます`; 詳細設定 foldout with four fields + リセット each, redirect URI read-only).
- [ ] **Step 3:** Menus: `Window/Natsuneko Laboratory/Petrichor/Petrichor` (Lookup), `…/Find Missing References`, `…/Contribute`, `…/My Submissions`, `…/Settings`; `Assets/Petrichor/Find Missing References` (validated: one selection); `Assets/Petrichor/Contribute This Folder` (validated: `AssetDatabase.IsValidFolder`); `GameObject/Petrichor/Find Missing References` (validated: `Selection.activeGameObject != null`).
- [ ] **Step 4:** `tools/unity-check.sh` → compiles, `failed=0`.
- [ ] **Step 5:** Commit `feat(ui): add submissions and settings tabs and menu items`.

### Task 14: README, final verification, PR

**Files:**
- Modify: `README.md`

- [ ] **Step 1:** README: what Petrichor is (link `https://petrichor.natsuneko.com`), install (import the `.unitypackage`), features (one subsection each, menu paths), login (browser, loopback `http://127.0.0.1:57193/callback`, tokens in EditorPrefs per machine), privacy (only GUID/path/type), maintainer note on registering the Egeria app (normal app, not CIMD; redirect URI; set `DefaultClientId`), development (`dotnet test tools/PureTests`, `tools/unity-check.sh`), license.
- [ ] **Step 2:** `dotnet test tools/PureTests` → all pass; `dotnet test tools/PureTests --filter Category=Live` → pass; `tools/unity-check.sh` → `failed=0`. `git status` shows no stray generated files.
- [ ] **Step 3:** Commit `docs: rewrite readme for petrichor`.
- [ ] **Step 4:** The user asked for development on the default branch (no PR). `git fetch origin main` and confirm `origin/main` is an ancestor of `HEAD` (`git merge-base --is-ancestor origin/main HEAD`); then `git push origin HEAD:main` (fast-forward only, never force). Report the click-through checklist (each tab's states, light/dark skin, login, direct submit, JSON save + website upload, Unity 2022.3 compile) and the manual registration step in the final message, and tell the user to `git pull` in the main checkout.
