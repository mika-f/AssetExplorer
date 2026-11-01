// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;

using NatsunekoLaboratory.AssetExplorer.Contribute;
using NatsunekoLaboratory.AssetExplorer.Json;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Contribute
{
    public class ManifestJsonTests
    {
        [Test]
        public void ToJson_matches_submission_shape()
        {
            var manifest = new Manifest
            {
                Package = PackageSpec.ByIdentifier(IdentifierProvider.Booth, "123", "Name", null),
                Version = "1.0.0",
                Assets = new List<ManifestAsset>
                {
                    new ManifestAsset(new string('a', 32), "Assets/A.mat", "NativeFormatImporter"),
                    new ManifestAsset(new string('b', 32), "Assets/B.bin", null)
                }
            };

            var json = JsonReader.Parse(manifest.ToJson());
            var package = json.Obj("package");
            var assets = json.Arr("assets");

            Assert.That(json.Str("format"), Is.EqualTo("petrichor/v1"));
            Assert.That(json.Str("version"), Is.EqualTo("1.0.0"));
            Assert.That(package.Obj("identifier").Str("provider"), Is.EqualTo("booth"));
            Assert.That(package.Obj("identifier").Str("value"), Is.EqualTo("123"));
            Assert.That(package.Str("name"), Is.EqualTo("Name"));
            Assert.That(package.ContainsKey("description"), Is.False);
            Assert.That(assets[0].Str("type"), Is.EqualTo("NativeFormatImporter"));
            Assert.That(((Dictionary<string, object>)assets[1]).ContainsKey("type"), Is.False);
        }

        [Test]
        public void ById_and_ByName_shapes()
        {
            Assert.That(JsonWriter.Write(PackageSpec.ById("p1").ToJsonObject()), Is.EqualTo("{\"id\":\"p1\"}"));
            Assert.That(JsonWriter.Write(PackageSpec.ByName(" Name ", "d").ToJsonObject()), Is.EqualTo("{\"name\":\"Name\",\"description\":\"d\"}"));
            Assert.That(JsonWriter.Write(PackageSpec.ByIdentifier(IdentifierProvider.Package, "jp.x", "", "").ToJsonObject()), Is.EqualTo("{\"identifier\":{\"provider\":\"package\",\"value\":\"jp.x\"}}"));
        }
    }
}
