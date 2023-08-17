// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using NatsunekoLaboratory.AssetExplorer.Models;

using UnityEngine;

namespace NatsunekoLaboratory.AssetExplorer.Database
{
    internal class AssetDatabaseClient
    {
        private static AssetDatabaseClient _instance;
        private readonly string _baseUrl;
        private readonly HttpClient _client;


        public static AssetDatabaseClient Instance
        {
            get
            {
                if (_instance == null) _instance = new AssetDatabaseClient("https://assetdatabase.natsuneko.cat/api");

                return _instance;
            }
        }

        private AssetDatabaseClient(string baseUrl)
        {
            _client = new HttpClient();
            _baseUrl = baseUrl;
        }

        /// <summary>
        ///     what this asset is included in asset???
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public async Task<FindAssetByGuidResponse> FindAssetByGuid(string guid)
        {
            return await GetAsync<FindAssetByGuidResponse>($"/v1/assets/{guid}/");
        }

        #region Request

        private static string UrlEncode(string str)
        {
            const string reservedLetters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

            var sb = new StringBuilder();
            foreach (var b in Encoding.UTF8.GetBytes(str))
                if (reservedLetters.Contains(((char)b).ToString()))
                    sb.Append((char)b);
                else
                    sb.Append("%").AppendFormat("{0:X2}", b);
            return sb.ToString();
        }

        private static string NormalizeValue(object value)
        {
            switch (value)
            {
                case bool b:
                    return b.ToString().ToLowerInvariant();

                case float f:
                    return f.ToString(CultureInfo.InvariantCulture);

                case double d:
                    return d.ToString(CultureInfo.InvariantCulture);

                default:
                    return UrlEncode(value.ToString());
            }
        }

        private string BuildUrl(string endpoint, List<KeyValuePair<string, object>> parameters = null)
        {
            var sb = new StringBuilder(_baseUrl);
            sb.Append(endpoint);

            if (parameters != null && parameters.Any())
            {
                var query = string.Join("&", parameters.Select(w => $"{w.Key}=${NormalizeValue(w.Value)}"));
                sb.Append("?");
                sb.Append(query);
            }

            return sb.ToString();
        }

        private async Task<T> GetAsync<T>(string endpoint, List<KeyValuePair<string, object>> parameters = null) where T : class
        {
            var response = await GetAsync(endpoint, parameters);
            var str = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<T>(str);
        }

        private async Task<HttpResponseMessage> GetAsync(string endpoint, List<KeyValuePair<string, object>> parameters = null)
        {
            var response = await _client.GetAsync(BuildUrl(endpoint, parameters));

            response.EnsureSuccessStatusCode();
            return response;
        }

        private async Task<T> PostAsync<T>(string endpoint, List<KeyValuePair<string, object>> parameters = null, object body = null) where T : class
        {
            var response = await PostAsync(endpoint, parameters, body);
            var str = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<T>(str);
        }

        private async Task<HttpResponseMessage> PostAsync(string endpoint, List<KeyValuePair<string, object>> parameters = null, object body = null)
        {
            return await SendAsync(HttpMethod.Post, endpoint, parameters, body);
        }

        private async Task<T> PutAsync<T>(string endpoint, List<KeyValuePair<string, object>> parameters = null, object body = null) where T : class
        {
            var response = await PutAsync(endpoint, parameters, body);
            var str = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<T>(str);
        }

        private async Task<HttpResponseMessage> PutAsync(string endpoint, List<KeyValuePair<string, object>> parameters = null, object body = null)
        {
            return await SendAsync(HttpMethod.Put, endpoint, parameters, body);
        }

        private async Task<T> DeleteAsync<T>(string endpoint, List<KeyValuePair<string, object>> parameters = null) where T : class
        {
            var response = await DeleteAsync(endpoint, parameters);
            var str = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<T>(str);
        }

        private async Task<HttpResponseMessage> DeleteAsync(string endpoint, List<KeyValuePair<string, object>> parameters = null)
        {
            var response = await _client.DeleteAsync(BuildUrl(endpoint, parameters));

            response.EnsureSuccessStatusCode();
            return response;
        }

        private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string endpoint, List<KeyValuePair<string, object>> parameters = null, object body = null)
        {
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _client.SendAsync(new HttpRequestMessage(method, BuildUrl(endpoint, parameters)) { Content = content });

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                Debug.LogWarning(message);
            }

            response.EnsureSuccessStatusCode();
            return response;
        }

        #endregion
    }
}