// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using NatsunekoLaboratory.AssetExplorer.Auth;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Auth
{
    // Each test uses its own port so a slow shutdown never collides with the next test. 57193 is left for real logins.
    public class LoopbackReceiverTests
    {
        [Test]
        public void Returns_callback_uri()
        {
            using (var receiver = new LoopbackReceiver(57290))
            using (var http = new HttpClient())
            {
                receiver.Start();
                var wait = Task.Run(() => receiver.WaitForCallbackAsync(TimeSpan.FromSeconds(10), CancellationToken.None));

                var response = http.GetAsync("http://127.0.0.1:57290/callback?code=c&state=s").Result;
                var body = response.Content.ReadAsStringAsync().Result;
                var uri = wait.Result;

                Assert.That(receiver.CallbackUri, Is.EqualTo("http://127.0.0.1:57290/callback"));
                Assert.That(uri.ToString(), Is.EqualTo("http://127.0.0.1:57290/callback?code=c&state=s"));
                Assert.That(body, Does.Contain("ログインしました"));
            }
        }

        [Test]
        public void Error_callback_shows_error_page()
        {
            using (var receiver = new LoopbackReceiver(57291))
            using (var http = new HttpClient())
            {
                receiver.Start();
                var wait = Task.Run(() => receiver.WaitForCallbackAsync(TimeSpan.FromSeconds(10), CancellationToken.None));

                var body = http.GetStringAsync("http://127.0.0.1:57291/callback?error=access_denied&state=s").Result;

                Assert.That(wait.Result.Query, Does.Contain("error=access_denied"));
                Assert.That(body, Does.Contain("ログインできませんでした"));
            }
        }

        [Test]
        public void Ignores_other_paths()
        {
            using (var receiver = new LoopbackReceiver(57292))
            using (var http = new HttpClient())
            {
                receiver.Start();
                var wait = Task.Run(() => receiver.WaitForCallbackAsync(TimeSpan.FromSeconds(10), CancellationToken.None));

                var favicon = http.GetAsync("http://127.0.0.1:57292/favicon.ico").Result;
                Assert.That(favicon.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That(wait.IsCompleted, Is.False);

                http.GetAsync("http://127.0.0.1:57292/callback?code=c&state=s").Wait();
                Assert.That(wait.Result.Query, Does.Contain("code=c"));
            }
        }

        [Test]
        public void Times_out()
        {
            using (var receiver = new LoopbackReceiver(57293))
            {
                receiver.Start();

                Assert.Throws<TimeoutException>(() => AsyncHelper.Run(() => receiver.WaitForCallbackAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None)));
            }
        }

        [Test]
        public void Cancellation_stops_waiting()
        {
            using (var receiver = new LoopbackReceiver(57294))
            using (var cts = new CancellationTokenSource(100))
            {
                receiver.Start();

                Assert.Catch<OperationCanceledException>(() => AsyncHelper.Run(() => receiver.WaitForCallbackAsync(TimeSpan.FromSeconds(10), cts.Token)));
            }
        }

        [Test]
        public void Second_receiver_on_same_port_reports_port_in_use()
        {
            using (var first = new LoopbackReceiver(57295))
            using (var second = new LoopbackReceiver(57295))
            {
                first.Start();

                var e = Assert.Throws<OAuthException>(() => second.Start());
                Assert.That(e.Message, Is.EqualTo("ポート 57295 が使用中です。他の Unity を閉じてからやり直してください"));
            }
        }

        [Test]
        public void Default_redirect_uri_is_fixed()
        {
            Assert.That(LoopbackReceiver.DefaultPort, Is.EqualTo(57193));
            Assert.That(LoopbackReceiver.DefaultRedirectUri, Is.EqualTo("http://127.0.0.1:57193/callback"));
        }
    }
}
