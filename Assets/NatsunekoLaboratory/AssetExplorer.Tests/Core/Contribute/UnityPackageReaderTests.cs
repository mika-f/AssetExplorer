// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Text;
using System.Threading;

using NatsunekoLaboratory.AssetExplorer.Contribute;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Contribute
{
    public class UnityPackageReaderTests
    {
        private static readonly string A = new string('a', 32);
        private static readonly string B = new string('b', 32);

        private static string Meta(string guid, string importer = "NativeFormatImporter", bool folder = false)
        {
            return $"fileFormatVersion: 2\nguid: {guid}\n{(folder ? "folderAsset: yes\n" : "")}{importer}:\n  userData: \n";
        }

        [Test]
        public void Reads_entries_in_any_order()
        {
            var archive = new TarBuilder()
                          .File($"{A}/asset", "aaa")
                          .File($"{A}/pathname", "Assets/A.mat")
                          .File($"{A}/asset.meta", Meta(A))
                          .File($"{B}/asset.meta", Meta(B, "TextureImporter"))
                          .File($"{B}/pathname", "Assets/B.png")
                          .File($"{B}/asset", "bbbb");

            var entries = UnityPackageReader.Read(archive.BuildStream(), null, CancellationToken.None);
            var assets = UnityPackageReader.ToManifestAssets(entries);

            Assert.That(assets.Select(w => (w.Guid, w.Path, w.Type)), Is.EqualTo(new[] { (A, "Assets/A.mat", "NativeFormatImporter"), (B, "Assets/B.png", "TextureImporter") }));
            Assert.That(entries.Single(w => w.Guid == B).AssetSize, Is.EqualTo(4));
        }

        [Test]
        public void Skips_folder_entries()
        {
            var archive = new TarBuilder()
                          .File($"{A}/pathname", "Assets/Folder")
                          .File($"{A}/asset.meta", Meta(A, "DefaultImporter", true))
                          .File($"{B}/pathname", "Assets/Folder/B.png")
                          .File($"{B}/asset.meta", Meta(B, "TextureImporter"))
                          .File($"{B}/asset", "b");

            var assets = UnityPackageReader.ToManifestAssets(UnityPackageReader.Read(archive.BuildStream(), null, CancellationToken.None));

            Assert.That(assets.Select(w => w.Path), Is.EqualTo(new[] { "Assets/Folder/B.png" }));
        }

        [Test]
        public void Skips_entries_without_asset_body()
        {
            var archive = new TarBuilder()
                          .File($"{A}/pathname", "Assets/A.mat")
                          .File($"{A}/asset.meta", Meta(A));

            Assert.That(UnityPackageReader.ToManifestAssets(UnityPackageReader.Read(archive.BuildStream(), null, CancellationToken.None)), Is.Empty);
        }

        [Test]
        public void Reads_gnu_long_names()
        {
            var archive = new TarBuilder()
                          .GnuLongNameFile($"./{A}/pathname", "Assets/A.mat")
                          .File($"{A}/asset.meta", Meta(A))
                          .File($"{A}/asset", "a");

            var entries = UnityPackageReader.Read(archive.BuildStream(), null, CancellationToken.None);

            Assert.That(entries.Single().Path, Is.EqualTo("Assets/A.mat"));
        }

        [Test]
        public void Reads_pax_path()
        {
            var archive = new TarBuilder()
                          .PaxFile($"{A}/pathname", "Assets/A.mat")
                          .File($"{A}/asset.meta", Meta(A))
                          .File($"{A}/asset", "a");

            var entries = UnityPackageReader.Read(archive.BuildStream(), null, CancellationToken.None);

            Assert.That(entries.Single().Path, Is.EqualTo("Assets/A.mat"));
        }

        [Test]
        public void Reads_ustar_prefix()
        {
            var archive = new TarBuilder()
                          .PrefixedFile(A, "pathname", "Assets/A.mat")
                          .File($"{A}/asset.meta", Meta(A))
                          .File($"{A}/asset", "a");

            Assert.That(UnityPackageReader.Read(archive.BuildStream(), null, CancellationToken.None).Single().Path, Is.EqualTo("Assets/A.mat"));
        }

        [Test]
        public void Trims_pathname_trailer()
        {
            var archive = new TarBuilder()
                          .File($"{A}/pathname", "Assets/A.mat\r\n00")
                          .File($"{A}/asset.meta", Meta(A))
                          .File($"{A}/asset", "a");

            Assert.That(UnityPackageReader.Read(archive.BuildStream(), null, CancellationToken.None).Single().Path, Is.EqualTo("Assets/A.mat"));
        }

        [Test]
        public void Reads_large_asset_bodies_without_breaking_alignment()
        {
            var archive = new TarBuilder()
                          .File($"{A}/asset", new byte[200_000])
                          .File($"{A}/pathname", "Assets/A.bin")
                          .File($"{A}/asset.meta", Meta(A, "DefaultImporter"));

            var entries = UnityPackageReader.Read(archive.BuildStream(), null, CancellationToken.None);

            Assert.That(entries.Single().AssetSize, Is.EqualTo(200_000));
            Assert.That(entries.Single().Path, Is.EqualTo("Assets/A.bin"));
        }

        [Test]
        public void Reads_meta_files_over_one_megabyte()
        {
            // e.g. a model with hundreds of animation clips; the guid and importer are at the top
            var meta = Meta(A, "ModelImporter") + "  animations:\n" + string.Concat(Enumerable.Repeat("    - name: clip\n", 120_000));
            var archive = new TarBuilder()
                          .File($"{A}/pathname", "Assets/Model.fbx")
                          .File($"{A}/asset.meta", meta)
                          .File($"{A}/asset", "fbx");

            var assets = UnityPackageReader.ToManifestAssets(UnityPackageReader.Read(archive.BuildStream(), null, CancellationToken.None));

            Assert.That(Encoding.UTF8.GetByteCount(meta), Is.GreaterThan(1024 * 1024));
            Assert.That(assets.Select(w => (w.Guid, w.Type)), Is.EqualTo(new[] { (A, "ModelImporter") }));
        }

        [Test]
        public void Oversized_pathname_is_ignored_without_error()
        {
            var archive = new TarBuilder()
                          .File($"{A}/pathname", new string('x', 17 * 1024 * 1024))
                          .File($"{A}/asset.meta", Meta(A))
                          .File($"{A}/asset", "a");

            Assert.That(UnityPackageReader.ToManifestAssets(UnityPackageReader.Read(archive.BuildStream(), null, CancellationToken.None)), Is.Empty);
        }

        [Test]
        public void ReadAsset_returns_bytes_for_guid()
        {
            var archive = new TarBuilder()
                          .File($"{A}/asset", "{\"name\":\"x\"}")
                          .File($"{A}/pathname", "Assets/package.json")
                          .File($"{B}/asset", "other");

            var bytes = UnityPackageReader.ReadAsset(archive.BuildStream(), A, CancellationToken.None);

            Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo("{\"name\":\"x\"}"));
            Assert.That(UnityPackageReader.ReadAsset(archive.BuildStream(), new string('c', 32), CancellationToken.None), Is.Null);
        }

        [Test]
        public void Reports_compressed_progress()
        {
            var archive = new TarBuilder().File($"{A}/pathname", "Assets/A.mat").Build();
            long last = 0;
            var progress = new SyncProgress<long>(w => last = w);

            UnityPackageReader.Read(new System.IO.MemoryStream(archive), progress, CancellationToken.None);

            Assert.That(last, Is.EqualTo(archive.Length));
        }

        [Test]
        public void Cancellation_throws()
        {
            var archive = new TarBuilder().File($"{A}/pathname", "Assets/A.mat");
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() => UnityPackageReader.Read(archive.BuildStream(), null, cts.Token));
        }

        private sealed class SyncProgress<T> : IProgress<T>
        {
            private readonly Action<T> _handler;

            public SyncProgress(Action<T> handler)
            {
                _handler = handler;
            }

            public void Report(T value)
            {
                _handler(value);
            }
        }
    }
}
