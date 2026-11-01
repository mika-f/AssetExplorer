// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace NatsunekoLaboratory.AssetExplorer.Contribute
{
    internal sealed class UnityPackageEntry
    {
        public string Guid { get; set; }

        public string Path { get; set; }

        public MetaFile Meta { get; set; }

        public bool HasAsset { get; set; }

        public long AssetSize { get; set; }
    }

    /// <summary>
    ///     Reads a .unitypackage (a gzip-compressed tar with <c>&lt;guid&gt;/pathname</c>, <c>&lt;guid&gt;/asset.meta</c> and
    ///     <c>&lt;guid&gt;/asset</c> entries) without importing it. Asset bodies are skipped, never buffered.
    /// </summary>
    internal static class UnityPackageReader
    {
        private const int BlockSize = 512;
        private const int MaxTextEntrySize = 1024 * 1024;

        public static List<UnityPackageEntry> Read(Stream gzipStream, IProgress<long> compressedBytesRead, CancellationToken ct)
        {
            var entries = new Dictionary<string, UnityPackageEntry>();

            Walk(gzipStream, compressedBytesRead, ct, (name, size, tar) =>
            {
                var slash = name.IndexOf('/');
                if (slash <= 0)
                    return false;

                var guid = name.Substring(0, slash);
                var file = name.Substring(slash + 1);
                if (!entries.TryGetValue(guid, out var entry))
                    entries[guid] = entry = new UnityPackageEntry { Guid = guid };

                switch (file)
                {
                    case "pathname":
                        entry.Path = ReadPathname(ReadText(tar, size, ct));
                        return true;

                    case "asset.meta":
                        entry.Meta = MetaFile.Parse(ReadText(tar, size, ct));
                        return true;

                    case "asset":
                        entry.HasAsset = true;
                        entry.AssetSize = size;
                        return false;

                    default:
                        return false;
                }
            });

            return entries.Values.Where(w => w.Path != null).ToList();
        }

        /// <summary>Returns the <c>asset</c> body of one entry, or null when the archive has none.</summary>
        public static byte[] ReadAsset(Stream gzipStream, string guid, CancellationToken ct)
        {
            byte[] result = null;

            Walk(gzipStream, null, ct, (name, size, tar) =>
            {
                if (result != null || name != guid + "/asset")
                    return false;

                result = ReadExactly(tar, size, ct);
                return true;
            });

            return result;
        }

        public static List<ManifestAsset> ToManifestAssets(IEnumerable<UnityPackageEntry> entries)
        {
            return entries.Where(w => w.HasAsset && w.Meta != null && !w.Meta.IsFolder && !string.IsNullOrEmpty(w.Path))
                          .Select(w => new ManifestAsset(w.Meta.Guid, w.Path, w.Meta.ImporterType))
                          .OrderBy(w => w.Path, StringComparer.Ordinal)
                          .ToList();
        }

        /// <summary>
        ///     Walks the regular files of the archive. <paramref name="onFile" /> returns true when it consumed exactly
        ///     <c>size</c> bytes from the stream; otherwise the body is skipped here.
        /// </summary>
        private static void Walk(Stream gzipStream, IProgress<long> progress, CancellationToken ct, Func<string, long, Stream, bool> onFile)
        {
            ct.ThrowIfCancellationRequested();

            using (var counting = new CountingStream(gzipStream, progress))
            using (var tar = new GZipStream(counting, CompressionMode.Decompress))
            {
                var header = new byte[BlockSize];
                string nextName = null;

                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!TryReadBlock(tar, header))
                        break;

                    if (header.All(w => w == 0))
                        break;

                    var size = ParseSize(header);
                    var type = (char)header[156];
                    var name = ParseName(header);

                    switch (type)
                    {
                        case 'L': // GNU long name for the next entry
                            nextName = Encoding.UTF8.GetString(ReadExactly(tar, size, ct)).TrimEnd('\0');
                            SkipPadding(tar, size, ct);
                            continue;

                        case 'x': // PAX extended header for the next entry
                            nextName = ParsePaxPath(Encoding.UTF8.GetString(ReadExactly(tar, size, ct))) ?? nextName;
                            SkipPadding(tar, size, ct);
                            continue;

                        case '0':
                        case '\0':
                            var path = Normalize(nextName ?? name);
                            nextName = null;

                            if (!onFile(path, size, tar))
                                Skip(tar, size, ct);

                            SkipPadding(tar, size, ct);
                            continue;

                        default: // directories, global headers, links
                            nextName = null;
                            Skip(tar, size, ct);
                            SkipPadding(tar, size, ct);
                            continue;
                    }
                }

                progress?.Report(counting.Position);
            }
        }

        private static string Normalize(string name)
        {
            name = name.Replace('\\', '/');
            while (name.StartsWith("./", StringComparison.Ordinal))
                name = name.Substring(2);

            return name;
        }

        private static string ReadPathname(string text)
        {
            // Some Unity versions append "\n00" after the path
            var newline = text.IndexOf('\n');
            return (newline >= 0 ? text.Substring(0, newline) : text).TrimEnd('\r');
        }

        private static string ReadText(Stream tar, long size, CancellationToken ct)
        {
            if (size > MaxTextEntrySize)
            {
                Skip(tar, size, ct);
                return null;
            }

            return Encoding.UTF8.GetString(ReadExactly(tar, size, ct));
        }

        private static string ParseName(byte[] header)
        {
            var name = ReadCString(header, 0, 100);
            var isUstar = ReadCString(header, 257, 6) == "ustar";
            var prefix = isUstar ? ReadCString(header, 345, 155) : "";

            return prefix.Length > 0 ? prefix + "/" + name : name;
        }

        private static long ParseSize(byte[] header)
        {
            // base-256 encoding for sizes over 8 GiB
            if ((header[124] & 0x80) != 0)
            {
                long value = header[124] & 0x7f;
                for (var i = 125; i < 136; i++)
                    value = (value << 8) | header[i];

                return value;
            }

            var octal = ReadCString(header, 124, 12).Trim();
            return octal.Length == 0 ? 0 : Convert.ToInt64(octal, 8);
        }

        private static string ParsePaxPath(string records)
        {
            foreach (var record in records.Split('\n'))
            {
                var space = record.IndexOf(' ');
                if (space < 0)
                    continue;

                var pair = record.Substring(space + 1);
                if (pair.StartsWith("path=", StringComparison.Ordinal))
                    return pair.Substring("path=".Length);
            }

            return null;
        }

        private static string ReadCString(byte[] buffer, int offset, int length)
        {
            var end = offset;
            while (end < offset + length && buffer[end] != 0)
                end++;

            return Encoding.UTF8.GetString(buffer, offset, end - offset);
        }

        private static bool TryReadBlock(Stream stream, byte[] block)
        {
            var read = 0;
            while (read < block.Length)
            {
                var n = stream.Read(block, read, block.Length - read);
                if (n == 0)
                    return false;

                read += n;
            }

            return true;
        }

        private static byte[] ReadExactly(Stream stream, long size, CancellationToken ct)
        {
            var buffer = new byte[size];
            var read = 0;
            while (read < size)
            {
                ct.ThrowIfCancellationRequested();

                var n = stream.Read(buffer, read, (int)Math.Min(81920, size - read));
                if (n == 0)
                    throw new EndOfStreamException("The archive ended in the middle of an entry");

                read += n;
            }

            return buffer;
        }

        private static void Skip(Stream stream, long size, CancellationToken ct)
        {
            var buffer = new byte[81920];
            while (size > 0)
            {
                ct.ThrowIfCancellationRequested();

                var n = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, size));
                if (n == 0)
                    throw new EndOfStreamException("The archive ended in the middle of an entry");

                size -= n;
            }
        }

        private static void SkipPadding(Stream stream, long size, CancellationToken ct)
        {
            Skip(stream, (BlockSize - size % BlockSize) % BlockSize, ct);
        }

        /// <summary>Reports how many compressed bytes have been read, for progress on large archives.</summary>
        private sealed class CountingStream : Stream
        {
            private readonly Stream _inner;
            private readonly IProgress<long> _progress;
            private long _lastReported;

            public CountingStream(Stream inner, IProgress<long> progress)
            {
                _inner = inner;
                _progress = progress;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position { get; set; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var n = _inner.Read(buffer, offset, count);
                Position += n;

                if (_progress != null && Position - _lastReported >= 1024 * 1024)
                {
                    _lastReported = Position;
                    _progress.Report(Position);
                }

                return n;
            }

            public override void Flush() { }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                // the caller owns the inner stream
                base.Dispose(disposing);
            }
        }
    }
}
