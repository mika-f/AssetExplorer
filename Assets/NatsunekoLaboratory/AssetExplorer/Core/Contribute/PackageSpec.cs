// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace NatsunekoLaboratory.AssetExplorer.Contribute
{
    internal enum IdentifierProvider
    {
        Package,

        Booth,

        Url
    }

    internal enum PackageSpecKind
    {
        Id,

        Identifier,

        Name
    }

    /// <summary>
    ///     The <c>package</c> part of a submission: an existing package ID, an identifier (with optional name), or a name only.
    /// </summary>
    internal sealed class PackageSpec
    {
        private PackageSpec() { }

        public PackageSpecKind Kind { get; private set; }

        public string Id { get; private set; }

        public IdentifierProvider Provider { get; private set; }

        public string Value { get; private set; }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public static PackageSpec ById(string id)
        {
            return new PackageSpec { Kind = PackageSpecKind.Id, Id = id?.Trim() };
        }

        public static PackageSpec ByIdentifier(IdentifierProvider provider, string value, string name, string description)
        {
            return new PackageSpec { Kind = PackageSpecKind.Identifier, Provider = provider, Value = value?.Trim(), Name = name?.Trim(), Description = description };
        }

        public static PackageSpec ByName(string name, string description)
        {
            return new PackageSpec { Kind = PackageSpecKind.Name, Name = name?.Trim(), Description = description };
        }

        public static string ProviderToJson(IdentifierProvider provider)
        {
            switch (provider)
            {
                case IdentifierProvider.Package:
                    return "package";

                case IdentifierProvider.Booth:
                    return "booth";

                default:
                    return "url";
            }
        }

        public Dictionary<string, object> ToJsonObject()
        {
            var json = new Dictionary<string, object>();

            switch (Kind)
            {
                case PackageSpecKind.Id:
                    json["id"] = Id;
                    return json;

                case PackageSpecKind.Identifier:
                    json["identifier"] = new Dictionary<string, object> { { "provider", ProviderToJson(Provider) }, { "value", Value } };
                    break;
            }

            if (!string.IsNullOrEmpty(Name))
                json["name"] = Name;

            if (!string.IsNullOrWhiteSpace(Description))
                json["description"] = Description;

            return json;
        }
    }
}
