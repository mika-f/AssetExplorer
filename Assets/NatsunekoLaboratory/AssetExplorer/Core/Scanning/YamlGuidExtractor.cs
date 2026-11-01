// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NatsunekoLaboratory.AssetExplorer.Scanning
{
    /// <summary>
    ///     Finds <c>guid: &lt;32 hex&gt;</c> references in Unity's text serialization (.prefab, .unity, .mat, .asset, …),
    ///     together with the property they belong to.
    /// </summary>
    internal static class YamlGuidExtractor
    {
        private static readonly Regex GuidReference = new Regex(@"guid: ([0-9a-fA-F]{32})", RegexOptions.Compiled);

        public static bool IsYaml(string text)
        {
            return text != null && text.StartsWith("%YAML", StringComparison.Ordinal);
        }

        public static List<(string Guid, string Key)> Extract(string yaml)
        {
            var references = new List<(string, string)>();
            var lines = yaml.Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                var matches = GuidReference.Matches(line);
                if (matches.Count == 0)
                    continue;

                var key = KeyOf(lines, i);
                foreach (Match match in matches)
                    references.Add((match.Groups[1].Value.ToLowerInvariant(), key));
            }

            return references;
        }

        private static string KeyOf(string[] lines, int index)
        {
            var line = lines[index].TrimEnd('\r');
            var indent = Indent(line);
            var content = line.TrimStart();
            var isListItem = content.StartsWith("- ", StringComparison.Ordinal);
            if (isListItem)
                content = content.Substring(2);

            // "m_Script: {fileID: …, guid: …}" or "- target: {…}"
            var brace = content.IndexOf('{');
            var colon = content.IndexOf(':');
            if (colon > 0 && (brace < 0 || colon < brace))
                return content.Substring(0, colon).Trim();

            // "- {fileID: …, guid: …}" belongs to the nearest "m_Materials:" above it at the same or a smaller indent
            for (var i = index - 1; i >= 0; i--)
            {
                var previous = lines[i].TrimEnd('\r');
                var previousContent = previous.TrimStart();
                if (previousContent.Length == 0 || previousContent.StartsWith("-", StringComparison.Ordinal))
                    continue;

                if (Indent(previous) <= indent && previousContent.EndsWith(":", StringComparison.Ordinal))
                    return previousContent.Substring(0, previousContent.Length - 1).Trim();

                if (Indent(previous) < indent)
                    break;
            }

            return "";
        }

        private static int Indent(string line)
        {
            var i = 0;
            while (i < line.Length && line[i] == ' ')
                i++;

            return i;
        }
    }
}
