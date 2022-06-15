using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NeighborhoodPermitParser
{
    public static class Utilities
    {
        private static string assemblyDirectory;

        /// <summary>
        /// Get filesystem path relative to the executing assembly to streamline pulling files off the filesystem
        /// </summary>
        public static string AssemblyDirectory
        {
            get
            {
                if (assemblyDirectory == null)
                {
                    string codeBase = Assembly.GetExecutingAssembly().Location;
                    UriBuilder uri = new UriBuilder(codeBase);
                    string path = Uri.UnescapeDataString(uri.Path);
                    assemblyDirectory = Path.GetDirectoryName(path);
                }

                return assemblyDirectory;
            }
        }

        /// <summary>
        /// In order to normalize neighborhood names between the neighborhood registry and the GIS neighborhood entries,
        /// we have to use extensive heuristics to get to common spelling from which we're able to match between the datasets.
        /// </summary>
        /// <param name="dirtyName">The un-sanitized neighborhood name.</param>
        /// <returns>The sanitized name.</returns>
        public static string SanitizeNeighborhoodName(string dirtyName)
        {
            string name = dirtyName.Trim();

            if (name.EndsWith(" ASSN", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^5] + " ASSOCIATION";
            }

            if (name.StartsWith("THE ", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[4..];
            }

            if (name.EndsWith(" HOA", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^4];
            }
            else if (name.EndsWith(" NA", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^3];
            }
            else if (name.EndsWith(" HOMEOWNERS ASSOCIATION", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^23];
            }
            else if (name.EndsWith(" OWNERS ASSOCIATION", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^19];
            }
            else if (name.EndsWith(" RESIDENTS ASSOCIATION", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^22];
            }
            else if (name.EndsWith(" PRESERVATION ASSOCIATION", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^25];
            }
            else if (name.EndsWith(" HISTORICAL ASSOCIATION", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^23];
            }
            else if (name.EndsWith(" COMMUNITY ASSOCIATION", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^22];
            }
            else if (name.EndsWith(" COMMUNITY", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^10];
            }
            else if (name.EndsWith(" AREA", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^5];
            }

            if (name.EndsWith(" RESIDENTIAL COMMUNITY", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^22];
            }

            if (name.EndsWith(" SUBDIVISION", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^12];
            }

            if (name.EndsWith(" ASSOCIATION", StringComparison.InvariantCultureIgnoreCase))
            {
                name = name[0..^12];
            }

            name = name
                .Replace(" AND ", " & ", StringComparison.InvariantCultureIgnoreCase)
                .Replace(" / ", "/", StringComparison.InvariantCultureIgnoreCase)
                .Replace(" - ", " ", StringComparison.InvariantCultureIgnoreCase)

                // Hunter's Chase, Hunter's Creek, Long's Creek, etc - all inconsistent whether ' is used
                .Replace("'S", "S", StringComparison.InvariantCultureIgnoreCase);


            name = Regex.Replace(name, @" OF (?:SAN ANTONIO|SA)$", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @" ROAD( |$)", " RD$1", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"(\w)\.", "$1", RegexOptions.IgnoreCase);

            return name;
        }
    }
}
