// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace NatsunekoLaboratory.AssetExplorer.Json
{
    /// <summary>
    ///     Tolerant accessors over parsed JSON. A missing key or a value of another type reads as null.
    /// </summary>
    internal static class JsonAccess
    {
        public static string Str(this object obj, string key)
        {
            return Get(obj, key) as string;
        }

        public static long? Long(this object obj, string key)
        {
            switch (Get(obj, key))
            {
                case long l:
                    return l;

                case double d when d >= long.MinValue && d <= long.MaxValue && d == System.Math.Floor(d):
                    return (long)d;

                default:
                    return null;
            }
        }

        public static bool? Bool(this object obj, string key)
        {
            return Get(obj, key) is bool b ? b : (bool?)null;
        }

        public static Dictionary<string, object> Obj(this object obj, string key)
        {
            return Get(obj, key) as Dictionary<string, object>;
        }

        public static List<object> Arr(this object obj, string key)
        {
            return Get(obj, key) as List<object>;
        }

        private static object Get(object obj, string key)
        {
            if (key != null && obj is Dictionary<string, object> dictionary && dictionary.TryGetValue(key, out var value))
                return value;

            return null;
        }
    }
}
