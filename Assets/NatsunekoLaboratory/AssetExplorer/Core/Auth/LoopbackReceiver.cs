// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NatsunekoLaboratory.AssetExplorer.Auth
{
    /// <summary>
    ///     Receives the OAuth redirect on a fixed loopback address. The redirect URI must exactly match the one registered
    ///     for the Egeria application, so the port is fixed.
    /// </summary>
    internal sealed class LoopbackReceiver : IDisposable
    {
        public const int DefaultPort = 57193;
        public const string DefaultRedirectUri = "http://127.0.0.1:57193/callback";

        private readonly HttpListener _listener = new HttpListener();
        private readonly int _port;

        public LoopbackReceiver(int port = DefaultPort)
        {
            _port = port;
            // listen on the whole origin and match the path ourselves, so stray requests (favicon) get a 404
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        }

        public string CallbackUri => $"http://127.0.0.1:{_port}/callback";

        public void Dispose()
        {
            try
            {
                if (_listener.IsListening)
                    _listener.Stop();

                _listener.Close();
            }
            catch (ObjectDisposedException)
            {
                // already closed
            }
        }

        public void Start()
        {
            try
            {
                _listener.Start();
            }
            catch (Exception e) when (e is HttpListenerException || e is SocketException)
            {
                throw new OAuthException($"ポート {_port} が使用中です。他の Unity を閉じてからやり直してください", OAuthErrorKind.InvalidAuthorizationResponse);
            }
        }

        /// <summary>Waits for <c>GET /callback</c> and returns <see cref="CallbackUri" /> with the request's query.</summary>
        public async Task<Uri> WaitForCallbackAsync(TimeSpan timeout, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow + timeout;

            while (true)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    throw new TimeoutException("ログインがタイムアウトしました");

                var contextTask = _listener.GetContextAsync();
                using (var delay = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    var finished = await Task.WhenAny(contextTask, Task.Delay(remaining, delay.Token));
                    delay.Cancel();

                    if (finished != contextTask)
                    {
                        // stop listening so the pending GetContextAsync completes and nothing else is accepted
                        Dispose();
                        ct.ThrowIfCancellationRequested();
                        throw new TimeoutException("ログインがタイムアウトしました");
                    }
                }

                var context = await contextTask;
                if (context.Request.Url.AbsolutePath != "/callback")
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    continue;
                }

                var query = context.Request.Url.Query;
                Respond(context.Response, context.Request.QueryString["error"]);

                return new Uri(CallbackUri + query);
            }
        }

        private static void Respond(HttpListenerResponse response, string error)
        {
            var message = error == null ? "ログインしました。Unity に戻ってください。" : $"ログインできませんでした: {WebUtility.HtmlEncode(error)}";
            var html = "<!doctype html><html lang=\"ja\"><head><meta charset=\"utf-8\"><title>Petrichor</title>"
                       + "<style>body{font-family:system-ui,sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0}</style>"
                       + $"</head><body><p>{message}</p></body></html>";

            var bytes = Encoding.UTF8.GetBytes(html);
            response.StatusCode = 200;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }
    }
}
