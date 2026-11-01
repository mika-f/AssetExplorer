// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Globalization;

using NatsunekoLaboratory.AssetExplorer.Json;

namespace NatsunekoLaboratory.AssetExplorer.Api
{
    internal sealed class PetrichorApiException : Exception
    {
        public PetrichorApiException(int statusCode, string message, string serverCode) : base(message)
        {
            StatusCode = statusCode;
            ServerCode = serverCode;
        }

        public int StatusCode { get; }

        public string ServerCode { get; }

        /// <summary>
        ///     Error bodies are <c>{ error: { message, code } }</c> or <c>{ message }</c>; anything else falls back to the reason phrase.
        /// </summary>
        public static PetrichorApiException FromResponse(int status, string reasonPhrase, string body)
        {
            object json = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(body))
                    json = JsonReader.Parse(body);
            }
            catch (JsonException)
            {
                // not JSON (e.g. an HTML error page from a proxy)
            }

            var error = json.Obj("error");
            var message = error.Str("message") ?? json.Str("message");
            if (string.IsNullOrWhiteSpace(message))
                message = string.IsNullOrWhiteSpace(reasonPhrase) ? $"HTTP {status}" : reasonPhrase;

            string code = null;
            if (error.Str("code") is string s)
                code = s;
            else if (error.Long("code") is long l)
                code = l.ToString(CultureInfo.InvariantCulture);

            return new PetrichorApiException(status, message, code);
        }
    }
}
