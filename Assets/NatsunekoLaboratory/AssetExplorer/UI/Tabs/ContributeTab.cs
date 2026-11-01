// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NatsunekoLaboratory.AssetExplorer.Api;
using NatsunekoLaboratory.AssetExplorer.Contribute;
using NatsunekoLaboratory.AssetExplorer.Integration;
using NatsunekoLaboratory.AssetExplorer.UI.Components;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NatsunekoLaboratory.AssetExplorer.UI.Tabs
{
    /// <summary>Builds a manifest from a folder, package, selection or .unitypackage, then submits it or saves it as JSON.</summary>
    internal sealed class ContributeTab : TabView
    {
        private const float WideLayoutWidth = 760;
        private const int ShownProblems = 5;

        private static readonly string[] IdentifierKinds = { "UPM/VPM 名", "BOOTH URL", "URL", "Petrichor パッケージ ID", "なし (名前のみ)" };

        private readonly VisualElement _columns;
        private readonly TextField _description;
        private readonly ObjectField _folder;
        private readonly TextField _identifier;
        private readonly Label _identifierError;
        private readonly PopupField<string> _identifierKind;
        private readonly VisualElement _manifestId;
        private readonly TextField _name;
        private readonly PopupField<string> _packages;
        private readonly ListView _preview;
        private readonly Label _previewCount;
        private readonly VisualElement _problems;
        private readonly VisualElement _result;
        private readonly Button _save;
        private readonly AsyncView _sourceAsync;
        private readonly StateView _sourceState;
        private readonly Button _submit;
        private readonly AsyncView _submitAsync;
        private readonly StateView _submitState;
        private readonly TextField _version;

        private List<ManifestAsset> _assets = new List<ManifestAsset>();
        private List<PackageInfo> _packageInfos = new List<PackageInfo>();

        public ContributeTab()
        {
            Add(Ui.Text("パッケージに含まれるアセットの GUID とパスを Petrichor に登録します。同じ内容を 2 人以上が登録すると「検証済み」になります。", "petrichor-hint"));

            _columns = new VisualElement();
            _columns.AddToClassList("petrichor-columns");
            Add(_columns);
            RegisterCallback<GeometryChangedEvent>(e => _columns.EnableInClassList("petrichor-columns--wide", e.newRect.width >= WideLayoutWidth));

            var left = Column();
            var right = Column();

            // 1. source
            left.Add(Ui.Heading("1. ソース"));

            _folder = new ObjectField("フォルダ") { objectType = typeof(DefaultAsset), allowSceneObjects = false };
            _folder.AddToClassList("petrichor-field");
            _folder.RegisterValueChangedCallback(e =>
            {
                var path = e.newValue == null ? null : AssetDatabase.GetAssetPath(e.newValue);
                if (string.IsNullOrEmpty(path))
                    return;

                if (AssetDatabase.IsValidFolder(path))
                    SetFolder(path);
                else
                    _sourceState.ShowError("フォルダを選んでください", null);
            });
            left.Add(_folder);

            _packages = new PopupField<string>("Packages/", new List<string> { "(選ぶ)" }, 0);
            _packages.AddToClassList("petrichor-field");
            _packages.RegisterCallback<PointerDownEvent>(_ => RefreshPackageChoices(), TrickleDown.TrickleDown);
            _packages.RegisterValueChangedCallback(e => SetPackage(e.newValue));
            left.Add(_packages);

            var sourceButtons = Ui.Row(Ui.Button("選択中のアセット", SetSelection), Ui.Button(".unitypackage を読む…", PickUnityPackage));
            sourceButtons.AddToClassList("petrichor-gap");
            left.Add(sourceButtons);

            _sourceState = new StateView();
            _sourceState.ShowIdle("フォルダ・パッケージ・選択中のアセット・.unitypackage のどれかを選んでください");
            left.Add(_sourceState);
            _sourceAsync = new AsyncView(this, _sourceState);

            // 2. preview
            left.Add(Ui.Heading("2. プレビュー"));
            _previewCount = Ui.Text("アセット 0 件", "petrichor-hint");
            left.Add(_previewCount);

            _preview = new ListView
            {
                fixedItemHeight = 18,
                selectionType = SelectionType.None,
                makeItem = () => MonoFont.Apply(new Label()),
                bindItem = (element, index) =>
                {
                    var asset = _assets[index];
                    ((Label)element).text = string.IsNullOrEmpty(asset.Type) ? asset.Path : $"{asset.Path}   ({asset.Type})";
                },
                itemsSource = _assets
            };
            _preview.AddToClassList("petrichor-preview");
            left.Add(_preview);

            _manifestId = new VisualElement();
            left.Add(_manifestId);

            // 3. package info
            right.Add(Ui.Heading("3. パッケージ情報"));

            _identifierKind = new PopupField<string>("識別子", IdentifierKinds.ToList(), 0);
            _identifierKind.AddToClassList("petrichor-field");
            _identifierKind.RegisterValueChangedCallback(_ => Validate());
            right.Add(_identifierKind);

            _identifier = Field("値");
            right.Add(_identifier);

            _identifierError = Ui.Text("", "petrichor-error-text");
            right.Add(_identifierError);

            _name = Field("名前");
            right.Add(_name);

            _description = Field("説明");
            _description.multiline = true;
            right.Add(_description);

            _version = Field("バージョン");
            right.Add(_version);

            right.Add(Ui.Text("送信されるのはアセットの GUID・パス・種類だけで、ファイルの中身は送られません。配布されていない (非公開の) アセットを含めないでください。", "petrichor-hint"));

            // 4. send
            right.Add(Ui.Heading("4. 送る"));

            _problems = new VisualElement();
            _problems.AddToClassList("petrichor-problems");
            right.Add(_problems);

            _submit = Ui.Button("登録する", Submit, "petrichor-primary");
            _save = Ui.Button("JSON を保存", SaveJson);
            var sendButtons = Ui.Row(_submit, _save);
            sendButtons.AddToClassList("petrichor-gap");
            right.Add(sendButtons);

            _submitState = new StateView();
            _submitState.ShowContent();
            right.Add(_submitState);
            _submitAsync = new AsyncView(this, _submitState);

            _result = new VisualElement();
            right.Add(_result);

            PetrichorServices.Auth.Changed += OnAuthChanged;
            Validate();
        }

        public override void OnShown()
        {
            Validate();
        }

        public override void OnClosed()
        {
            PetrichorServices.Auth.Changed -= OnAuthChanged;
            _sourceAsync.Cancel();
            _submitAsync.Cancel();
        }

        private void OnAuthChanged()
        {
            EditorApplication.delayCall += () => Validate();
        }

        public void SetFolder(string assetFolder)
        {
            _folder.SetValueWithoutNotify(AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetFolder));
            LoadSource(() => ManifestSources.FromFolder(assetFolder), ManifestSources.FindPackageJson(assetFolder), null);
        }

        private static VisualElement Column()
        {
            var column = new VisualElement();
            column.AddToClassList("petrichor-column");
            return column;
        }

        private TextField Field(string label)
        {
            var field = new TextField(label);
            field.AddToClassList("petrichor-field");
            field.RegisterValueChangedCallback(_ => Validate());
            return field;
        }

        private void RefreshPackageChoices()
        {
            _packageInfos = PackageInfo.GetAllRegisteredPackages().OrderBy(w => w.name, StringComparer.Ordinal).ToList();
            _packages.choices = new List<string> { "(選ぶ)" }.Concat(_packageInfos.Select(w => w.name)).ToList();
        }

        private void SetPackage(string name)
        {
            var package = _packageInfos.FirstOrDefault(w => w.name == name);
            if (package == null)
                return;

            LoadSource(() => ManifestSources.FromPackage(package), ManifestSources.FindPackageJson(package.assetPath), null);
        }

        private void SetSelection()
        {
            LoadSource(ManifestSources.FromSelection, null, null);
        }

        private void LoadSource(Func<List<ManifestAsset>> build, string packageJson, DetectedPackageInfo fallback)
        {
            try
            {
                SetAssets(build());
                Prefill(packageJson == null ? null : PackageInfoDetector.FromPackageJson(packageJson), fallback);
            }
            catch (Exception e)
            {
                SetAssets(new List<ManifestAsset>());
                _sourceState.ShowError(ErrorMessages.Describe(e), null);
            }
        }

        private void PickUnityPackage()
        {
            var path = EditorUtility.OpenFilePanel("unitypackage を選ぶ", "", "unitypackage");
            if (string.IsNullOrEmpty(path))
                return;

            var size = new FileInfo(path).Length;
            _sourceAsync.Run(async ct =>
            {
                _sourceState.ShowLoading($"{Path.GetFileName(path)} を読み込んでいます");
                var progress = new Progress<long>(read => _previewCount.text = $"読み込み中… {read / 1048576} / {size / 1048576} MB");

                var (assets, packageJson) = await Task.Run(() =>
                {
                    List<UnityPackageEntry> entries;
                    using (var stream = File.OpenRead(path))
                        entries = UnityPackageReader.Read(stream, progress, ct);

                    // a package.json at the shallowest level describes the package; reading it needs a second pass
                    var json = entries.Where(w => w.HasAsset && (w.Path == "package.json" || w.Path.EndsWith("/package.json", StringComparison.Ordinal)))
                                      .OrderBy(w => w.Path.Count(c => c == '/'))
                                      .FirstOrDefault();
                    string text = null;
                    if (json != null)
                        using (var stream = File.OpenRead(path))
                        {
                            var bytes = UnityPackageReader.ReadAsset(stream, json.Guid, ct);
                            text = bytes == null ? null : Encoding.UTF8.GetString(bytes);
                        }

                    return (ManifestValidator.Normalize(UnityPackageReader.ToManifestAssets(entries)), text);
                }, ct);

                if (!_sourceAsync.IsCurrent(ct))
                    return;

                _folder.SetValueWithoutNotify(null);
                SetAssets(assets);
                Prefill(packageJson == null ? null : PackageInfoDetector.FromPackageJson(packageJson), PackageInfoDetector.FromFileName(path));
            }, PickUnityPackage);
        }

        private void SetAssets(List<ManifestAsset> assets)
        {
            _assets = assets;
            _preview.itemsSource = _assets;
            _preview.Rebuild();
            _previewCount.text = $"アセット {_assets.Count:N0} 件";
            _result.Clear();

            _manifestId.Clear();
            if (_assets.Count > 0)
            {
                _manifestId.Add(Ui.Text("マニフェスト ID (サイトの表示と同じになります)", "petrichor-hint"));
                _manifestId.Add(new GuidLabel(ManifestHash.Compute(_assets)));
                _sourceState.ShowContent();
            }
            else
            {
                _sourceState.ShowEmpty("アセットが見つかりませんでした", "フォルダは登録の対象になりません。ファイルを含むフォルダを選んでください。");
            }

            Validate();
        }

        private void Prefill(DetectedPackageInfo fromPackageJson, DetectedPackageInfo fallback)
        {
            if (fromPackageJson != null && !string.IsNullOrWhiteSpace(fromPackageJson.Name))
            {
                _identifierKind.value = IdentifierKinds[0];
                _identifier.value = fromPackageJson.Name;
            }

            var info = fromPackageJson ?? fallback;
            if (info == null)
                return;

            var name = string.IsNullOrWhiteSpace(info.DisplayName) ? info.Name : info.DisplayName;
            if (!string.IsNullOrWhiteSpace(name))
                _name.value = name;

            if (!string.IsNullOrWhiteSpace(info.Version))
                _version.value = info.Version;

            if (!string.IsNullOrWhiteSpace(info.Description))
                _description.value = info.Description;
        }

        /// <summary>Builds the manifest from the form; <paramref name="identifierProblem" /> is set when the identifier cannot be used.</summary>
        private Manifest BuildManifest(out string identifierProblem)
        {
            identifierProblem = null;
            var value = _identifier.value?.Trim() ?? "";
            var name = _name.value;
            var description = _description.value;
            PackageSpec package;

            switch (Array.IndexOf(IdentifierKinds, _identifierKind.value))
            {
                case 0:
                    package = PackageSpec.ByIdentifier(IdentifierProvider.Package, value, name, description);
                    break;

                case 1:
                    if (!PackageIdentifier.TryParseBooth(value, out var itemId))
                    {
                        identifierProblem = "BOOTH の商品ページの URL を入力してください";
                        itemId = value;
                    }

                    package = PackageSpec.ByIdentifier(IdentifierProvider.Booth, itemId, name, description);
                    break;

                case 2:
                    var (provider, normalized) = PackageIdentifier.FromUrl(value);
                    package = PackageSpec.ByIdentifier(provider, normalized, name, description);
                    break;

                case 3:
                    package = PackageSpec.ById(value);
                    break;

                default:
                    package = PackageSpec.ByName(name, description);
                    break;
            }

            return new Manifest { Package = package, Version = _version.value?.Trim(), Assets = _assets };
        }

        private List<string> Validate()
        {
            var kind = Array.IndexOf(IdentifierKinds, _identifierKind.value);
            _identifier.style.display = kind == 4 ? DisplayStyle.None : DisplayStyle.Flex;
            _name.label = kind == 4 ? "名前" : "名前 (任意)";

            var manifest = BuildManifest(out var identifierProblem);
            _identifierError.text = identifierProblem ?? "";
            _identifierError.style.display = identifierProblem == null ? DisplayStyle.None : DisplayStyle.Flex;

            var problems = ManifestValidator.Validate(manifest);
            if (identifierProblem != null)
                problems.Insert(0, identifierProblem);

            _problems.Clear();
            foreach (var problem in problems.Take(ShownProblems))
                _problems.Add(Ui.Text(problem, "petrichor-error-text"));

            if (problems.Count > ShownProblems)
                _problems.Add(Ui.Text($"他 {problems.Count - ShownProblems} 件", "petrichor-error-text"));

            var loggedIn = PetrichorServices.Auth.IsLoggedIn;
            _submit.text = loggedIn ? "登録する" : "ログインして登録";
            _submit.SetEnabled(problems.Count == 0 && !_submitAsync.IsRunning);
            _save.SetEnabled(problems.Count == 0);

            return problems;
        }

        private void Submit()
        {
            if (!PetrichorServices.Auth.IsLoggedIn)
            {
                PetrichorWindow.Open(PetrichorTab.Settings);
                return;
            }

            if (Validate().Count > 0)
                return;

            var manifest = BuildManifest(out _);
            _result.Clear();

            _submitAsync.Run(async ct =>
            {
                _submitState.ShowLoading("送信しています");
                _submit.SetEnabled(false);

                SubmissionResult result;
                try
                {
                    result = await PetrichorServices.Client.SubmitAsync(manifest, ct);
                }
                catch (PetrichorApiException e) when (e.StatusCode == 404)
                {
                    throw new PetrichorApiException(404, "指定したパッケージが見つかりません", e.ServerCode);
                }
                finally
                {
                    EditorApplication.delayCall += () => Validate();
                }

                if (!_submitAsync.IsCurrent(ct))
                    return;

                _submitState.ShowContent();
                ShowResult(result);
            }, Submit);
        }

        private void ShowResult(SubmissionResult result)
        {
            var panel = new VisualElement();
            panel.AddToClassList("petrichor-panel");

            panel.Add(Ui.Text(result.Created ? "登録しました" : "登録済みの内容を再送しました", "petrichor-success-text", "petrichor-primary"));
            panel.Add(Ui.Text(result.Verified ? "検証済みになりました" : $"あと {Math.Max(0, 2 - result.SubmitterCount)} 人の登録で検証済みになります"));
            panel.Add(Ui.Link("Release を開く", PetrichorSettings.ReleaseUrl(result.ReleaseId)));
            panel.Add(Ui.Text("マニフェスト ID", "petrichor-hint"));
            panel.Add(new GuidLabel(result.ManifestId));

            _result.Add(panel);
        }

        private void SaveJson()
        {
            if (Validate().Count > 0)
                return;

            var manifest = BuildManifest(out _);
            var fileName = $"{SafeFileName(_name.value, "manifest")}-{SafeFileName(manifest.Version, "version")}.petrichor.json";
            var path = EditorUtility.SaveFilePanel("マニフェストを保存", "", fileName, "json");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                File.WriteAllText(path, manifest.ToJson(), new UTF8Encoding(false));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _submitState.ShowError($"保存できませんでした: {e.Message}", null);
                return;
            }

            if (EditorUtility.DisplayDialog("Petrichor", "マニフェストを保存しました。登録ページを開いてアップロードできます。", "登録ページを開く", "閉じる"))
                Application.OpenURL(PetrichorSettings.ContributeUrl);
        }

        private static string SafeFileName(string value, string fallback)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string((value ?? "").Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return safe.Length == 0 ? fallback : safe;
        }
    }
}
