// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using NatsunekoLaboratory.AssetExplorer.Contribute;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Contribute
{
    public class PackageIdentifierTests
    {
        [TestCase("https://booth.pm/ja/items/123456", "123456")]
        [TestCase("https://booth.pm/items/7", "7")]
        [TestCase("https://foo.booth.pm/items/42", "42")]
        [TestCase("https://booth.pm/zh-cn/items/9/", "9")]
        public void Booth_urls_become_booth_identifiers(string url, string id)
        {
            Assert.That(PackageIdentifier.TryParseBooth(url, out var itemId), Is.True);
            Assert.That(itemId, Is.EqualTo(id));
            Assert.That(PackageIdentifier.FromUrl(url), Is.EqualTo((IdentifierProvider.Booth, id)));
        }

        [TestCase("https://example.com/x")]
        [TestCase("https://booth.pm/ja/browse/3D")]
        [TestCase("not a url")]
        public void Other_urls_stay_urls(string url)
        {
            Assert.That(PackageIdentifier.TryParseBooth(url, out _), Is.False);
            Assert.That(PackageIdentifier.FromUrl(url), Is.EqualTo((IdentifierProvider.Url, url)));
        }
    }

    public class PackageInfoDetectorTests
    {
        [Test]
        public void Reads_package_json()
        {
            var info = PackageInfoDetector.FromPackageJson("{\"name\":\"jp.lilxyzw.liltoon\",\"displayName\":\"lilToon\",\"version\":\"1.8.3\",\"description\":\"d\"}");

            Assert.That(info.Name, Is.EqualTo("jp.lilxyzw.liltoon"));
            Assert.That(info.DisplayName, Is.EqualTo("lilToon"));
            Assert.That(info.Version, Is.EqualTo("1.8.3"));
            Assert.That(info.Description, Is.EqualTo("d"));
        }

        [Test]
        public void Invalid_package_json_returns_null()
        {
            Assert.That(PackageInfoDetector.FromPackageJson("{"), Is.Null);
            Assert.That(PackageInfoDetector.FromPackageJson("[]"), Is.Null);
        }

        [TestCase("lilToon_1.8.3.unitypackage", "lilToon", "1.8.3")]
        [TestCase("MyAvatar v2.0.unitypackage", "MyAvatar", "2.0")]
        [TestCase("Avatar3_1.0.0-beta.1.unitypackage", "Avatar3", "1.0.0-beta.1")]
        [TestCase("Thing.unitypackage", "Thing", null)]
        public void Guesses_from_file_name(string fileName, string name, string version)
        {
            var info = PackageInfoDetector.FromFileName(fileName);

            Assert.That(info.DisplayName, Is.EqualTo(name));
            Assert.That(info.Version, Is.EqualTo(version));
        }
    }
}
