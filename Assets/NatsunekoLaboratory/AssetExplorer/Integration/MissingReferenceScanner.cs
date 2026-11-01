// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using NatsunekoLaboratory.AssetExplorer.Scanning;

using UnityEditor;

using UnityEngine;
using UnityEngine.SceneManagement;

using Object = UnityEngine.Object;

namespace NatsunekoLaboratory.AssetExplorer.Integration
{
    /// <summary>
    ///     Collects the GUIDs that objects and assets reference but the project does not contain.
    ///     Text-serialized files are read as YAML (which also finds Missing Scripts); objects in open scenes are walked
    ///     with <see cref="SerializedObject" />.
    /// </summary>
    internal static class MissingReferenceScanner
    {
        private const string NullGuid = "00000000000000000000000000000000";

        public static List<MissingReference> ScanObject(Object target)
        {
            var collector = new Collector();

            if (target is GameObject go && !EditorUtility.IsPersistent(go))
            {
                ScanGameObject(go, collector);
                return collector.Result();
            }

            var path = AssetDatabase.GetAssetPath(target);
            if (AssetDatabase.IsValidFolder(path))
                return ScanFolder(path);

            if (!string.IsNullOrEmpty(path))
                ScanAssetFile(path, collector);

            return collector.Result();
        }

        public static List<MissingReference> ScanOpenScenes()
        {
            var collector = new Collector();

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                    ScanGameObject(root, collector);

                // the saved file also names the GUIDs of Missing Scripts, which the loaded objects no longer know
                if (!string.IsNullOrEmpty(scene.path))
                    ScanAssetFile(scene.path, collector);
            }

            return collector.Result();
        }

        public static List<MissingReference> ScanFolder(string assetFolder)
        {
            var collector = new Collector();
            var paths = AssetDatabase.FindAssets("", new[] { assetFolder })
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .Where(w => !string.IsNullOrEmpty(w) && !AssetDatabase.IsValidFolder(w))
                                     .Distinct()
                                     .ToList();

            try
            {
                for (var i = 0; i < paths.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Petrichor", $"Missing 参照を探しています… {paths[i]}", (float)i / paths.Count))
                        break;

                    ScanAssetFile(paths[i], collector);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return collector.Result();
        }

        private static void ScanAssetFile(string assetPath, Collector collector)
        {
            string text;
            try
            {
                text = ReadHead(assetPath);
            }
            catch (IOException)
            {
                return;
            }

            if (YamlGuidExtractor.IsYaml(text))
            {
                foreach (var (guid, key) in YamlGuidExtractor.Extract(File.ReadAllText(ProjectPaths.ToPhysical(assetPath))))
                    if (IsMissing(guid))
                        collector.Add(guid, string.IsNullOrEmpty(key) ? assetPath : $"{assetPath} · {key}");

                return;
            }

            // binary-serialized assets: walk the loaded objects instead
            if (!IsSerializedAsset(assetPath))
                return;

            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath).Where(w => w != null))
                if (obj is GameObject go)
                    ScanGameObject(go, collector);
                else
                    ScanSerializedObject(obj, $"{assetPath} · {obj.name}", collector);
        }

        private static void ScanGameObject(GameObject root, Collector collector)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            foreach (var component in transform.GetComponents<Component>())
                if (component != null)
                    ScanSerializedObject(component, $"{HierarchyPath(transform)} · {component.GetType().Name}", collector);
        }

        private static void ScanSerializedObject(Object obj, string location, Collector collector)
        {
            using (var so = new SerializedObject(obj))
            {
                var property = so.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    // "Missing" (as opposed to "None") keeps the referenced object's ID, which still maps to its GUID
                    if (property.objectReferenceValue != null || !TryGetMissingGuid(property, out var guid))
                        continue;

                    if (IsMissing(guid))
                        collector.Add(guid, $"{location} · {property.displayName}");
                }
            }
        }

        private static bool TryGetMissingGuid(SerializedProperty property, out string guid)
        {
#if UNITY_6000_3_OR_NEWER
            // Unity 6.3+ replaces instance IDs with EntityId; the int APIs are obsolete errors in newer versions
            var id = property.objectReferenceEntityIdValue;
            guid = null;
            return id != EntityId.None && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(id, out guid, out long _);
#else
            var id = property.objectReferenceInstanceIDValue;
            guid = null;
            return id != 0 && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(id, out guid, out long _);
#endif
        }

        private static bool IsMissing(string guid)
        {
            if (string.IsNullOrEmpty(guid) || guid == NullGuid)
                return false;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return true;

            // built-in resources resolve to these pseudo paths
            if (path.StartsWith("Library/", StringComparison.Ordinal) || path.StartsWith("Resources/unity_builtin_extra", StringComparison.Ordinal))
                return false;

            return !ProjectPaths.Exists(path);
        }

        private static bool IsSerializedAsset(string assetPath)
        {
            var extension = Path.GetExtension(assetPath).ToLowerInvariant();
            return extension == ".prefab" || extension == ".unity" || extension == ".asset" || extension == ".mat" || extension == ".controller" || extension == ".anim" || extension == ".overridecontroller";
        }

        private static string ReadHead(string assetPath)
        {
            using (var stream = File.OpenRead(ProjectPaths.ToPhysical(assetPath)))
            {
                var buffer = new byte[5];
                var read = stream.Read(buffer, 0, buffer.Length);
                return System.Text.Encoding.ASCII.GetString(buffer, 0, read);
            }
        }

        private static string HierarchyPath(Transform transform)
        {
            var names = new List<string>();
            for (var t = transform; t != null; t = t.parent)
                names.Add(t.name);

            names.Reverse();
            return string.Join("/", names);
        }

        private sealed class Collector
        {
            private readonly Dictionary<string, MissingReference> _byGuid = new Dictionary<string, MissingReference>();
            private readonly List<MissingReference> _ordered = new List<MissingReference>();

            public void Add(string guid, string location)
            {
                guid = guid.ToLowerInvariant();
                if (!_byGuid.TryGetValue(guid, out var reference))
                {
                    _byGuid[guid] = reference = new MissingReference(guid);
                    _ordered.Add(reference);
                }

                if (!reference.Locations.Contains(location))
                    reference.Locations.Add(location);
            }

            public List<MissingReference> Result()
            {
                return _ordered.OrderBy(w => w.Locations[0], StringComparer.Ordinal).ToList();
            }
        }
    }
}
