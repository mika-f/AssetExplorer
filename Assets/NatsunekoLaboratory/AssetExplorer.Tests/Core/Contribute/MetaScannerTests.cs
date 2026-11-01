// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.IO;
using System.Linq;

using NatsunekoLaboratory.AssetExplorer.Contribute;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Contribute
{
    public class MetaScannerTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "petrichor-meta-scanner-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, true);
        }

        private void Asset(string relative, string guid, string importer = "NativeFormatImporter")
        {
            var path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "x");
            File.WriteAllText(path + ".meta", $"fileFormatVersion: 2\nguid: {guid}\n{importer}:\n");
        }

        [Test]
        public void Scans_files_and_skips_folders_orphans_and_hidden()
        {
            Asset("A.mat", new string('a', 32));
            Directory.CreateDirectory(Path.Combine(_root, "Sub"));
            File.WriteAllText(Path.Combine(_root, "Sub.meta"), $"fileFormatVersion: 2\nguid: {new string('f', 32)}\nfolderAsset: yes\nDefaultImporter:\n");
            Asset("Sub/B.png", new string('b', 32), "TextureImporter");
            File.WriteAllText(Path.Combine(_root, "C.txt.meta"), $"fileFormatVersion: 2\nguid: {new string('c', 32)}\n");
            Asset("Hidden~/D.txt", new string('d', 32));
            Asset(".hidden/E.txt", new string('e', 32));

            var assets = MetaScanner.Scan(_root, "Assets/Pkg");

            Assert.That(assets.Select(w => (w.Path, w.Guid, w.Type)), Is.EqualTo(new[]
            {
                ("Assets/Pkg/A.mat", new string('a', 32), "NativeFormatImporter"),
                ("Assets/Pkg/Sub/B.png", new string('b', 32), "TextureImporter")
            }));
        }

        [Test]
        public void Trailing_separator_on_root_and_prefix_is_ignored()
        {
            Asset("A.mat", new string('a', 32));

            Assert.That(MetaScanner.Scan(_root + Path.DirectorySeparatorChar, "Assets/Pkg/").Single().Path, Is.EqualTo("Assets/Pkg/A.mat"));
        }
    }
}
