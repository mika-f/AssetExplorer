// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using NatsunekoLaboratory.AssetExplorer.Api;

using UnityEngine.UIElements;

namespace NatsunekoLaboratory.AssetExplorer.UI.Components
{
    internal static class AssetLookupView
    {
        /// <summary>Each known path of the GUID with its type and releases (verified first, unverified last).</summary>
        public static VisualElement Paths(AssetLookup asset)
        {
            var container = new VisualElement();

            foreach (var path in asset.Paths)
            {
                var panel = new VisualElement();
                panel.AddToClassList("petrichor-panel");

                panel.Add(MonoFont.Apply(Ui.Text(path.Path, "petrichor-path")));
                if (!string.IsNullOrEmpty(path.Type))
                    panel.Add(Ui.Text(path.Type, "petrichor-hint"));

                var releases = ReleaseOrdering.Sort(path.Releases);
                if (releases.Count == 0)
                    panel.Add(Ui.Text("リリースがありません", "petrichor-hint"));

                foreach (var release in releases)
                    panel.Add(new ReleaseRow(release));

                container.Add(panel);
            }

            return container;
        }
    }
}
