// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Auth
{
    /// <summary>
    ///     Records requests and answers them from a routing function. Bodies are captured as strings before the request is disposed.
    /// </summary>
    internal sealed class MockHandler : HttpMessageHandler
    {
        private readonly Func<RecordedRequest, HttpResponseMessage> _respond;

        public MockHandler(Func<RecordedRequest, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public List<RecordedRequest> Requests { get; } = new List<RecordedRequest>();

        /// <summary>Delays every response, to let concurrent callers overlap.</summary>
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;

        public static HttpResponseMessage Json(HttpStatusCode status, string json)
        {
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var recorded = new RecordedRequest
            {
                Method = request.Method.Method,
                Uri = request.RequestUri,
                Authorization = request.Headers.Authorization?.ToString(),
                UserAgent = request.Headers.UserAgent.ToString(),
                Body = request.Content == null ? null : await request.Content.ReadAsStringAsync()
            };

            lock (Requests)
                Requests.Add(recorded);

            if (Delay > TimeSpan.Zero)
                await Task.Delay(Delay, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            return _respond(recorded);
        }
    }

    internal sealed class RecordedRequest
    {
        public string Method { get; set; }

        public Uri Uri { get; set; }

        public string Authorization { get; set; }

        public string UserAgent { get; set; }

        public string Body { get; set; }
    }
}
