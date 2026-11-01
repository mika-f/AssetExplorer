// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using NatsunekoLaboratory.AssetExplorer.Api;
using NatsunekoLaboratory.AssetExplorer.Auth;

using UnityEngine;
using UnityEngine.UIElements;

namespace NatsunekoLaboratory.AssetExplorer.UI.Components
{
    /// <summary>
    ///     Runs one request at a time for a view: a new run cancels the previous one, results for a detached view are dropped,
    ///     and failures become an error state with a retry button.
    /// </summary>
    internal sealed class AsyncView
    {
        private readonly VisualElement _owner;
        private readonly StateView _state;
        private CancellationTokenSource _cts;

        public AsyncView(VisualElement owner, StateView state)
        {
            _owner = owner;
            _state = state;
        }

        public bool IsRunning => _cts != null;

        public void Cancel()
        {
            _cts?.Cancel();
            _cts = null;
        }

        /// <summary>
        ///     Runs <paramref name="work" /> on the main thread's async context. <paramref name="work" /> touches the UI only after awaiting;
        ///     it is skipped when the run was replaced or the view closed.
        /// </summary>
        public async void Run(Func<CancellationToken, Task> work, Action retry)
        {
            Cancel();
            var cts = new CancellationTokenSource();
            _cts = cts;

            try
            {
                await work(cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // replaced by a newer run, or the window closed
            }
            catch (Exception e)
            {
                if (!cts.IsCancellationRequested && _owner.panel != null)
                    _state.ShowError(ErrorMessages.Describe(e), retry);
            }
            finally
            {
                if (_cts == cts)
                    _cts = null;
            }
        }

        /// <summary>True when the result of <paramref name="ct" />'s run should still be shown.</summary>
        public bool IsCurrent(CancellationToken ct)
        {
            return !ct.IsCancellationRequested && _owner.panel != null;
        }
    }

    internal static class ErrorMessages
    {
        public static string Describe(Exception e)
        {
            switch (e)
            {
                case PetrichorApiException api when api.StatusCode == 401:
                    return "ログインが必要です";

                case PetrichorApiException api when api.StatusCode == 429:
                    return "本日の投稿上限 (100 件) に達しました";

                case PetrichorApiException api:
                    return api.Message;

                case AuthRequiredException auth:
                    return auth.Message;

                case OAuthException oauth when oauth.Kind == OAuthErrorKind.AuthorizationRequestFailed:
                    return $"ログインできませんでした: {oauth.ErrorDescription ?? oauth.Error}";

                case OAuthException oauth when oauth.Kind == OAuthErrorKind.InvalidTokenResponse:
                    return $"ログインできませんでした: {oauth.ErrorDescription ?? oauth.Error ?? "トークンを取得できませんでした"}";

                case OAuthException oauth:
                    return oauth.Message.StartsWith("Invalid", StringComparison.Ordinal) ? "ログインの応答が正しくありませんでした。もう一度やり直してください" : oauth.Message;

                case TimeoutException timeout:
                    return timeout.Message;

                case TaskCanceledException _:
                    return "応答がありませんでした。時間をおいてやり直してください";

                case HttpRequestException _:
                    return "Petrichor に接続できませんでした。ネットワークを確認してください";

                default:
                    Debug.LogException(e);
                    return "予期しないエラーが発生しました (詳細はコンソールを見てください)";
            }
        }
    }
}
