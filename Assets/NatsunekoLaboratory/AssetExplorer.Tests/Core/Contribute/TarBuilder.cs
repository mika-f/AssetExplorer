// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Contribute
{
    /// <summary>
    ///     Builds small gzip-compressed tar archives (the .unitypackage format) for tests.
    /// </summary>
    internal sealed class TarBuilder
    {
        private readonly List<(string Name, char Type, byte[] Data, string Prefix)> _entries = new List<(string, char, byte[], string)>();

        public TarBuilder File(string name, string content)
        {
            _entries.Add((name, '0', Encoding.UTF8.GetBytes(content), null));
            return this;
        }

        public TarBuilder File(string name, byte[] content)
        {
            _entries.Add((name, '0', content, null));
            return this;
        }

        /// <summary>A file whose name is split into the ustar prefix and name fields.</summary>
        public TarBuilder PrefixedFile(string prefix, string name, string content)
        {
            _entries.Add((name, '0', Encoding.UTF8.GetBytes(content), prefix));
            return this;
        }

        /// <summary>A GNU long-name record ('L') followed by a file with a truncated name.</summary>
        public TarBuilder GnuLongNameFile(string name, string content)
        {
            _entries.Add(("././@LongLink", 'L', Encoding.UTF8.GetBytes(name + "\0"), null));
            _entries.Add((name.Substring(0, Math.Min(99, name.Length)), '0', Encoding.UTF8.GetBytes(content), null));
            return this;
        }

        /// <summary>A PAX extended header ('x') carrying <c>path</c>, followed by a file with a placeholder name.</summary>
        public TarBuilder PaxFile(string name, string content)
        {
            var record = " path=" + name + "\n";
            var length = Encoding.UTF8.GetByteCount(record);
            var total = length + length.ToString().Length;
            if (total.ToString().Length != length.ToString().Length)
                total++;

            _entries.Add(("PaxHeader/x", 'x', Encoding.UTF8.GetBytes(total + record), null));
            _entries.Add(("placeholder", '0', Encoding.UTF8.GetBytes(content), null));
            return this;
        }

        public byte[] Build()
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
                {
                    foreach (var entry in _entries)
                    {
                        gzip.Write(Header(entry.Name, entry.Type, entry.Data.Length, entry.Prefix), 0, 512);
                        gzip.Write(entry.Data, 0, entry.Data.Length);

                        var padding = (512 - entry.Data.Length % 512) % 512;
                        gzip.Write(new byte[padding], 0, padding);
                    }

                    gzip.Write(new byte[1024], 0, 1024);
                }

                return output.ToArray();
            }
        }

        public MemoryStream BuildStream()
        {
            return new MemoryStream(Build());
        }

        private static byte[] Header(string name, char type, long size, string prefix)
        {
            var header = new byte[512];
            Put(header, 0, 100, name);
            Put(header, 100, 8, "0000644");
            Put(header, 108, 8, "0000000");
            Put(header, 116, 8, "0000000");
            Put(header, 124, 12, Convert.ToString(size, 8).PadLeft(11, '0'));
            Put(header, 136, 12, "00000000000");
            header[156] = (byte)type;
            Put(header, 257, 6, "ustar");
            Put(header, 263, 2, "00");
            if (prefix != null)
                Put(header, 345, 155, prefix);

            for (var i = 148; i < 156; i++)
                header[i] = (byte)' ';

            var checksum = 0;
            foreach (var b in header)
                checksum += b;

            Put(header, 148, 7, Convert.ToString(checksum, 8).PadLeft(6, '0'));
            return header;
        }

        private static void Put(byte[] buffer, int offset, int length, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            Array.Copy(bytes, 0, buffer, offset, Math.Min(bytes.Length, length));
        }
    }
}
