// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Threading;

using NatsunekoLaboratory.AssetExplorer.Integration;
using NatsunekoLaboratory.AssetExplorer.UI.Components;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace NatsunekoLaboratory.AssetExplorer.UI.Tabs
{
    /// <summary>Login / logout and the endpoint overrides used for testing against staging.</summary>
    internal sealed class SettingsTab : TabView
    {
        private readonly VisualElement _account;
        private readonly Label _error;
        private CancellationTokenSource _login;

        public SettingsTab()
        {
            Add(Ui.Heading("アカウント"));
            _account = new VisualElement();
            Add(_account);

            _error = Ui.Text("", "petrichor-error-text");
            Add(_error);

            var advanced = new Foldout { text = "詳細設定", value = false };
            advanced.Add(Ui.Text("ステージング環境で試すときなどに変更します。空にすると既定値に戻ります。", "petrichor-hint"));
            advanced.Add(SettingField("API", nameof(PetrichorSettings.ApiBaseUrl), () => PetrichorSettings.ApiBaseUrl, v => PetrichorSettings.ApiBaseUrl = v));
            advanced.Add(SettingField("サイト", nameof(PetrichorSettings.WebBaseUrl), () => PetrichorSettings.WebBaseUrl, v => PetrichorSettings.WebBaseUrl = v));
            advanced.Add(SettingField("認可 URL", nameof(PetrichorSettings.AuthorizeUrl), () => PetrichorSettings.AuthorizeUrl, v => PetrichorSettings.AuthorizeUrl = v));
            advanced.Add(SettingField("Client ID", nameof(PetrichorSettings.ClientId), () => PetrichorSettings.ClientId, v => PetrichorSettings.ClientId = v));

            var redirect = new TextField("リダイレクト URI") { value = PetrichorSettings.RedirectUri, isReadOnly = true };
            redirect.AddToClassList("petrichor-field");
            MonoFont.Apply(redirect);
            advanced.Add(redirect);

            advanced.Add(Ui.Row(new VisualElement { style = { flexGrow = 1 } }, Ui.Button("すべて既定に戻す", () =>
            {
                PetrichorSettings.ResetAll();
                Rebuild(advanced);
            })));
            Add(advanced);

            PetrichorServices.Auth.Changed += OnChanged;
            PetrichorSettings.Changed += OnChanged;
            RenderAccount();
        }

        public override void OnShown()
        {
            RenderAccount();
        }

        public override void OnClosed()
        {
            PetrichorServices.Auth.Changed -= OnChanged;
            PetrichorSettings.Changed -= OnChanged;
            _login?.Cancel();
        }

        private void OnChanged()
        {
            EditorApplication.delayCall += RenderAccount;
        }

        private static VisualElement SettingField(string label, string name, Func<string> get, Action<string> set)
        {
            var field = new TextField(label) { value = get(), isDelayed = true, userData = get };
            field.AddToClassList("petrichor-field");
            field.AddToClassList("petrichor-grow");
            field.RegisterValueChangedCallback(e =>
            {
                set(e.newValue);
                field.SetValueWithoutNotify(get());
            });

            var reset = Ui.Button("リセット", () =>
            {
                PetrichorSettings.Reset(name);
                field.SetValueWithoutNotify(get());
            }, "petrichor-icon-button");

            return Ui.Row(field, reset);
        }

        private static void Rebuild(VisualElement advanced)
        {
            advanced.Query<TextField>().ForEach(field =>
            {
                if (field.userData is Func<string> get)
                    field.SetValueWithoutNotify(get());
            });
        }

        private void RenderAccount()
        {
            _account.Clear();
            var auth = PetrichorServices.Auth;

            if (_login != null)
            {
                _account.Add(Ui.Text("ブラウザでログインして、アクセスを許可してください…", "petrichor-hint"));
                _account.Add(Ui.Row(Ui.Button("キャンセル", () => _login?.Cancel())));
                return;
            }

            if (auth.IsLoggedIn)
            {
                var name = string.IsNullOrEmpty(auth.Current.ScreenName) ? "ログイン中" : $"{auth.Current.DisplayName} (@{auth.Current.ScreenName})";
                _account.Add(Ui.Row(Ui.Text(name, "petrichor-primary", "petrichor-grow"), Ui.Button("ログアウト", Logout)));
                _account.Add(Ui.Text("ログアウトしてもアプリの連携は残ります。連携の解除は accounts.natsuneko.com の「連携中のアプリ」から行えます。", "petrichor-hint"));
                return;
            }

            if (!PetrichorServices.HasClientId)
            {
                var button = Ui.Button("ログイン", Login);
                button.SetEnabled(false);
                _account.Add(Ui.Row(button));
                _account.Add(Ui.Text("Client ID 未設定のため、まだログインできません。詳細設定で Client ID を入力してください。", "petrichor-warning-text"));
                return;
            }

            _account.Add(Ui.Row(Ui.Button("ログイン", Login, "petrichor-primary")));
            _account.Add(Ui.Text($"ブラウザが開きます。ログインして許可すると {PetrichorSettings.RedirectUri} に戻ってきます。直接登録と「自分の登録」に使います。", "petrichor-hint"));
        }

        private async void Login()
        {
            if (_login != null)
                return;

            var login = new CancellationTokenSource();
            _login = login;
            _error.text = "";
            RenderAccount();

            try
            {
                await PetrichorServices.Auth.LoginAsync(uri => Application.OpenURL(uri.OriginalString), login.Token);

                try
                {
                    PetrichorServices.Auth.SetProfile(await PetrichorServices.Client.GetMeAsync(login.Token));
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    // the login itself succeeded; the account name is only for display
                    Debug.LogWarning($"Petrichor: could not load the account name: {e.Message}");
                }
            }
            catch (OperationCanceledException) when (login.IsCancellationRequested)
            {
                // cancelled from the UI or the window closed
            }
            catch (Exception e)
            {
                _error.text = ErrorMessages.Describe(e);
            }
            finally
            {
                if (_login == login)
                    _login = null;

                RenderAccount();
            }
        }

        private void Logout()
        {
            PetrichorServices.Auth.Logout();
            _error.text = "";
            RenderAccount();
        }
    }
}
