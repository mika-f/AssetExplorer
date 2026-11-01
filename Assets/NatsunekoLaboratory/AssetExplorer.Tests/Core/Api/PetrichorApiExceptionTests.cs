// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using NatsunekoLaboratory.AssetExplorer.Api;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Api
{
    public class PetrichorApiExceptionTests
    {
        [Test]
        public void FromResponse_reads_error_message()
        {
            var e = PetrichorApiException.FromResponse(400, "Bad Request", "{\"error\":{\"message\":\"x\",\"code\":400}}");

            Assert.That(e.StatusCode, Is.EqualTo(400));
            Assert.That(e.Message, Is.EqualTo("x"));
            Assert.That(e.ServerCode, Is.EqualTo("400"));
        }

        [Test]
        public void FromResponse_reads_flat_message()
        {
            var e = PetrichorApiException.FromResponse(401, "Unauthorized", "{\"message\":\"unauthorized\"}");

            Assert.That(e.Message, Is.EqualTo("unauthorized"));
            Assert.That(e.ServerCode, Is.Null);
        }

        [Test]
        public void FromResponse_falls_back_to_reason_on_non_json()
        {
            var e = PetrichorApiException.FromResponse(502, "Bad Gateway", "<html>");

            Assert.That(e.Message, Is.EqualTo("Bad Gateway"));
        }

        [Test]
        public void FromResponse_falls_back_to_status_when_reason_empty()
        {
            var e = PetrichorApiException.FromResponse(500, null, "");

            Assert.That(e.Message, Is.EqualTo("HTTP 500"));
        }
    }
}
