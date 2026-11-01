// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

using NatsunekoLaboratory.AssetExplorer.Json;

namespace NatsunekoLaboratory.AssetExplorer.Api
{
    internal enum ReleaseStatus
    {
        Verified,

        Legacy,

        Unverified
    }

    internal sealed class PackageRef
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public static PackageRef FromJson(object json)
        {
            return json == null ? null : new PackageRef { Id = json.Str("id"), Name = json.Str("name") };
        }
    }

    internal sealed class ReleaseSummary
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public ReleaseStatus Status { get; set; }

        public int SubmitterCount { get; set; }

        public int AssetCount { get; set; }

        public string ManifestId { get; set; }

        public string CreatedAt { get; set; }

        public PackageRef Package { get; set; }

        public static ReleaseSummary FromJson(object json)
        {
            return new ReleaseSummary
            {
                Id = json.Str("id"),
                Version = json.Str("version"),
                Status = ParseStatus(json.Str("status")),
                SubmitterCount = (int)(json.Long("submitterCount") ?? 0),
                AssetCount = (int)(json.Long("assetCount") ?? 0),
                ManifestId = json.Str("manifestId"),
                CreatedAt = json.Str("createdAt"),
                Package = PackageRef.FromJson(json.Obj("package")) ?? new PackageRef()
            };
        }

        private static ReleaseStatus ParseStatus(string status)
        {
            switch (status)
            {
                case "verified":
                    return ReleaseStatus.Verified;

                case "legacy":
                    return ReleaseStatus.Legacy;

                default:
                    return ReleaseStatus.Unverified;
            }
        }
    }

    internal sealed class AssetPathEntry
    {
        public string Path { get; set; }

        public string Type { get; set; }

        public List<ReleaseSummary> Releases { get; set; }

        public static AssetPathEntry FromJson(object json)
        {
            return new AssetPathEntry
            {
                Path = json.Str("path"),
                Type = json.Str("type"),
                Releases = MapList(json.Arr("releases"), ReleaseSummary.FromJson)
            };
        }

        internal static List<T> MapList<T>(List<object> items, System.Func<object, T> map)
        {
            return items == null ? new List<T>() : items.Select(map).ToList();
        }
    }

    internal sealed class AssetLookup
    {
        public string Guid { get; set; }

        public List<AssetPathEntry> Paths { get; set; }

        public static AssetLookup FromJson(object json)
        {
            return new AssetLookup
            {
                Guid = json.Str("guid"),
                Paths = AssetPathEntry.MapList(json.Arr("paths"), AssetPathEntry.FromJson)
            };
        }
    }

    internal sealed class Suggestion
    {
        public ReleaseSummary Release { get; set; }

        public int Matched { get; set; }

        public static Suggestion FromJson(object json)
        {
            return new Suggestion
            {
                Release = ReleaseSummary.FromJson(json.Obj("release")),
                Matched = (int)(json.Long("matched") ?? 0)
            };
        }
    }

    internal sealed class BulkLookup
    {
        public Dictionary<string, AssetLookup> Results { get; set; } = new Dictionary<string, AssetLookup>();

        public List<string> Missing { get; set; } = new List<string>();

        public List<Suggestion> Suggestions { get; set; } = new List<Suggestion>();

        public static BulkLookup FromJson(object json)
        {
            var results = json.Obj("results") ?? new Dictionary<string, object>();

            return new BulkLookup
            {
                Results = results.ToDictionary(w => w.Key, w => AssetLookup.FromJson(w.Value)),
                Missing = AssetPathEntry.MapList(json.Arr("missing"), w => w as string).Where(w => w != null).ToList(),
                Suggestions = AssetPathEntry.MapList(json.Arr("suggestions"), Suggestion.FromJson)
            };
        }
    }

    internal sealed class SubmissionResult
    {
        public string ReleaseId { get; set; }

        public string ManifestId { get; set; }

        public bool Verified { get; set; }

        public int SubmitterCount { get; set; }

        /// <summary>True when the server answered 201 (a new submission) rather than 200 (a resubmission).</summary>
        public bool Created { get; set; }

        public static SubmissionResult FromJson(object json)
        {
            return new SubmissionResult
            {
                ReleaseId = json.Str("releaseId"),
                ManifestId = json.Str("manifestId"),
                Verified = json.Bool("verified") ?? false,
                SubmitterCount = (int)(json.Long("submitterCount") ?? 0)
            };
        }
    }

    internal sealed class Submission
    {
        public string Id { get; set; }

        public string Version { get; set; }

        /// <summary>LEGACY, WEB or API.</summary>
        public string Source { get; set; }

        public string CreatedAt { get; set; }

        public ReleaseSummary Release { get; set; }

        public static Submission FromJson(object json)
        {
            return new Submission
            {
                Id = json.Str("id"),
                Version = json.Str("version"),
                Source = json.Str("source"),
                CreatedAt = json.Str("createdAt"),
                Release = ReleaseSummary.FromJson(json.Obj("release"))
            };
        }

        public static List<Submission> ListFromJson(object json)
        {
            return AssetPathEntry.MapList(json as List<object>, FromJson);
        }
    }

    internal sealed class User
    {
        public string ScreenName { get; set; }

        public string DisplayName { get; set; }

        public static User FromJson(object json)
        {
            var user = json.Obj("user");
            return new User { ScreenName = user.Str("screenName"), DisplayName = user.Str("displayName") };
        }
    }
}
