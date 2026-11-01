// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

using NatsunekoLaboratory.AssetExplorer.Integration;
using NatsunekoLaboratory.AssetExplorer.UI.Components;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

using Object = UnityEngine.Object;

namespace NatsunekoLaboratory.AssetExplorer.UI.Tabs
{
    /// <summary>Looks up which packages and versions contain one GUID.</summary>
    internal sealed class LookupTab : TabView
    {
        private static readonly Regex GuidPattern = new Regex("^[0-9a-fA-F]{32}$");

        private readonly AsyncView _async;
        private readonly TextField _guid;
        private readonly VisualElement _result;
        private readonly StateView _state;

        public LookupTab()
        {
            Add(Ui.Text("GUID から、そのアセットを含むパッケージとバージョンを調べます。送信されるのは GUID だけです。", "petrichor-hint"));

            _guid = new TextField("GUID");
            _guid.AddToClassList("petrichor-field");
            MonoFont.Apply(_guid);
            _guid.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    Search();
            }, TrickleDown.TrickleDown);
            Add(_guid);

            var asset = new ObjectField("アセット") { objectType = typeof(Object), allowSceneObjects = false };
            asset.AddToClassList("petrichor-field");
            asset.RegisterValueChangedCallback(e =>
            {
                var path = e.newValue == null ? null : AssetDatabase.GetAssetPath(e.newValue);
                if (string.IsNullOrEmpty(path))
                    return;

                _guid.value = AssetDatabase.AssetPathToGUID(path);
                Search();
            });
            Add(asset);

            Add(Ui.Row(new VisualElement { style = { flexGrow = 1 } }, Ui.Button("検索", Search, "petrichor-primary")));

            _state = new StateView();
            _state.ShowIdle("GUID を入力するか、アセットを選んでください");
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

        public void Search()
        {
            // a new search replaces the previous one, even when it stops at validation
            _async.Cancel();
            var guid = _guid.value?.Trim() ?? "";
            _result.Clear();

            if (!GuidPattern.IsMatch(guid))
            {
                _state.ShowError("32 桁の 16 進数の GUID を入力してください", null);
                return;
            }

            _async.Run(async ct =>
            {
                _state.ShowLoading("調べています");

                var asset = await PetrichorServices.Client.GetAssetAsync(guid, ct);
                if (!_async.IsCurrent(ct))
                    return;

                if (asset == null || asset.Paths.Count == 0)
                {
                    _state.ShowEmpty("この GUID はまだ登録されていません", "パッケージをお持ちなら、登録タブから登録できます。", "登録タブを開く", () => PetrichorWindow.Open(PetrichorTab.Contribute));
                    return;
                }

                _state.ShowContent();
                _result.Add(Ui.Row(new GuidLabel(asset.Guid) { style = { flexGrow = 1 } }, Ui.Link("サイトで開く", PetrichorSettings.GuidUrl(asset.Guid))));
                _result.Add(AssetLookupView.Paths(asset));
            }, Search);
        }
    }
}
