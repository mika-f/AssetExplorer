// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Globalization;
using System.Threading;

using NatsunekoLaboratory.AssetExplorer.Json;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Json
{
    public class JsonTests
    {
        [Test]
        public void Parses_nested_structures()
        {
            var root = (Dictionary<string, object>)JsonReader.Parse("{\"a\":[1,2.5,\"x\",true,null],\"b\":{\"c\":\"d\"}}");
            var a = (List<object>)root["a"];

            Assert.That(a[0], Is.EqualTo(1L));
            Assert.That(a[1], Is.EqualTo(2.5d));
            Assert.That(a[2], Is.EqualTo("x"));
            Assert.That(a[3], Is.EqualTo(true));
            Assert.That(a[4], Is.Null);
            Assert.That(root.Obj("b").Str("c"), Is.EqualTo("d"));
        }

        [Test]
        public void Parses_unicode_escapes_including_surrogate_pairs()
        {
            Assert.That(JsonReader.Parse("\"\\u3042\\ud842\\udfb7\""), Is.EqualTo("あ𠮷"));
        }

        [Test]
        public void Parses_whitespace_and_exponents()
        {
            var root = JsonReader.Parse(" { \"n\" : -1.5e2 , \"m\" : [ ] } ");

            Assert.That(((Dictionary<string, object>)root)["n"], Is.EqualTo(-150d));
            Assert.That(root.Arr("m"), Is.Empty);
        }

        [Test]
        public void Rejects_trailing_garbage()
        {
            Assert.Throws<JsonException>(() => JsonReader.Parse("{} x"));
        }

        [Test]
        public void Rejects_unterminated_string()
        {
            Assert.Throws<JsonException>(() => JsonReader.Parse("{\"a\":\"b"));
        }

        [Test]
        public void Rejects_empty_input()
        {
            Assert.Throws<JsonException>(() => JsonReader.Parse(""));
        }

        [Test]
        public void Writes_escapes()
        {
            var json = JsonWriter.Write(new Dictionary<string, object> { { "p", "a\"b\\c\n\u0001あ" } });

            Assert.That(json, Is.EqualTo("{\"p\":\"a\\\"b\\\\c\\n\\u0001あ\"}"));
        }

        [Test]
        public void Writes_lists_and_primitives()
        {
            var json = JsonWriter.Write(new List<object> { 1, 2L, true, false, null, "s" });

            Assert.That(json, Is.EqualTo("[1,2,true,false,null,\"s\"]"));
        }

        [Test]
        public void Round_trips()
        {
            const string json = "{\"a\":[1,2.5,\"x\",true,null],\"b\":{\"c\":\"d\\u0000\"}}";

            Assert.That(JsonWriter.Write(JsonReader.Parse(json)), Is.EqualTo(json));
        }

        [Test]
        public void Writes_numbers_invariant()
        {
            var culture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

                Assert.That(JsonWriter.Write(1.5), Is.EqualTo("1.5"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
            }
        }

        [Test]
        public void Access_helpers_return_null_for_missing_or_wrong_types()
        {
            var root = JsonReader.Parse("{\"s\":\"x\",\"n\":3,\"b\":true,\"o\":{},\"a\":[]}");

            Assert.That(root.Str("s"), Is.EqualTo("x"));
            Assert.That(root.Long("n"), Is.EqualTo(3L));
            Assert.That(root.Bool("b"), Is.True);
            Assert.That(root.Obj("o"), Is.Not.Null);
            Assert.That(root.Arr("a"), Is.Not.Null);
            Assert.That(root.Str("n"), Is.Null);
            Assert.That(root.Long("s"), Is.Null);
            Assert.That(root.Obj("missing"), Is.Null);
            Assert.That(((object)"not an object").Str("s"), Is.Null);
        }
    }
}
