// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using NatsunekoLaboratory.AssetExplorer.Extensions;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace NatsunekoLaboratory.AssetExplorer.Editors
{
    internal class AssetContributionEditor : BaseEditorWindow
    {
        [SerializeField]
        private List<string> _assets;

        [SerializeField]
        private DefaultAsset _dir;

        private bool _isSubmitting;

        [SerializeField]
        private string _name;

        [SerializeField]
        private Button _submit;

        [SerializeField]
        private string _url;

        [SerializeField]
        private string _version;

        /*
        [MenuItem("Window/Natsuneko Laboratory/Asset Explorer/Contribute to AssetDatabase")]
        public static void ShowWindow()
        {
            Show<AssetContributionEditor>("Contribute");
        }
        */

        protected override void OnGuiCreated()
        {
            var root = rootVisualElement;
            root.QuerySelector<Button>("[name='preview-button']").clicked += OnPreviewButtonClicked;

            _submit = root.QuerySelector<Button>("[name='submit-button']");
            _submit.clicked += OnSubmitButtonClicked;
        }

        protected override void OnGuiUpdated()
        {
            if (_submit != null)
            {
                var isValid = _assets != null && _assets.Count > 0 && !string.IsNullOrWhiteSpace(_name) && !string.IsNullOrWhiteSpace(_version) && !_isSubmitting;
                _submit.SetEnabled(isValid);
            }
        }

        private void OnPreviewButtonClicked()
        {
            var path = AssetDatabase.GetAssetPath(_dir);
            if (string.IsNullOrWhiteSpace(path))
                return;

            _assets = Directory.GetFiles(path, "*.meta", SearchOption.AllDirectories)
                               .Select(Path.GetFullPath)
                               .Select(w => MakeRelative(Application.dataPath, w))
                               .ToList();

            var root = rootVisualElement;
            var container = root.QuerySelector<VisualElement>("[name='preview-container']");
            container.Clear();

            if (_assets.Count > 0)
                foreach (var asset in _assets)
                    container.Add(new Label(asset));
            else
                container.Add(new Label("No Assets Found"));
        }

        private async void OnSubmitButtonClicked()
        {
            void Progress(float percentage)
            {
                EditorUtility.DisplayProgressBar("AssetDatabase", "Generating Asset Information......", percentage);
            }

            try
            {
                _isSubmitting = true;

                var total = _assets.Count + 1;

                Progress(0f / total);

                EditorUtility.DisplayProgressBar("AssetDatabase", "Submitting Asset Information......", 99f / 100f);

                await Task.Delay(1000);

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("AssetDatabase", "Submit Completed. Thank you for your contributing!", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("AssetDatabase", "Submit Failed. Please retry again later!", "OK");

                Debug.LogException(e);
            }
            finally
            {
                _isSubmitting = false;
            }
        }

        protected override string XamlGuid()
        {
            return "759c64cc29b893445866f2c4f4fd73b9";
        }

        private static string MakeRelative(string from, string to)
        {
            var fromUri = new Uri(from);
            var toUri = new Uri(to);

            if (fromUri.Scheme != toUri.Scheme)
                return to;

            var rel = fromUri.MakeRelativeUri(toUri);
            var path = Uri.UnescapeDataString(rel.ToString());

            if (toUri.Scheme == "file")
                path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            return path;
        }
    }
}