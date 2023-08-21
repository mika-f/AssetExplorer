// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.IO;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace NatsunekoLaboratory.AssetExplorer.Editors
{
#if USTYLED
    using UStyled;
    using UStyled.Configurations;
    using UStyled.Presets;
#endif


    public abstract class BaseEditorWindow : EditorWindow
    {
        private const string StyleGuid = "d52ceb94bb55646499e577e1be483701";

#if USTYLED

        private static readonly UStyledCompiler UStyled;

#endif

        private SerializedObject _so;


#if USTYLED

        static BaseEditorWindow()
        {
            UStyled = new UStyledCompiler().DefineConfig(new ConfigurationProvider { Presets = { new PrimitivePreset() } });
        }

#endif

        protected static T Show<T>(string title) where T : EditorWindow
        {
            var window = GetWindow<T>();
            window.titleContent = new GUIContent(title);
            window.Show();

            return window;
        }

#if USTYLED

        protected void GenerateStaticCss(string path)
        {
            var xaml = LoadAssetByGuid<VisualTreeAsset>(XamlGuid());
            var uss = UStyled.Compile(xaml);
            File.WriteAllText(path, uss);
        }

#endif

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

#if USTYLED

            root.styleSheets.Add(UStyled.JitCompile(xaml));

#endif

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