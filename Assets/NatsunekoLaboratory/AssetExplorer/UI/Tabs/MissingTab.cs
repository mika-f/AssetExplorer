// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using NatsunekoLaboratory.AssetExplorer.Api;
using NatsunekoLaboratory.AssetExplorer.Integration;
using NatsunekoLaboratory.AssetExplorer.Scanning;
using NatsunekoLaboratory.AssetExplorer.UI.Components;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

using Object = UnityEngine.Object;

namespace NatsunekoLaboratory.AssetExplorer.UI.Tabs
{
    /// <summary>Finds Missing references and the packages that would resolve them.</summary>
    internal sealed class MissingTab : TabView
    {
        private readonly AsyncView _async;
        private readonly VisualElement _result;
        private readonly StateView _state;
        private readonly Label _status;
        private readonly ObjectField _target;

        public MissingTab()
        {
            Add(Ui.Text("プレハブやシーン、オブジェクトの Missing 参照を集めて、どのパッケージを入れれば直るかを調べます。送信されるのは GUID だけです。", "petrichor-hint"));

            _target = new ObjectField("対象") { objectType = typeof(Object), allowSceneObjects = true };
            _target.AddToClassList("petrichor-field");
            Add(_target);

            var actions = Ui.Row(Ui.Button("調べる", () => Scan(_target.value), "petrichor-primary"), Ui.Button("開いているシーン", ScanOpenScenes), Ui.Button("フォルダ…", PickFolder));
            actions.AddToClassList("petrichor-gap");
            Add(actions);

            _status = Ui.Text("", "petrichor-hint");
            Add(_status);

            _state = new StateView();
            _state.ShowIdle("対象を選んで「調べる」を押すか、開いているシーン・フォルダをまとめて調べます");
            Add(_state);

            _result = new VisualElement();
            Add(_result);

            _async = new AsyncView(_state);
        }

        /// <summary>True while a lookup is in flight.</summary>
        internal bool IsBusy => _async.IsRunning;

        public override void OnClosed()
        {
            _async.Cancel();
        }

        public void Scan(Object target)
        {
            if (target == null)
            {
                _state.ShowError("対象のオブジェクトかアセットを選んでください", null);
                return;
            }

            _target.SetValueWithoutNotify(target);
            Run(() => MissingReferenceScanner.ScanObject(target));
        }

        public void ScanOpenScenes()
        {
            Run(MissingReferenceScanner.ScanOpenScenes);
        }

        public void ScanFolder(string assetFolder)
        {
            _target.SetValueWithoutNotify(AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetFolder));
            Run(() => MissingReferenceScanner.ScanFolder(assetFolder));
        }

        private void PickFolder()
        {
            var absolute = EditorUtility.OpenFolderPanel("調べるフォルダを選ぶ", Application.dataPath, "");
            if (string.IsNullOrEmpty(absolute))
                return;

            var folder = ToAssetFolder(absolute);
            if (folder == null)
            {
                _state.ShowError("プロジェクトの Assets フォルダの中を選んでください", null);
                return;
            }

            ScanFolder(folder);
        }

        internal static string ToAssetFolder(string absolute)
        {
            var dataPath = Application.dataPath.Replace('\\', '/');
            absolute = absolute.Replace('\\', '/').TrimEnd('/');

            if (absolute == dataPath)
                return "Assets";

            return absolute.StartsWith(dataPath + "/", StringComparison.Ordinal) ? "Assets" + absolute.Substring(dataPath.Length) : null;
        }

        private void Run(Func<List<MissingReference>> collect)
        {
            // a new scan replaces the previous lookup, even when it finds nothing to look up
            _async.Cancel();
            _result.Clear();
            _status.text = "";

            List<MissingReference> references;
            try
            {
                references = collect();
            }
            catch (Exception e)
            {
                _state.ShowError(ErrorMessages.Describe(e), null);
                return;
            }

            if (references.Count == 0)
            {
                _state.ShowEmpty("Missing 参照はありません", "参照先はすべてプロジェクトにあります。");
                return;
            }

            _async.Run(async ct =>
            {
                _state.ShowLoading("照合しています");

                var batches = LookupMerger.Split(references.Select(w => w.Guid));
                var parts = new List<BulkLookup>();
                Exception failure = null;

                foreach (var batch in batches)
                {
                    _status.text = $"Missing 参照 {references.Count} 件 · 照合 {parts.Count}/{batches.Count}";
                    try
                    {
                        parts.Add(await PetrichorServices.Client.LookupAsync(batch, null, ct));
                    }
                    catch (Exception e) when (!ct.IsCancellationRequested)
                    {
                        failure = e;
                        break;
                    }

                    if (!_async.IsCurrent(ct))
                        return;
                }

                if (!_async.IsCurrent(ct))
                    return;

                _status.text = $"Missing 参照 {references.Count} 件 · 照合 {parts.Count}/{batches.Count}";
                if (parts.Count == 0 && failure != null)
                    throw failure;

                _state.ShowContent();
                Render(references, LookupMerger.Merge(parts), failure);
            }, () => Run(collect));
        }

        private void Render(List<MissingReference> references, BulkLookup lookup, Exception failure)
        {
            if (failure != null)
                _result.Add(Ui.Text($"一部のみ表示しています: {ErrorMessages.Describe(failure)}", "petrichor-warning-text"));

            if (lookup.Suggestions.Count > 0)
            {
                var suggestions = new Section("このパッケージを入れると解決します");
                foreach (var suggestion in lookup.Suggestions)
                    suggestions.Content.Add(new ReleaseRow(suggestion.Release, $"{suggestion.Matched} 件解決"));

                _result.Add(suggestions);
            }

            var found = references.Where(w => lookup.Results.ContainsKey(w.Guid)).ToList();
            if (found.Count > 0)
            {
                var results = new Section($"GUID ごとの結果 ({found.Count})");
                foreach (var reference in found)
                {
                    var foldout = new Foldout { text = $"{reference.Guid}  —  {reference.Locations[0]}", value = found.Count <= 3 };
                    foreach (var location in reference.Locations)
                        foldout.Add(Ui.Text(location, "petrichor-hint"));

                    foldout.Add(AssetLookupView.Paths(lookup.Results[reference.Guid]));
                    results.Content.Add(foldout);
                }

                _result.Add(results);
            }

            var missing = references.Where(w => lookup.Missing.Contains(w.Guid)).ToList();
            if (missing.Count > 0)
            {
                var section = new Section($"見つからなかった GUID ({missing.Count})");
                section.Content.Add(Ui.Row(Ui.Text("まだ誰も登録していないアセットです。", "petrichor-hint", "petrichor-grow"),
                                           Ui.Button("すべてコピー", () => EditorGUIUtility.systemCopyBuffer = string.Join("\n", missing.Select(w => w.Guid)))));

                foreach (var reference in missing)
                {
                    var row = new VisualElement();
                    row.AddToClassList("petrichor-list-row");
                    row.AddToClassList("petrichor-list-row--muted");
                    row.style.flexDirection = FlexDirection.Column;
                    row.style.alignItems = Align.Stretch;
                    row.Add(new GuidLabel(reference.Guid));
                    foreach (var location in reference.Locations)
                        row.Add(Ui.Text(location, "petrichor-hint"));

                    section.Content.Add(row);
                }

                _result.Add(section);
            }
        }
    }
}
