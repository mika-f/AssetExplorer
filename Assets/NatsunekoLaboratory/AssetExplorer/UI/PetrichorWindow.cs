// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using NatsunekoLaboratory.AssetExplorer.Integration;
using NatsunekoLaboratory.AssetExplorer.UI.Tabs;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace NatsunekoLaboratory.AssetExplorer.UI
{
    internal enum PetrichorTab
    {
        Lookup,

        Missing,

        Contribute,

        Submissions,

        Settings
    }

    /// <summary>A page of the window. Views are created once and kept while the window is open.</summary>
    internal abstract class TabView : VisualElement
    {
        protected TabView()
        {
            AddToClassList("petrichor-page");
        }

        public virtual void OnShown() { }

        public virtual void OnClosed() { }
    }

    internal sealed class PetrichorWindow : EditorWindow
    {
        public const string StyleSheetGuid = "d829ffb939564b6494ecb4a122c9253a";

        private static readonly (PetrichorTab Tab, string Label)[] Tabs =
        {
            (PetrichorTab.Lookup, "検索"),
            (PetrichorTab.Missing, "Missing"),
            (PetrichorTab.Contribute, "登録"),
            (PetrichorTab.Submissions, "自分の登録"),
            (PetrichorTab.Settings, "設定")
        };

        private readonly Dictionary<PetrichorTab, Button> _buttons = new Dictionary<PetrichorTab, Button>();
        private readonly Dictionary<PetrichorTab, TabView> _views = new Dictionary<PetrichorTab, TabView>();

        private Button _account;
        private ScrollView _body;

        [SerializeField]
        private PetrichorTab _tab;

        public void CreateGUI()
        {
            var root = rootVisualElement;
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(StyleSheetGuid));
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            root.AddToClassList("petrichor-root");

            var topbar = new VisualElement();
            topbar.AddToClassList("petrichor-topbar");
            root.Add(topbar);

            var title = new Label("Petrichor");
            title.AddToClassList("petrichor-title");
            topbar.Add(title);

            var tabs = new VisualElement();
            tabs.AddToClassList("petrichor-tabs");
            topbar.Add(tabs);

            foreach (var (tab, label) in Tabs)
            {
                var button = new Button(() => Select(tab)) { text = label };
                button.AddToClassList("petrichor-tab");
                _buttons[tab] = button;
                tabs.Add(button);
            }

            _account = new Button(() => Select(PetrichorTab.Settings));
            _account.AddToClassList("petrichor-account");
            topbar.Add(_account);

            _body = new ScrollView(ScrollViewMode.Vertical);
            _body.AddToClassList("petrichor-body");
            root.Add(_body);

            UpdateAccount();
            Select(_tab);
        }

        public static PetrichorWindow Open(PetrichorTab tab)
        {
            var window = GetWindow<PetrichorWindow>();
            window.titleContent = new GUIContent("Petrichor");
            window.minSize = new Vector2(420, 360);
            window.Show();
            window.Select(tab);
            return window;
        }

        public T View<T>(PetrichorTab tab) where T : TabView
        {
            return (T)GetOrCreate(tab);
        }

        public void Select(PetrichorTab tab)
        {
            _tab = tab;
            if (_body == null)
                return;

            var view = GetOrCreate(tab);
            if (view.parent != _body.contentContainer)
            {
                _body.Clear();
                _body.Add(view);
            }

            foreach (var pair in _buttons)
                pair.Value.EnableInClassList("petrichor-tab--selected", pair.Key == tab);

            view.OnShown();
        }

        private void OnEnable()
        {
            PetrichorServices.Auth.Changed += OnAuthChanged;
        }

        private void OnDisable()
        {
            PetrichorServices.Auth.Changed -= OnAuthChanged;

            foreach (var view in _views.Values)
                view.OnClosed();
        }

        private void OnAuthChanged()
        {
            // the session may raise this from a continuation; touch the UI on the next editor tick
            EditorApplication.delayCall += UpdateAccount;
        }

        private void UpdateAccount()
        {
            if (_account == null)
                return;

            var auth = PetrichorServices.Auth;
            _account.text = !auth.IsLoggedIn ? "ログイン" : string.IsNullOrEmpty(auth.Current.ScreenName) ? "ログイン中" : "@" + auth.Current.ScreenName;
            _account.tooltip = auth.IsLoggedIn ? auth.Current.DisplayName : "設定タブからログインします";
        }

        private TabView GetOrCreate(PetrichorTab tab)
        {
            if (_views.TryGetValue(tab, out var view))
                return view;

            switch (tab)
            {
                case PetrichorTab.Lookup:
                    view = new LookupTab();
                    break;

                case PetrichorTab.Missing:
                    view = new MissingTab();
                    break;

                case PetrichorTab.Contribute:
                    view = new ContributeTab();
                    break;

                case PetrichorTab.Submissions:
                    view = new SubmissionsTab();
                    break;

                case PetrichorTab.Settings:
                    view = new SettingsTab();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(tab));
            }

            _views[tab] = view;
            return view;
        }
    }
}
