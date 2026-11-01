// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

using NatsunekoLaboratory.AssetExplorer.Contribute;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Contribute
{
    public class ManifestValidatorTests
    {
        private static readonly string A = new string('a', 32);
        private static readonly string B = new string('b', 32);

        private static Manifest Valid(params ManifestAsset[] assets)
        {
            return new Manifest
            {
                Package = PackageSpec.ByName("Package", null),
                Version = "1.0.0",
                Assets = assets.Length > 0 ? assets.ToList() : new List<ManifestAsset> { new ManifestAsset(A, "Assets/A.mat", null) }
            };
        }

        [Test]
        public void Normalize_lowercases_and_fixes_slashes()
        {
            var normalized = ManifestValidator.Normalize(new[] { new ManifestAsset(new string('A', 32), "Assets\\Sub\\A.mat", null) });

            Assert.That(normalized.Single().Guid, Is.EqualTo(A));
            Assert.That(normalized.Single().Path, Is.EqualTo("Assets/Sub/A.mat"));
        }

        [Test]
        public void Normalize_merges_duplicates_keeping_type()
        {
            var normalized = ManifestValidator.Normalize(new[]
            {
                new ManifestAsset(A, "Assets/A.mat", null),
                new ManifestAsset(A, "Assets/A.mat", "NativeFormatImporter"),
                new ManifestAsset(A, "Assets/A.mat", "Other")
            });

            Assert.That(normalized.Single().Type, Is.EqualTo("NativeFormatImporter"));
        }

        [Test]
        public void Validate_reports_conflicting_path()
        {
            var manifest = Valid(new ManifestAsset(A, "Assets/A.mat", null), new ManifestAsset(A, "Assets/B.mat", null));

            Assert.That(ManifestValidator.Validate(manifest), Does.Contain($"同じ GUID に別のパスがあります: {A}"));
        }

        [Test]
        public void Validate_rejects_control_characters()
        {
            var manifest = Valid(new ManifestAsset(A, "Assets/a\tb", null));

            Assert.That(ManifestValidator.Validate(manifest), Does.Contain($"パスに使えない文字が含まれています: {A}"));
        }

        [Test]
        public void Validate_rejects_lone_surrogate()
        {
            var manifest = Valid(new ManifestAsset(A, "Assets/\ud800.png", null));

            Assert.That(ManifestValidator.Validate(manifest), Does.Contain($"パスに使えない文字が含まれています: {A}"));
        }

        [Test]
        public void Validate_accepts_surrogate_pairs()
        {
            Assert.That(ManifestValidator.Validate(Valid(new ManifestAsset(A, "Assets/𠮷.png", null))), Is.Empty);
        }

        [Test]
        public void Validate_rejects_bad_guid()
        {
            var manifest = Valid(new ManifestAsset("xyz", "Assets/A.mat", null));

            Assert.That(ManifestValidator.Validate(manifest), Does.Contain("GUID の形式が正しくありません: xyz"));
        }

        [Test]
        public void Validate_rejects_long_path_and_type()
        {
            var manifest = Valid(new ManifestAsset(A, "Assets/" + new string('x', 1020), null), new ManifestAsset(B, "Assets/B", new string('t', 192)));
            var problems = ManifestValidator.Validate(manifest);

            Assert.That(problems, Does.Contain("パスが長すぎます (1,024 文字まで): Assets/" + new string('x', 1020)));
            Assert.That(problems, Does.Contain($"種類が長すぎます: {B}"));
        }

        [Test]
        public void Validate_limits_version_length()
        {
            var manifest = Valid();
            manifest.Version = new string('1', 65);

            Assert.That(ManifestValidator.Validate(manifest), Does.Contain("バージョンは 64 文字以内にしてください"));

            manifest.Version = " ";
            Assert.That(ManifestValidator.Validate(manifest), Does.Contain("バージョンを入力してください"));
        }

        [Test]
        public void Validate_requires_assets_and_limits_count()
        {
            var manifest = Valid();
            manifest.Assets = new List<ManifestAsset>();
            Assert.That(ManifestValidator.Validate(manifest), Does.Contain("アセットがありません"));

            manifest.Assets = Enumerable.Range(0, 30001).Select(i => new ManifestAsset(i.ToString("x32"), $"Assets/{i}", null)).ToList();
            Assert.That(ManifestValidator.Validate(manifest), Does.Contain("アセットは 30,000 件までです (現在 30,001 件)"));
        }

        [Test]
        public void Validate_requires_name_for_name_only_package()
        {
            var manifest = Valid();
            manifest.Package = PackageSpec.ByName("  ", null);

            Assert.That(ManifestValidator.Validate(manifest), Does.Contain("パッケージ名を入力してください"));
        }

        [Test]
        public void Validate_checks_package_fields()
        {
            var manifest = Valid();

            manifest.Package = PackageSpec.ByIdentifier(IdentifierProvider.Url, "", new string('n', 192), new string('d', 10001));
            var problems = ManifestValidator.Validate(manifest);
            Assert.That(problems, Does.Contain("識別子を入力してください"));
            Assert.That(problems, Does.Contain("パッケージ名は 191 文字以内にしてください"));
            Assert.That(problems, Does.Contain("説明は 10,000 文字以内にしてください"));

            manifest.Package = PackageSpec.ByIdentifier(IdentifierProvider.Url, new string('u', 513), null, null);
            Assert.That(ManifestValidator.Validate(manifest), Does.Contain("識別子は 512 文字以内にしてください"));

            manifest.Package = PackageSpec.ById(" ");
            Assert.That(ManifestValidator.Validate(manifest), Does.Contain("パッケージ ID を入力してください"));
        }

        [Test]
        public void Validate_accepts_valid_manifest()
        {
            Assert.That(ManifestValidator.Validate(Valid()), Is.Empty);
        }
    }
}
