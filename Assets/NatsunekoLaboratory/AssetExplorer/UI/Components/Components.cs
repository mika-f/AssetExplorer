// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;

using NatsunekoLaboratory.AssetExplorer.Api;
using NatsunekoLaboratory.AssetExplorer.Integration;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace NatsunekoLaboratory.AssetExplorer.UI.Components
{
    internal static class MonoFont
    {
        private static Font _font;
        private static bool _loaded;

        /// <summary>Uses the editor's monospace font for GUIDs and paths, when the editor ships one.</summary>
        public static T Apply<T>(T element) where T : VisualElement
        {
            if (!_loaded)
            {
                _loaded = true;
                _font = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font;
            }

            element.AddToClassList("petrichor-mono");
            if (_font != null)
                element.style.unityFontDefinition = new StyleFontDefinition(_font);

            return element;
        }
    }

    internal static class Ui
    {
        public static Label Text(string text, params string[] classes)
        {
            var label = new Label(text);
            foreach (var c in classes)
                label.AddToClassList(c);

            return label;
        }

        public static Label Link(string text, string url, params string[] classes)
        {
            var label = Text(text, classes);
            label.AddToClassList("petrichor-link");
            label.tooltip = url;
            label.RegisterCallback<ClickEvent>(_ => Application.OpenURL(url));
            return label;
        }

        public static Label Heading(string text)
        {
            return Text(text, "petrichor-heading");
        }

        public static VisualElement Row(params VisualElement[] children)
        {
            var row = new VisualElement();
            row.AddToClassList("petrichor-row");
            foreach (var child in children)
                row.Add(child);

            return row;
        }

        public static Button Button(string text, Action onClick, params string[] classes)
        {
            var button = new Button(onClick) { text = text };
            foreach (var c in classes)
                button.AddToClassList(c);

            return button;
        }
    }

    internal sealed class StatusBadge : Label
    {
        public StatusBadge(ReleaseStatus status)
        {
            AddToClassList("petrichor-badge");
            switch (status)
            {
                case ReleaseStatus.Verified:
                    text = "検証済み";
                    AddToClassList("petrichor-badge--verified");
                    tooltip = "2 人以上が同じ内容を登録しています";
                    break;

                case ReleaseStatus.Legacy:
                    text = "Legacy";
                    AddToClassList("petrichor-badge--legacy");
                    tooltip = "以前の AssetDatabase から移行したデータです";
                    break;

                default:
                    text = "未検証";
                    AddToClassList("petrichor-badge--unverified");
                    tooltip = "まだ 1 人しか登録していません";
                    break;
            }
        }

        public static Label Neutral(string text)
        {
            var label = new Label(text);
            label.AddToClassList("petrichor-badge");
            label.AddToClassList("petrichor-badge--neutral");
            return label;
        }
    }

    /// <summary>One release: package name and version (both linking to the site), status, and optional trailing text.</summary>
    internal sealed class ReleaseRow : VisualElement
    {
        public ReleaseRow(ReleaseSummary release, string trailingText = null)
        {
            AddToClassList("petrichor-list-row");
            if (release.Status == ReleaseStatus.Unverified)
                AddToClassList("petrichor-list-row--muted");

            Add(Ui.Link(release.Package?.Name ?? "(名前なし)", PetrichorSettings.PackageUrl(release.Package?.Id), "petrichor-release-package"));
            Add(Ui.Link(release.Version ?? "", PetrichorSettings.ReleaseUrl(release.Id), "petrichor-release-version"));
            Add(new StatusBadge(release.Status));
            Add(Ui.Text($"{release.SubmitterCount} 人", "petrichor-muted"));

            if (!string.IsNullOrEmpty(trailingText))
                Add(Ui.Text(trailingText, "petrichor-release-trailing", "petrichor-muted"));
        }
    }

    /// <summary>A GUID (or path) that can be selected and copied.</summary>
    internal sealed class GuidLabel : VisualElement
    {
        public GuidLabel(string text)
        {
            AddToClassList("petrichor-guid");

            var field = new TextField { value = text, isReadOnly = true };
            MonoFont.Apply(field);
            Add(field);
            Add(Ui.Button("コピー", () => EditorGUIUtility.systemCopyBuffer = text, "petrichor-icon-button"));
        }
    }

    internal sealed class Section : VisualElement
    {
        public Section(string title)
        {
            Title = Ui.Heading(title);
            Add(Title);

            Content = new VisualElement();
            Add(Content);
        }

        public Label Title { get; }

        public VisualElement Content { get; }
    }

    /// <summary>Idle, loading, empty and error states shown in place of a result.</summary>
    internal sealed class StateView : VisualElement
    {
        private IVisualElementScheduledItem _dots;

        public StateView()
        {
            AddToClassList("petrichor-state");
        }

        public void ShowIdle(string hint)
        {
            Reset();
            Add(Ui.Text(hint, "petrichor-state-body"));
        }

        public void ShowLoading(string message)
        {
            Reset();
            var label = Ui.Text(message, "petrichor-state-body");
            Add(label);

            var step = 0;
            _dots = schedule.Execute(() => label.text = message + new string('.', step++ % 4)).Every(300);
        }

        public void ShowEmpty(string title, string body, string actionLabel = null, Action action = null)
        {
            Reset();
            Add(Ui.Text(title, "petrichor-state-title"));
            if (!string.IsNullOrEmpty(body))
                Add(Ui.Text(body, "petrichor-state-body"));

            if (actionLabel != null && action != null)
                Add(Ui.Button(actionLabel, action));
        }

        public void ShowError(string message, Action retry)
        {
            Reset();
            Add(Ui.Text(message, "petrichor-state-title", "petrichor-error-text"));
            if (retry != null)
                Add(Ui.Button("再試行", retry));
        }

        public void ShowContent()
        {
            Reset();
            style.display = DisplayStyle.None;
        }

        private void Reset()
        {
            _dots?.Pause();
            _dots = null;
            Clear();
            style.display = DisplayStyle.Flex;
        }
    }
}
