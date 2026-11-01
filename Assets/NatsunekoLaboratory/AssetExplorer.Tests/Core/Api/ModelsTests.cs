// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Linq;

using NatsunekoLaboratory.AssetExplorer.Api;
using NatsunekoLaboratory.AssetExplorer.Json;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Api
{
    public class ModelsTests
    {
        internal const string ReleaseJson = "{\"id\":\"r1\",\"version\":\"1.8.3\",\"status\":\"legacy\",\"submitterCount\":3,\"assetCount\":120,\"manifestId\":\"m1\",\"createdAt\":\"2026-01-02T03:04:05.000Z\",\"package\":{\"id\":\"p1\",\"name\":\"lilToon\"}}";

        [Test]
        public void ReleaseSummary_parses_all_fields()
        {
            var release = ReleaseSummary.FromJson(JsonReader.Parse(ReleaseJson));

            Assert.That(release.Id, Is.EqualTo("r1"));
            Assert.That(release.Version, Is.EqualTo("1.8.3"));
            Assert.That(release.Status, Is.EqualTo(ReleaseStatus.Legacy));
            Assert.That(release.SubmitterCount, Is.EqualTo(3));
            Assert.That(release.AssetCount, Is.EqualTo(120));
            Assert.That(release.ManifestId, Is.EqualTo("m1"));
            Assert.That(release.CreatedAt, Is.EqualTo("2026-01-02T03:04:05.000Z"));
            Assert.That(release.Package.Id, Is.EqualTo("p1"));
            Assert.That(release.Package.Name, Is.EqualTo("lilToon"));
        }

        [Test]
        public void Unknown_status_is_unverified()
        {
            var release = ReleaseSummary.FromJson(JsonReader.Parse("{\"id\":\"r\",\"status\":\"something\"}"));

            Assert.That(release.Status, Is.EqualTo(ReleaseStatus.Unverified));
            Assert.That(ReleaseSummary.FromJson(JsonReader.Parse("{\"status\":\"verified\"}")).Status, Is.EqualTo(ReleaseStatus.Verified));
        }

        [Test]
        public void AssetLookup_parses_paths_and_releases()
        {
            var json = "{\"guid\":\"g\",\"paths\":[{\"path\":\"Assets/A.mat\",\"type\":null,\"releases\":[" + ReleaseJson + "]}]}";
            var asset = AssetLookup.FromJson(JsonReader.Parse(json));

            Assert.That(asset.Guid, Is.EqualTo("g"));
            Assert.That(asset.Paths.Single().Path, Is.EqualTo("Assets/A.mat"));
            Assert.That(asset.Paths.Single().Type, Is.Null);
            Assert.That(asset.Paths.Single().Releases.Single().Id, Is.EqualTo("r1"));
        }

        [Test]
        public void BulkLookup_parses_results_missing_suggestions()
        {
            var json = "{\"results\":{\"g\":{\"guid\":\"g\",\"paths\":[]}},\"missing\":[\"x\",\"y\"],\"suggestions\":[{\"release\":" + ReleaseJson + ",\"matched\":7}]}";
            var bulk = BulkLookup.FromJson(JsonReader.Parse(json));

            Assert.That(bulk.Results.Keys, Is.EquivalentTo(new[] { "g" }));
            Assert.That(bulk.Missing, Is.EqualTo(new[] { "x", "y" }));
            Assert.That(bulk.Suggestions.Single().Matched, Is.EqualTo(7));
            Assert.That(bulk.Suggestions.Single().Release.Id, Is.EqualTo("r1"));
        }

        [Test]
        public void Submission_list_parses_bare_array()
        {
            var json = "[{\"id\":\"s1\",\"version\":\"1.0\",\"source\":\"API\",\"createdAt\":\"2026-01-01T00:00:00.000Z\",\"release\":" + ReleaseJson + "}]";
            var list = Submission.ListFromJson(JsonReader.Parse(json));

            Assert.That(list.Single().Id, Is.EqualTo("s1"));
            Assert.That(list.Single().Source, Is.EqualTo("API"));
            Assert.That(list.Single().Release.Package.Name, Is.EqualTo("lilToon"));
        }

        [Test]
        public void SubmissionResult_parses_fields()
        {
            var result = SubmissionResult.FromJson(JsonReader.Parse("{\"releaseId\":\"r\",\"manifestId\":\"m\",\"verified\":true,\"submitterCount\":2}"));

            Assert.That(result.ReleaseId, Is.EqualTo("r"));
            Assert.That(result.ManifestId, Is.EqualTo("m"));
            Assert.That(result.Verified, Is.True);
            Assert.That(result.SubmitterCount, Is.EqualTo(2));
        }

        [Test]
        public void User_reads_user_wrapper()
        {
            var user = User.FromJson(JsonReader.Parse("{\"user\":{\"screenName\":\"natsuneko\",\"displayName\":\"なつねこ\"}}"));

            Assert.That(user.ScreenName, Is.EqualTo("natsuneko"));
            Assert.That(user.DisplayName, Is.EqualTo("なつねこ"));
        }
    }
}
