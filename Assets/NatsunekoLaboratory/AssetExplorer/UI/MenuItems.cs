// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using NatsunekoLaboratory.AssetExplorer.UI.Tabs;

using UnityEditor;

namespace NatsunekoLaboratory.AssetExplorer.UI
{
    internal static class MenuItems
    {
        private const string WindowRoot = "Window/Natsuneko Laboratory/Petrichor/";

        [MenuItem(WindowRoot + "Petrichor", false, 0)]
        private static void OpenLookup()
        {
            PetrichorWindow.Open(PetrichorTab.Lookup);
        }

        [MenuItem(WindowRoot + "Find Missing References", false, 1)]
        private static void OpenMissing()
        {
            PetrichorWindow.Open(PetrichorTab.Missing);
        }

        [MenuItem(WindowRoot + "Contribute", false, 2)]
        private static void OpenContribute()
        {
            PetrichorWindow.Open(PetrichorTab.Contribute);
        }

        [MenuItem(WindowRoot + "My Submissions", false, 3)]
        private static void OpenSubmissions()
        {
            PetrichorWindow.Open(PetrichorTab.Submissions);
        }

        [MenuItem(WindowRoot + "Settings", false, 20)]
        private static void OpenSettings()
        {
            PetrichorWindow.Open(PetrichorTab.Settings);
        }

        [MenuItem("Assets/Petrichor/Find Missing References", false, 2000)]
        private static void FindMissingInAsset()
        {
            PetrichorWindow.Open(PetrichorTab.Missing).View<MissingTab>(PetrichorTab.Missing).Scan(Selection.activeObject);
        }

        [MenuItem("Assets/Petrichor/Find Missing References", true)]
        private static bool CanFindMissingInAsset()
        {
            return Selection.activeObject != null && EditorUtility.IsPersistent(Selection.activeObject);
        }

        [MenuItem("Assets/Petrichor/Contribute This Folder", false, 2001)]
        private static void ContributeFolder()
        {
            PetrichorWindow.Open(PetrichorTab.Contribute).View<ContributeTab>(PetrichorTab.Contribute).SetFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        [MenuItem("Assets/Petrichor/Contribute This Folder", true)]
        private static bool CanContributeFolder()
        {
            return Selection.activeObject != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        [MenuItem("GameObject/Petrichor/Find Missing References", false, 49)]
        private static void FindMissingInGameObject()
        {
            PetrichorWindow.Open(PetrichorTab.Missing).View<MissingTab>(PetrichorTab.Missing).Scan(Selection.activeGameObject);
        }

        [MenuItem("GameObject/Petrichor/Find Missing References", true)]
        private static bool CanFindMissingInGameObject()
        {
            return Selection.activeGameObject != null;
        }
    }
}
