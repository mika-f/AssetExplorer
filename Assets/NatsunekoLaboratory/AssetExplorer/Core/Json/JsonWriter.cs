// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NatsunekoLaboratory.AssetExplorer.Json
{
    /// <summary>
    ///     Serializes the shapes <see cref="JsonReader" /> produces: string-keyed dictionaries, lists, strings, numbers, booleans and null.
    /// </summary>
    internal static class JsonWriter
    {
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;

                case string s:
                    WriteString(sb, s);
                    break;

                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;

                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;

                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;

                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;

                case float f:
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    break;

                case IDictionary<string, object> dictionary:
                    WriteObject(sb, dictionary);
                    break;

                case IEnumerable enumerable:
                    WriteArray(sb, enumerable);
                    break;

                default:
                    throw new ArgumentException($"Cannot serialize {value.GetType()} as JSON");
            }
        }

        private static void WriteObject(StringBuilder sb, IDictionary<string, object> dictionary)
        {
            sb.Append('{');

            var first = true;
            foreach (var pair in dictionary)
            {
                if (!first)
                    sb.Append(',');

                first = false;
                WriteString(sb, pair.Key);
                sb.Append(':');
                WriteValue(sb, pair.Value);
            }

            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IEnumerable enumerable)
        {
            sb.Append('[');

            var first = true;
            foreach (var item in enumerable)
            {
                if (!first)
                    sb.Append(',');

                first = false;
                WriteValue(sb, item);
            }

            sb.Append(']');
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');

            foreach (var c in s)
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;

                    case '\\':
                        sb.Append("\\\\");
                        break;

                    case '\n':
                        sb.Append("\\n");
                        break;

                    case '\r':
                        sb.Append("\\r");
                        break;

                    case '\t':
                        sb.Append("\\t");
                        break;

                    case '\b':
                        sb.Append("\\b");
                        break;

                    case '\f':
                        sb.Append("\\f");
                        break;

                    default:
                        if (c < 0x20 || c == 0x7f)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }

            sb.Append('"');
        }
    }
}
