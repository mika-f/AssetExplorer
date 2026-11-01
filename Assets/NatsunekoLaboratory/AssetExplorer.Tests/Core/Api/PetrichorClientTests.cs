// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;

using NatsunekoLaboratory.AssetExplorer.Api;
using NatsunekoLaboratory.AssetExplorer.Auth;
using NatsunekoLaboratory.AssetExplorer.Contribute;
using NatsunekoLaboratory.AssetExplorer.Json;
using NatsunekoLaboratory.AssetExplorer.Tests.Auth;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Api
{
    public class PetrichorClientTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

        private MockHandler _handler;
        private Func<RecordedRequest, HttpResponseMessage> _respond;
        private MemoryTokenStorage _storage;
        private PetrichorClient _client;

        [SetUp]
        public void SetUp()
        {
            _respond = _ => MockHandler.Json(HttpStatusCode.OK, "{}");
            _handler = new MockHandler(r => _respond(r));
            _storage = new MemoryTokenStorage();
            var http = new HttpClient(_handler);
            var auth = new AuthSession(_storage, () => new OAuthClient("c", "https://accounts.test/authorize", "https://api.test/token", http, () => Now), () => Now);
            _client = new PetrichorClient(http, () => "https://api.test/", auth);
        }

        private void Login()
        {
            _storage.Save(new StoredToken { AccessToken = "at", RefreshToken = "rt", ExpiresAt = Now.AddDays(3) });
            _client.Auth.Reload();
        }

        private static Manifest Manifest()
        {
            return new Manifest { Package = PackageSpec.ByName("P", null), Version = "1.0", Assets = new List<ManifestAsset> { new ManifestAsset(new string('a', 32), "Assets/A.mat", null) } };
        }

        [Test]
        public void GetAsset_returns_null_on_404()
        {
            _respond = _ => MockHandler.Json(HttpStatusCode.NotFound, "{\"message\":\"not found\"}");

            Assert.That(AsyncHelper.Run(() => _client.GetAssetAsync(new string('a', 32), CancellationToken.None)), Is.Null);
        }

        [Test]
        public void GetAsset_hits_lowercased_guid_url()
        {
            _respond = _ => MockHandler.Json(HttpStatusCode.OK, "{\"guid\":\"" + new string('a', 32) + "\",\"paths\":[]}");

            var asset = AsyncHelper.Run(() => _client.GetAssetAsync(" " + new string('A', 32) + " ", CancellationToken.None));

            Assert.That(_handler.Requests.Single().Uri.ToString(), Is.EqualTo("https://api.test/petrichor/v2/assets/" + new string('a', 32)));
            Assert.That(_handler.Requests.Single().Authorization, Is.Null);
            Assert.That(_handler.Requests.Single().UserAgent, Does.StartWith("Petrichor-Unity"));
            Assert.That(asset.Guid, Is.EqualTo(new string('a', 32)));
        }

        [Test]
        public void GetAsset_500_throws_api_exception()
        {
            _respond = _ => MockHandler.Json(HttpStatusCode.InternalServerError, "{\"message\":\"boom\"}");

            var e = Assert.Throws<PetrichorApiException>(() => AsyncHelper.Run(() => _client.GetAssetAsync(new string('a', 32), CancellationToken.None)));
            Assert.That(e.StatusCode, Is.EqualTo(500));
            Assert.That(e.Message, Is.EqualTo("boom"));
        }

        [Test]
        public void Lookup_sends_batches_sequentially_and_reports_progress()
        {
            _respond = r =>
            {
                var guids = JsonReader.Parse(r.Body).Arr("guids").Cast<string>();
                return MockHandler.Json(HttpStatusCode.OK, "{\"results\":{},\"missing\":[" + string.Join(",", guids.Select(w => $"\"{w}\"")) + "],\"suggestions\":[]}");
            };
            var reports = new List<(int, int)>();
            var progress = new SyncProgress<(int Done, int Total)>(w => reports.Add(w));

            var result = AsyncHelper.Run(() => _client.LookupAsync(Enumerable.Range(0, 2500).Select(i => i.ToString("x32")), progress, CancellationToken.None));

            Assert.That(_handler.Requests.Select(w => JsonReader.Parse(w.Body).Arr("guids").Count), Is.EqualTo(new[] { 1000, 1000, 500 }));
            Assert.That(_handler.Requests.All(w => w.Method == "POST" && w.Uri.ToString() == "https://api.test/petrichor/v2/assets/lookup"), Is.True);
            Assert.That(reports, Is.EqualTo(new[] { (0, 3), (1, 3), (2, 3), (3, 3) }));
            Assert.That(result.Missing.Count, Is.EqualTo(2500));
        }

        [Test]
        public void Lookup_with_no_guids_sends_nothing()
        {
            var result = AsyncHelper.Run(() => _client.LookupAsync(new string[0], null, CancellationToken.None));

            Assert.That(_handler.Requests, Is.Empty);
            Assert.That(result.Results, Is.Empty);
        }

        [Test]
        public void Submit_sends_bearer_and_body()
        {
            Login();
            _respond = _ => MockHandler.Json(HttpStatusCode.OK, "{\"releaseId\":\"r\",\"manifestId\":\"m\",\"verified\":false,\"submitterCount\":1}");

            var result = AsyncHelper.Run(() => _client.SubmitAsync(Manifest(), CancellationToken.None));

            var request = _handler.Requests.Single();
            Assert.That(request.Uri.ToString(), Is.EqualTo("https://api.test/petrichor/v2/submissions"));
            Assert.That(request.Authorization, Is.EqualTo("Bearer at"));
            Assert.That(JsonReader.Parse(request.Body).Str("format"), Is.EqualTo("petrichor/v1"));
            Assert.That(result.Created, Is.False);
            Assert.That(result.ReleaseId, Is.EqualTo("r"));
        }

        [Test]
        public void Submit_maps_201_to_Created()
        {
            Login();
            _respond = _ => MockHandler.Json(HttpStatusCode.Created, "{\"releaseId\":\"r\",\"manifestId\":\"m\",\"verified\":true,\"submitterCount\":2}");

            Assert.That(AsyncHelper.Run(() => _client.SubmitAsync(Manifest(), CancellationToken.None)).Created, Is.True);
        }

        [Test]
        public void Submit_429_throws_with_status()
        {
            Login();
            _respond = _ => MockHandler.Json((HttpStatusCode)429, "{\"error\":{\"message\":\"limit\",\"code\":429}}");

            var e = Assert.Throws<PetrichorApiException>(() => AsyncHelper.Run(() => _client.SubmitAsync(Manifest(), CancellationToken.None)));
            Assert.That(e.StatusCode, Is.EqualTo(429));
        }

        [Test]
        public void Submit_without_login_throws_AuthRequired()
        {
            Assert.Throws<AuthRequiredException>(() => AsyncHelper.Run(() => _client.SubmitAsync(Manifest(), CancellationToken.None)));
            Assert.That(_handler.Requests, Is.Empty);
        }

        [Test]
        public void MySubmissions_parses_array()
        {
            Login();
            _respond = _ => MockHandler.Json(HttpStatusCode.OK, "[{\"id\":\"s1\",\"version\":\"1\",\"source\":\"API\",\"createdAt\":\"x\",\"release\":" + ModelsTests.ReleaseJson + "}]");

            var list = AsyncHelper.Run(() => _client.GetMySubmissionsAsync(CancellationToken.None));

            Assert.That(_handler.Requests.Single().Uri.ToString(), Is.EqualTo("https://api.test/petrichor/v2/me/submissions"));
            Assert.That(list.Single().Id, Is.EqualTo("s1"));
        }

        [Test]
        public void Me_reads_user()
        {
            Login();
            _respond = _ => MockHandler.Json(HttpStatusCode.OK, "{\"user\":{\"screenName\":\"n\",\"displayName\":\"d\"}}");

            var user = AsyncHelper.Run(() => _client.GetMeAsync(CancellationToken.None));

            Assert.That(_handler.Requests.Single().Uri.ToString(), Is.EqualTo("https://api.test/egeria/v1/me"));
            Assert.That(user.ScreenName, Is.EqualTo("n"));
        }

        [Test]
        public void Cancelled_request_throws_OperationCanceled()
        {
            _handler.Delay = TimeSpan.FromSeconds(5);
            var cts = new CancellationTokenSource(50);

            Assert.Catch<OperationCanceledException>(() => AsyncHelper.Run(() => _client.GetAssetAsync(new string('a', 32), cts.Token)));
        }

        private sealed class SyncProgress<T> : IProgress<T>
        {
            private readonly Action<T> _handler;

            public SyncProgress(Action<T> handler)
            {
                _handler = handler;
            }

            public void Report(T value)
            {
                _handler(value);
            }
        }
    }
}
