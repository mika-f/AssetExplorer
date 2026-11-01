// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Globalization;

using NatsunekoLaboratory.AssetExplorer.Api;
using NatsunekoLaboratory.AssetExplorer.Integration;
using NatsunekoLaboratory.AssetExplorer.UI.Components;

using UnityEditor;

using UnityEngine.UIElements;

namespace NatsunekoLaboratory.AssetExplorer.UI.Tabs
{
    /// <summary>The logged-in user's submissions, newest first.</summary>
    internal sealed class SubmissionsTab : TabView
    {
        private readonly AsyncView _async;
        private readonly VisualElement _list;
        private readonly StateView _state;
        private bool _loaded;

        public SubmissionsTab()
        {
            Add(Ui.Row(Ui.Text("自分が登録したパッケージとバージョンです。", "petrichor-hint", "petrichor-grow"), Ui.Button("再読み込み", Load)));

            _state = new StateView();
            Add(_state);

            _list = new VisualElement();
            Add(_list);

            _async = new AsyncView(_state);
            PetrichorServices.Auth.Changed += OnAuthChanged;
        }

        public override void OnShown()
        {
            if (!_loaded)
                Load();
        }

        public override void OnClosed()
        {
            PetrichorServices.Auth.Changed -= OnAuthChanged;
            _async.Cancel();
        }

        private void OnAuthChanged()
        {
            EditorApplication.delayCall += () =>
            {
                _loaded = false;
                if (panel != null)
                    Load();
            };
        }

        private void Load()
        {
            _list.Clear();

            if (!PetrichorServices.Auth.IsLoggedIn)
            {
                _async.Cancel();
                _state.ShowEmpty("ログインすると自分の登録を見られます", null, "ログイン", () => PetrichorWindow.Open(PetrichorTab.Settings));
                return;
            }

            _async.Run(async ct =>
            {
                _state.ShowLoading("読み込んでいます");

                var submissions = await PetrichorServices.Client.GetMySubmissionsAsync(ct);
                if (!_async.IsCurrent(ct))
                    return;

                _loaded = true;
                if (submissions.Count == 0)
                {
                    _state.ShowEmpty("まだ登録していません", "登録タブからパッケージを登録できます。", "登録タブを開く", () => PetrichorWindow.Open(PetrichorTab.Contribute));
                    return;
                }

                _state.ShowContent();
                foreach (var submission in submissions)
                    _list.Add(Row(submission));
            }, Load);
        }

        private static VisualElement Row(Submission submission)
        {
            var release = submission.Release;
            var row = new VisualElement();
            row.AddToClassList("petrichor-list-row");
            if (release.Status == ReleaseStatus.Unverified)
                row.AddToClassList("petrichor-list-row--muted");

            row.Add(Ui.Link(release.Package?.Name ?? "(名前なし)", PetrichorSettings.PackageUrl(release.Package?.Id), "petrichor-release-package"));
            row.Add(Ui.Link(submission.Version ?? release.Version ?? "", PetrichorSettings.ReleaseUrl(release.Id), "petrichor-release-version"));
            row.Add(new StatusBadge(release.Status));
            row.Add(StatusBadge.Neutral(submission.Source ?? ""));
            row.Add(Ui.Text(FormatDate(submission.CreatedAt), "petrichor-release-trailing", "petrichor-muted"));
            return row;
        }

        private static string FormatDate(string iso)
        {
            return DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) ? date.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture) : iso ?? "";
        }
    }
}
