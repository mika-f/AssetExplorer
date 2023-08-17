// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using NatsunekoLaboratory.UStyled;
using NatsunekoLaboratory.UStyled.Configurations;
using NatsunekoLaboratory.UStyled.Presets;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace NatsunekoLaboratory.AssetExplorer.Editors
{
    public abstract class BaseEditorWindow : EditorWindow
    {
        private const string StyleGuid = "d52ceb94bb55646499e577e1be483701";
        private static readonly UStyledCompiler UStyled;

        private SerializedObject _so;

        static BaseEditorWindow()
        {
            UStyled = new UStyledCompiler().DefineConfig(new ConfigurationProvider { Presets = { new PrimitivePreset() } });
        }

        protected static void Show<T>(string title) where T : EditorWindow
        {
            var window = GetWindow<T>();
            window.titleContent = new GUIContent(title);
            window.Show();
        }

        private static T LoadAssetByGuid<T>(string guid) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }

        // ReSharper disable once InconsistentNaming
        public void CreateGUI()
        {
            _so = new SerializedObject(this);
            _so.Update();


            var root = rootVisualElement;
            var xaml = LoadAssetByGuid<VisualTreeAsset>(XamlGuid());
            var tree = xaml.CloneTree();

            root.styleSheets.Add(LoadAssetByGuid<StyleSheet>(StyleGuid));

            var additionalStyleSheet = AdditionalStyleSheetGuid();
            if (!string.IsNullOrWhiteSpace(additionalStyleSheet))
                root.styleSheets.Add(LoadAssetByGuid<StyleSheet>(additionalStyleSheet));

            root.styleSheets.Add(UStyled.JitCompile(xaml));

            tree.Bind(_so);
            root.Add(tree);

            OnGuiCreated();
        }

        public void OnGUI()
        {
            OnGuiUpdated();
        }

        protected virtual void OnGuiCreated() { }

        protected virtual void OnGuiUpdated() { }

        protected virtual string AdditionalStyleSheetGuid()
        {
            return null;
        }

        protected abstract string XamlGuid();
    }
}