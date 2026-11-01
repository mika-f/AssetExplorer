// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NatsunekoLaboratory.AssetExplorer.Contribute
{
    /// <summary>
    ///     Mirrors the server's submission rules (teyvat <c>PETRICHOR_SUBMISSION_SCHEMA</c> and <c>normalizeManifestAssets</c>)
    ///     so problems show up before anything is sent.
    /// </summary>
    internal static class ManifestValidator
    {
        public const int MaxAssets = 30000;
        public const int MaxPathLength = 1024;
        public const int MaxVersionLength = 64;
        public const int MaxPackageNameLength = 191;
        public const int MaxIdentifierLength = 512;
        public const int MaxDescriptionLength = 10000;
        public const int MaxTypeLength = 191;

        private static readonly Regex GuidPattern = new Regex("^[a-f0-9]{32}$", RegexOptions.Compiled);

        /// <summary>
        ///     Lowercases GUIDs, turns <c>\</c> into <c>/</c>, and merges identical (GUID, path) pairs keeping the first non-empty type.
        ///     Pairs with the same GUID and different paths are kept so <see cref="Validate" /> can report them.
        /// </summary>
        public static List<ManifestAsset> Normalize(IEnumerable<ManifestAsset> assets)
        {
            var result = new List<ManifestAsset>();
            var index = new Dictionary<(string, string), int>();

            foreach (var asset in assets)
            {
                var guid = asset.Guid?.ToLowerInvariant() ?? "";
                var path = asset.Path?.Replace('\\', '/') ?? "";
                var key = (guid, path);

                if (index.TryGetValue(key, out var i))
                {
                    if (string.IsNullOrEmpty(result[i].Type) && !string.IsNullOrEmpty(asset.Type))
                        result[i] = new ManifestAsset(guid, path, asset.Type);

                    continue;
                }

                index[key] = result.Count;
                result.Add(new ManifestAsset(guid, path, asset.Type));
            }

            return result;
        }

        public static List<string> Validate(Manifest manifest)
        {
            var problems = new List<string>();

            ValidatePackage(manifest.Package, problems);

            var version = manifest.Version?.Trim() ?? "";
            if (version.Length == 0)
                problems.Add("バージョンを入力してください");
            else if (version.Length > MaxVersionLength)
                problems.Add("バージョンは 64 文字以内にしてください");

            var assets = manifest.Assets ?? new List<ManifestAsset>();
            if (assets.Count == 0)
                problems.Add("アセットがありません");
            else if (assets.Count > MaxAssets)
                problems.Add($"アセットは 30,000 件までです (現在 {assets.Count.ToString("N0", CultureInfo.InvariantCulture)} 件)");

            var paths = new Dictionary<string, string>();
            foreach (var asset in assets)
            {
                var guid = asset.Guid ?? "";
                if (!GuidPattern.IsMatch(guid))
                {
                    problems.Add($"GUID の形式が正しくありません: {guid}");
                    continue;
                }

                var path = asset.Path ?? "";
                if (path.Length == 0 || path.Length > MaxPathLength)
                    problems.Add($"パスが長すぎます (1,024 文字まで): {path}");
                else if (HasForbiddenCharacters(path))
                    problems.Add($"パスに使えない文字が含まれています: {guid}");

                if (asset.Type != null && asset.Type.Length > MaxTypeLength)
                    problems.Add($"種類が長すぎます: {guid}");

                if (paths.TryGetValue(guid, out var existing))
                {
                    if (existing != path)
                        problems.Add($"同じ GUID に別のパスがあります: {guid}");
                }
                else
                {
                    paths[guid] = path;
                }
            }

            return problems;
        }

        private static void ValidatePackage(PackageSpec package, List<string> problems)
        {
            if (package == null)
            {
                problems.Add("パッケージ名を入力してください");
                return;
            }

            switch (package.Kind)
            {
                case PackageSpecKind.Id:
                    if (string.IsNullOrWhiteSpace(package.Id))
                        problems.Add("パッケージ ID を入力してください");
                    return;

                case PackageSpecKind.Identifier:
                    if (string.IsNullOrWhiteSpace(package.Value))
                        problems.Add("識別子を入力してください");
                    else if (package.Value.Length > MaxIdentifierLength)
                        problems.Add("識別子は 512 文字以内にしてください");
                    break;

                case PackageSpecKind.Name:
                    if (string.IsNullOrWhiteSpace(package.Name))
                        problems.Add("パッケージ名を入力してください");
                    break;
            }

            if (package.Name != null && package.Name.Length > MaxPackageNameLength)
                problems.Add("パッケージ名は 191 文字以内にしてください");

            if (package.Description != null && package.Description.Length > MaxDescriptionLength)
                problems.Add("説明は 10,000 文字以内にしてください");
        }

        // Control characters (U+0000-U+001F, U+007F) and unpaired surrogates would make two manifests hash alike on the server.
        private static bool HasForbiddenCharacters(string path)
        {
            for (var i = 0; i < path.Length; i++)
            {
                var c = path[i];
                if (c < 0x20 || c == 0x7f)
                    return true;

                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= path.Length || !char.IsLowSurrogate(path[i + 1]))
                        return true;

                    i++;
                }
                else if (char.IsLowSurrogate(c))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
