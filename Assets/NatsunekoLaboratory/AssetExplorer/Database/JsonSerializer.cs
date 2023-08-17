// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using UnityEngine;

namespace NatsunekoLaboratory.AssetExplorer.Database
{
    internal static class JsonSerializer
    {
        private static Dictionary<string, JsonSerializerType> JsonSerializers => new Dictionary<string, JsonSerializerType>
        {
            { "System.Text.Json.JsonSerializer", JsonSerializerType.SystemTextJson },
            { "Newtonsoft.Json.JsonConvert", JsonSerializerType.NewtonsoftJson },
            { "Unity.Plastic.Newtonsoft.Json.JsonConvert", JsonSerializerType.NewtonsoftJson }
        };

        private static JsonSerializerType CurrentJsonSerializerType { get; }
        private static Type CurrentJsonSerializer { get; }

        static JsonSerializer()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // 効率が悪いのは分かったうえで、 Dictionary の順番で解決したいため
            foreach (var serializer in JsonSerializers)
            {
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var t = assembly.GetType(serializer.Key);
                        CurrentJsonSerializerType = serializer.Value;
                        CurrentJsonSerializer = t;

                        if (t != null)
                            return;
                    }
                    catch (Exception ex)
                    {
                        // ignored
                    }
                }
            }


            Debug.LogWarning("Failed to load Json serializer");
        }

        private static void Assert()
        {
            if (CurrentJsonSerializer == null)
                throw new NotSupportedException("");
        }

        private static Type T(string name)
        {
            return CurrentJsonSerializer.Assembly.GetType(name) ?? throw new InvalidOperationException();
        }

        private static void Set(object o, string name, object value)
        {
            var t = o.GetType();
            var p = t.GetProperty(name);
            if (p == null)
                throw new InvalidOperationException();

            p.SetValue(o, value);
        }

        public static TObject Deserialize<TObject>(string json) where TObject : class
        {
            Assert();

            switch (CurrentJsonSerializerType)
            {
                case JsonSerializerType.SystemTextJson:
                {
                    var ot = T($"{CurrentJsonSerializer.Namespace}.JsonSerializerOptions");
                    var pt = T($"{CurrentJsonSerializer.Namespace}.JsonNamingPolicy");

                    var p = pt.GetProperty("CamelCase")?.GetMethod.Invoke(null, null);
                    var o = Activator.CreateInstance(ot);

                    Set(o, "PropertyNamingPolicy", p);


                    var m = CurrentJsonSerializer.GetMethod("Deserialize", new[] { typeof(string), ot }) ?? throw new InvalidOperationException();
                    return m.MakeGenericMethod(typeof(TObject)).Invoke(null, new object[] { json, o }) as TObject;
                }

                case JsonSerializerType.NewtonsoftJson:
                {
                    var rt = T($"{CurrentJsonSerializer.Namespace}.Serialization.DefaultContractResolver");
                    var ct = T($"{CurrentJsonSerializer.Namespace}.Serialization.CamelCaseNamingStrategy");

                    var r = Activator.CreateInstance(rt);
                    var c = Activator.CreateInstance(ct);

                    Set(r, "NamingStrategy", c);

                    var st = T($"{CurrentJsonSerializer.Namespace}.JsonSerializerSettings");

                    var s = Activator.CreateInstance(st);

                    Set(s, "ContractResolver", r);


                    var m = CurrentJsonSerializer.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type), st }) ?? throw new InvalidOperationException();
                    return m.Invoke(null, new object[] { json, typeof(TObject), s }) as TObject;
                }
            }

            throw new InvalidOperationException();
        }

        public static string Serialize<TObject>(TObject obj)
        {
            Assert();

            switch (CurrentJsonSerializerType)
            {
                case JsonSerializerType.SystemTextJson:
                {
                    var ot = T($"{CurrentJsonSerializer.Namespace}.JsonSerializerOptions");
                    var pt = T($"{CurrentJsonSerializer.Namespace}.JsonNamingPolicy");

                    var p = pt.GetProperty("CamelCase")?.GetMethod.Invoke(null, null);
                    var o = Activator.CreateInstance(ot);

                    Set(o, "PropertyNamingPolicy", p);

                    var m = CurrentJsonSerializer.GetMethod("Serialize", new[] { typeof(object), typeof(Type), ot }) ?? throw new InvalidOperationException();
                    return m.Invoke(null, new[] { obj, typeof(TObject), o }) as string;
                }

                case JsonSerializerType.NewtonsoftJson:
                {
                    var rt = T($"{CurrentJsonSerializer.Namespace}.Serialization.DefaultContractResolver");
                    var ct = T($"{CurrentJsonSerializer.Namespace}.Serialization.CamelCaseNamingStrategy");

                    var r = Activator.CreateInstance(rt);
                    var c = Activator.CreateInstance(ct);

                    Set(r, "NamingStrategy", c);

                    var st = T($"{CurrentJsonSerializer.Namespace}.JsonSerializerSettings");

                    var s = Activator.CreateInstance(st);

                    Set(s, "ContractResolver", r);

                    var m = CurrentJsonSerializer.GetMethod("SerializeObject", new[] { typeof(object), st }) ?? throw new InvalidOperationException();
                    return m.Invoke(null, new object[] { obj, s }) as string;
                }
            }

            throw new InvalidOperationException();
        }

        private enum JsonSerializerType
        {
            SystemTextJson,
            NewtonsoftJson
        }
    }
}