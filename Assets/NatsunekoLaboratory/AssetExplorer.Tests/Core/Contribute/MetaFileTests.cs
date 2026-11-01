// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using NatsunekoLaboratory.AssetExplorer.Contribute;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Contribute
{
    public class MetaFileTests
    {
        [Test]
        public void Parses_guid_and_importer()
        {
            var meta = MetaFile.Parse("fileFormatVersion: 2\nguid: ABCDEF0123456789ABCDEF0123456789\nTextureImporter:\n  serializedVersion: 12\n");

            Assert.That(meta.Guid, Is.EqualTo("abcdef0123456789abcdef0123456789"));
            Assert.That(meta.ImporterType, Is.EqualTo("TextureImporter"));
            Assert.That(meta.IsFolder, Is.False);
        }

        [Test]
        public void Detects_folder()
        {
            var meta = MetaFile.Parse("fileFormatVersion: 2\r\nguid: 0123456789abcdef0123456789abcdef\r\nfolderAsset: yes\r\nDefaultImporter:\r\n  externalObjects: {}\r\n");

            Assert.That(meta.IsFolder, Is.True);
            Assert.That(meta.ImporterType, Is.EqualTo("DefaultImporter"));
        }

        [Test]
        public void Indented_importer_keys_are_ignored()
        {
            var meta = MetaFile.Parse("fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nScriptedImporter:\n  nestedImporter:\n    a: b\n");

            Assert.That(meta.ImporterType, Is.EqualTo("ScriptedImporter"));
        }

        [Test]
        public void No_importer_gives_null_type()
        {
            Assert.That(MetaFile.Parse("fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n").ImporterType, Is.Null);
        }

        [Test]
        public void Missing_guid_returns_null()
        {
            Assert.That(MetaFile.Parse("fileFormatVersion: 2\nTextureImporter:\n"), Is.Null);
            Assert.That(MetaFile.Parse(""), Is.Null);
        }
    }
}
