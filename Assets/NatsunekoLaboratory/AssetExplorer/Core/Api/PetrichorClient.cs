// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NatsunekoLaboratory.AssetExplorer.Auth;
using NatsunekoLaboratory.AssetExplorer.Contribute;
using NatsunekoLaboratory.AssetExplorer.Json;

namespace NatsunekoLaboratory.AssetExplorer.Api
{
    /// <summary>
    ///     The Petrichor v2 API (https://api.natsuneko.com/petrichor/v2). Lookups are public; submissions need a login.
    /// </summary>
    internal sealed class PetrichorClient
    {
        public const string PetrichorPath = "/petrichor/v2";

        private readonly Func<string> _apiBaseUrl;
        private readonly HttpClient _http;

        public PetrichorClient(HttpClient http, Func<string> apiBaseUrl, AuthSession auth)
        {
            _http = http;
            _apiBaseUrl = apiBaseUrl;
            Auth = auth;
        }

        /// <summary>Set by the editor integration to <c>Petrichor-Unity/&lt;version&gt; (Unity &lt;version&gt;)</c>.</summary>
        public static string UserAgent { get; set; } = "Petrichor-Unity";

        public AuthSession Auth { get; }

        private string ApiBase => _apiBaseUrl().TrimEnd('/');

        public async Task<AssetLookup> GetAssetAsync(string guid, CancellationToken ct)
        {
            var normalized = (guid ?? "").Trim().ToLowerInvariant();
            using (var response = await SendAsync(() => Request(HttpMethod.Get, $"{PetrichorPath}/assets/{Uri.EscapeDataString(normalized)}"), ct))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                return AssetLookup.FromJson(await ReadJsonAsync(response));
            }
        }

        /// <summary>
        ///     Looks up any number of GUIDs, 1,000 per request, one request at a time. Progress reports (finished batches, total batches).
        /// </summary>
        public async Task<BulkLookup> LookupAsync(IEnumerable<string> guids, IProgress<(int Done, int Total)> progress, CancellationToken ct)
        {
            var batches = LookupMerger.Split(guids);
            var parts = new List<BulkLookup>();

            if (batches.Count == 0)
                return new BulkLookup();

            progress?.Report((0, batches.Count));
            foreach (var batch in batches)
            {
                var body = JsonWriter.Write(new Dictionary<string, object> { { "guids", batch } });
                using (var response = await SendAsync(() => Request(HttpMethod.Post, $"{PetrichorPath}/assets/lookup", body), ct))
                    parts.Add(BulkLookup.FromJson(await ReadJsonAsync(response)));

                progress?.Report((parts.Count, batches.Count));
            }

            return LookupMerger.Merge(parts);
        }

        public async Task<SubmissionResult> SubmitAsync(Manifest manifest, CancellationToken ct)
        {
            var body = manifest.ToJson();
            using (var response = await Auth.SendAsync(_http, token => Request(HttpMethod.Post, $"{PetrichorPath}/submissions", body, token), ct))
            {
                var result = SubmissionResult.FromJson(await ReadJsonAsync(response));
                result.Created = response.StatusCode == HttpStatusCode.Created;
                return result;
            }
        }

        public async Task<List<Submission>> GetMySubmissionsAsync(CancellationToken ct)
        {
            using (var response = await Auth.SendAsync(_http, token => Request(HttpMethod.Get, $"{PetrichorPath}/me/submissions", null, token), ct))
                return Submission.ListFromJson(await ReadJsonAsync(response));
        }

        public async Task<User> GetMeAsync(CancellationToken ct)
        {
            using (var response = await Auth.SendAsync(_http, token => Request(HttpMethod.Get, "/egeria/v1/me", null, token), ct))
                return User.FromJson(await ReadJsonAsync(response));
        }

        private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> createRequest, CancellationToken ct)
        {
            using (var request = createRequest())
                return await _http.SendAsync(request, ct);
        }

        private HttpRequestMessage Request(HttpMethod method, string path, string jsonBody = null, string accessToken = null)
        {
            var request = new HttpRequestMessage(method, ApiBase + path);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (accessToken != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            if (jsonBody != null)
                request.Content = new StringContent(jsonBody, new UTF8Encoding(false), "application/json");

            return request;
        }

        /// <summary>Returns the parsed body of a 2xx response; anything else becomes a <see cref="PetrichorApiException" />.</summary>
        private static async Task<object> ReadJsonAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw PetrichorApiException.FromResponse((int)response.StatusCode, response.ReasonPhrase, body);

            try
            {
                return JsonReader.Parse(body);
            }
            catch (JsonException)
            {
                throw new PetrichorApiException((int)response.StatusCode, "サーバーの応答を読み込めませんでした", null);
            }
        }
    }
}
