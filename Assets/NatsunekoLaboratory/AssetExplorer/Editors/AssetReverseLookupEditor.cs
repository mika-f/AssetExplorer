// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using NatsunekoLaboratory.AssetExplorer.Database;
using NatsunekoLaboratory.AssetExplorer.Extensions;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

using Object = UnityEngine.Object;

namespace NatsunekoLaboratory.AssetExplorer.Editors
{
    internal class AssetReverseLookupEditor : BaseEditorWindow
    {
        [SerializeField]
        private Object _go;

        [SerializeField]
        private string _guid;

        private bool _isSubmitting;

        private Button _search;

        [MenuItem("Window/Natsuneko Laboratory/Asset Explorer/Reverse Lookup from AssetDatabase")]
        public static void ShowWindow()
        {
            Show<AssetReverseLookupEditor>("Reverse Lookup");
        }

#if USTYLED

        [MenuItem("Window/Natsuneko Laboratory/Asset Explorer/Reverse Lookup - Generate Static CSS")]
        public static void GenerateStaticCss()
        {
            var window = Show<AssetReverseLookupEditor>("Reverse Lookup");
            window.GenerateStaticCss(Path.Combine(Application.dataPath, "NatsunekoLaboratory", "AssetExplorer", "Assets", "AssetReverseLookupEditor.Generated.uss"));
        }

#endif

        protected override void OnGuiCreated()
        {
            var root = rootVisualElement;
            _search = root.QuerySelector<Button>("[name='search-button']");
            _search.clicked += OnSearchButtonClicked;
        }

        protected override void OnGuiUpdated()
        {
            if (_search != null)
            {
                var isValid = (_go != null || !string.IsNullOrWhiteSpace(_guid)) && !_isSubmitting;
                _search.SetEnabled(isValid);
            }
        }

        private async void OnSearchButtonClicked()
        {
            var root = rootVisualElement;
            var container = root.QuerySelector<VisualElement>("[name='result-container']");

            void ToggleContainer(bool r)
            {
                var n = container.QuerySelector<VisualElement>("[name='not-found']");
                var m = container.QuerySelector<VisualElement>("[name='found']");

                if (r)
                {
                    n.AddToClassList("none");
                    n.RemoveFromClassList("flex");
                    m.AddToClassList("flex");
                    m.RemoveFromClassList("none");
                }
                else
                {
                    n.AddToClassList("flex");
                    n.RemoveFromClassList("none");
                    m.AddToClassList("none");
                    m.RemoveFromClassList("flex");
                }
            }

            try
            {
                _isSubmitting = true;

                EditorUtility.DisplayProgressBar("AssetDatabase", "Searching Asset Information...", 1f);

                var r = _go != null ? await SearchObjectReferences(container) : await SearchGuid(container);
                ToggleContainer(r);
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                ToggleContainer(false);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isSubmitting = false;
            }
        }

        private VisualElement CreateContainer(ResultContainer r, KeyValuePair<string, string> property)
        {
            var root = new VisualElement();
            root.AddClasses("border-1px", "border-cccccc", "p-2", "my-1");

            var scroller = new ScrollView(ScrollViewMode.Horizontal);

            if (!property.Equals(default(KeyValuePair<string, string>)))
            {
                var heading = new Label($"Property: {property.Key}");
                heading.AddClasses("bold", "text-lg", "mb-2");
                root.Add(heading);
            }

            var result = new VisualElement();
            result.AddClasses("flex", "flex-row");

            var left = new VisualElement();
            left.AddClasses("w-160px", "flex-shrink-0");

            var labels = new[] { "GUID", "PACKAGE", "URL", "WELL KNOWN NAME", "WELL KNOWN TYPE", "CANONICAL NAME", "VERSIONS IN", "VERIFIED BY USERS" };
            foreach (var label in labels)
            {
                var l = new Label($"{label} :");
                l.AddClasses("h-18px", "bold", "text-sm", "mb-1");

                left.Add(l);
            }

            var right = new VisualElement();
            right.AddClasses("flex-grow-1", "min-w-290px");


            var values = new[] { r.Id, r.Name, r.Url, r.WellKnownName, r.WellKnownType, r.WellKnownName, string.Join(",", r.Versions), "No" };
            var isNotFoundOnDatabase = values.Skip(1).Take(values.Length - 2).All(string.IsNullOrWhiteSpace);
            foreach (var value in values.Select(w => string.IsNullOrWhiteSpace(w) ? "Unknown" : w))
            {
                var v = new Label(isNotFoundOnDatabase ? "Not Found" : value);
                v.AddClasses("h-18px", "text-sm", "mb-1");

                right.Add(v);
            }

            result.Add(left);
            result.Add(right);

            scroller.Add(result);
            root.Add(scroller);

            return root;
        }


        private async Task<bool> SearchObjectReferences(VisualElement container)
        {
            var so = new SerializedObject(_go);
            var properties = new Dictionary<string, string>();
            var current = so.GetIterator();

            while (current.Next(true))
                if (current.propertyType == SerializedPropertyType.ObjectReference)
                    if (current.objectReferenceValue == null && current.objectReferenceInstanceIDValue != 0)
                    {
                        var instanceId = current.objectReferenceInstanceIDValue;
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(instanceId, out var guid, out long _);


                        if (properties.ContainsKey(current.displayName))
                            continue;

                        properties.Add(current.displayName, guid);
                    }

            if (properties.Count == 0)
                return false;


            var c = container.QuerySelector<VisualElement>("[name='found']");
            c.Clear();

            foreach (var property in properties)
                try
                {
                    var response = await AssetDatabaseClient.Instance.FindAssetByGuid(property.Value);
                    foreach (var package in response.Packages)
                    {
                        var result = new ResultContainer
                        {
                            Id = response.Id,
                            WellKnownName = response.WellKnownName,
                            WellKnownType = response.WellKnownType,
                            Name = package.Name,
                            Url = package.Url,
                            Versions = package.Versions.ToList()
                        };

                        c.Add(CreateContainer(result, property));
                    }
                }
                catch
                {
                    // ignored
                }

            return c.childCount > 0;
        }

        private async Task<bool> SearchGuid(VisualElement container)
        {
            var response = await AssetDatabaseClient.Instance.FindAssetByGuid(_guid);

            var c = container.QuerySelector<VisualElement>("[name='found']");
            c.Clear();

            foreach (var package in response.Packages)
            {
                var result = new ResultContainer
                {
                    Id = response.Id,
                    WellKnownName = response.WellKnownName,
                    WellKnownType = response.WellKnownType,
                    Name = package.Name,
                    Url = package.Url,
                    Versions = package.Versions.ToList()
                };

                c.Add(CreateContainer(result, default));
            }


            return true;
        }


        protected override string XamlGuid()
        {
            return "30b25e96ff4452c4a9363008c1e78a27";
        }

        protected override string AdditionalStyleSheetGuid()
        {
            return "e0e00a63d0c631d4ab1b13964610493d";
        }

        private class ResultContainer
        {
            public string Id { get; set; }
            public string WellKnownName { get; set; }
            public string WellKnownType { get; set; }
            public string Name { get; set; }
            public string Url { get; set; }
            public List<string> Versions { get; set; }
        }
    }
}