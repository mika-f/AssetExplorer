// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;

using NatsunekoLaboratory.AssetExplorer.Api;
using NatsunekoLaboratory.AssetExplorer.Auth;
using NatsunekoLaboratory.AssetExplorer.Tests.Auth;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Api
{
    // Calls the real API. Run explicitly: dotnet test tools/PureTests --filter Category=Live
    [Category("Live")]
    [Explicit("calls https://api.natsuneko.com")]
    public class PetrichorLiveTests
    {
        private static PetrichorClient Client()
        {
            var http = new HttpClient();
            var auth = new AuthSession(new MemoryTokenStorage(), () => new OAuthClient("unused", "https://accounts.natsuneko.com/authorize", "https://api.natsuneko.com/token", http, null), null);
            return new PetrichorClient(http, () => "https://api.natsuneko.com", auth);
        }

        [Test]
        public void Live_lookup_unknown_guid_is_missing()
        {
            var guid = Guid.NewGuid().ToString("N");

            var result = AsyncHelper.Run(() => Client().LookupAsync(new[] { guid }, null, CancellationToken.None));

            Assert.That(result.Missing, Does.Contain(guid));
            Assert.That(result.Results, Is.Empty);
        }

        [Test]
        public void Live_get_unknown_asset_returns_null()
        {
            Assert.That(AsyncHelper.Run(() => Client().GetAssetAsync(Guid.NewGuid().ToString("N"), CancellationToken.None)), Is.Null);
        }

        [Test]
        public void Live_my_submissions_without_login_requires_auth()
        {
            Assert.Throws<AuthRequiredException>(() => AsyncHelper.Run(() => Client().GetMySubmissionsAsync(CancellationToken.None)));
        }

        // A GUID from the migrated legacy data (Assets/Animations/Skybox.anim, found via GET /packages → /releases/{id}/assets).
        private const string KnownGuid = "1f8856353f3f1a1419b52d022238c8fb";

        [Test]
        public void Live_lookup_known_guid_has_releases()
        {
            var result = AsyncHelper.Run(() => Client().LookupAsync(new[] { KnownGuid.ToUpperInvariant() }, null, CancellationToken.None));

            Assert.That(result.Results.Keys, Does.Contain(KnownGuid));
            Assert.That(result.Results[KnownGuid].Paths.SelectMany(w => w.Releases), Is.Not.Empty);
            Assert.That(result.Suggestions.Select(w => w.Matched), Has.All.GreaterThan(0));
        }

        [Test]
        public void Live_get_known_asset_has_paths()
        {
            var asset = AsyncHelper.Run(() => Client().GetAssetAsync(KnownGuid, CancellationToken.None));

            Assert.That(asset.Paths.Select(w => w.Path), Does.Contain("Assets/Animations/Skybox.anim"));
        }
    }
}
