// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;

using NatsunekoLaboratory.AssetExplorer.Integration;
using NatsunekoLaboratory.AssetExplorer.UI;
using NatsunekoLaboratory.AssetExplorer.UI.Tabs;

using NUnit.Framework;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

using Object = UnityEngine.Object;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Editor
{
    // Runs only inside Unity (the .NET harness compiles Core/ only).
    public class EditorIntegrationTests
    {
        private const string Folder = "Assets/PetrichorIntegrationTestTemp";
        private const string MissingShaderGuid = "0123456789abcdef0123456789abcdef";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", "PetrichorIntegrationTestTemp");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void Window_builds_every_tab()
        {
            var window = ScriptableObject.CreateInstance<PetrichorWindow>();
            try
            {
                window.CreateGUI();

                foreach (PetrichorTab tab in Enum.GetValues(typeof(PetrichorTab)))
                {
                    window.Select(tab);

                    var body = window.rootVisualElement.Q<ScrollView>().contentContainer;
                    Assert.That(body.childCount, Is.EqualTo(1), tab.ToString());
                    Assert.That(body[0].GetType().Name, Is.EqualTo(tab + "Tab"));
                }

                Assert.That(window.rootVisualElement.Query<Button>(className: "petrichor-tab").ToList().Count, Is.EqualTo(5));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void Folder_manifest_matches_asset_database()
        {
            File.WriteAllText(Path.Combine(Folder, "a.txt"), "a");
            AssetDatabase.CreateFolder(Folder, "Sub");
            File.WriteAllText(Path.Combine(Folder, "Sub", "b.txt"), "b");
            AssetDatabase.Refresh();

            var assets = ManifestSources.FromFolder(Folder);

            Assert.That(assets.Select(w => w.Path), Is.EqualTo(new[] { $"{Folder}/Sub/b.txt", $"{Folder}/a.txt" }.OrderBy(w => w, StringComparer.Ordinal)));
            Assert.That(assets.Select(w => w.Guid), Is.EqualTo(assets.Select(w => AssetDatabase.AssetPathToGUID(w.Path))));
            Assert.That(assets.Select(w => w.Type), Has.All.EqualTo("TextScriptImporter"));
        }

        [Test]
        public void Missing_shader_in_material_is_found_from_yaml()
        {
            var path = $"{Folder}/Broken.mat";
            File.WriteAllText(path, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n  m_ObjectHideFlags: 0\n  m_Name: Broken\n" +
                                    $"  m_Shader: {{fileID: 4800000, guid: {MissingShaderGuid}, type: 3}}\n");
            AssetDatabase.ImportAsset(path);

            var missing = MissingReferenceScanner.ScanObject(AssetDatabase.LoadMainAssetAtPath(path));

            Assert.That(missing.Select(w => w.Guid), Is.EqualTo(new[] { MissingShaderGuid }));
            Assert.That(missing[0].Locations, Is.EqualTo(new[] { $"{path} · m_Shader" }));
        }

        private static TextField Field(VisualElement root, string label)
        {
            return root.Query<TextField>().ToList().First(w => w.label == label);
        }

        private string WriteBrokenMaterial(string name)
        {
            var path = $"{Folder}/{name}.mat";
            File.WriteAllText(path, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n  m_ObjectHideFlags: 0\n  m_Name: Broken\n" +
                                    $"  m_Shader: {{fileID: 4800000, guid: {MissingShaderGuid}, type: 3}}\n");
            AssetDatabase.ImportAsset(path);
            return path;
        }

        [Test]
        public void New_lookup_cancels_the_previous_one()
        {
            var tab = new LookupTab();
            try
            {
                Field(tab, "GUID").value = MissingShaderGuid;
                tab.Search();
                Assert.That(tab.IsBusy, Is.True);

                Field(tab, "GUID").value = "not a guid";
                tab.Search();
                Assert.That(tab.IsBusy, Is.False);
            }
            finally
            {
                tab.OnClosed();
            }
        }

        [Test]
        public void New_missing_scan_cancels_the_previous_lookup()
        {
            var broken = AssetDatabase.LoadMainAssetAtPath(WriteBrokenMaterial("Broken"));
            var finePath = $"{Folder}/Fine.mat";
            AssetDatabase.CreateAsset(new Material(Shader.Find("Unlit/Color")), finePath);

            var tab = new MissingTab();
            try
            {
                tab.Scan(broken);
                Assert.That(tab.IsBusy, Is.True);

                tab.Scan(AssetDatabase.LoadMainAssetAtPath(finePath));
                Assert.That(tab.IsBusy, Is.False);
            }
            finally
            {
                tab.OnClosed();
            }
        }

        [Test]
        public void New_contribute_source_cancels_unitypackage_loading()
        {
            File.WriteAllText(Path.Combine(Folder, "a.txt"), "a");
            AssetDatabase.Refresh();
            var package = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".unitypackage");
            File.WriteAllBytes(package, new Contribute.TarBuilder().File($"{MissingShaderGuid}/pathname", "Assets/Old.mat").Build());

            var tab = new ContributeTab();
            try
            {
                tab.LoadUnityPackage(package);
                Assert.That(tab.IsLoadingSource, Is.True);

                tab.SetFolder(Folder);
                Assert.That(tab.IsLoadingSource, Is.False);
            }
            finally
            {
                tab.OnClosed();
                File.Delete(package);
            }
        }

        [Test]
        public void New_contribute_source_clears_prefilled_package_fields()
        {
            AssetDatabase.CreateFolder(Folder, "WithPackage");
            AssetDatabase.CreateFolder(Folder, "Plain");
            File.WriteAllText($"{Folder}/WithPackage/package.json", "{\"name\":\"com.example.a\",\"displayName\":\"A\",\"version\":\"1.0.0\",\"description\":\"d\"}");
            File.WriteAllText($"{Folder}/Plain/b.txt", "b");
            AssetDatabase.Refresh();

            var tab = new ContributeTab();
            try
            {
                tab.SetFolder($"{Folder}/WithPackage");
                Assert.That(Field(tab, "値").value, Is.EqualTo("com.example.a"));
                Assert.That(Field(tab, "バージョン").value, Is.EqualTo("1.0.0"));

                tab.SetFolder($"{Folder}/Plain");
                Assert.That(Field(tab, "値").value, Is.Empty);
                Assert.That(Field(tab, "バージョン").value, Is.Empty);
                Assert.That(Field(tab, "説明").value, Is.Empty);
                Assert.That(tab.Query<TextField>().ToList().First(w => w.label.StartsWith("名前", StringComparison.Ordinal)).value, Is.Empty);
            }
            finally
            {
                tab.OnClosed();
            }
        }

        [Test]
        public void Builtin_references_are_not_missing()
        {
            var path = $"{Folder}/Fine.mat";
            AssetDatabase.CreateAsset(new Material(Shader.Find("Unlit/Color")), path);
            AssetDatabase.SaveAssets();

            Assert.That(MissingReferenceScanner.ScanObject(AssetDatabase.LoadMainAssetAtPath(path)), Is.Empty);
        }
    }
}
