using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using CsvHelper;
using NeighborhoodPermitParser.Serializers;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.ShapeFile.Extended.Entities;


namespace NeighborhoodPermitParser
{
    public class PermitRunner
    {
        /// <summary>
        /// URL for applications submitted this calendar year. Found here: https://data.sanantonio.gov/dataset/building-permits
        /// </summary>
        public const string PERMIT_REPORT_URL = "https://data.sanantonio.gov/dataset/05012dcb-ba1b-4ade-b5f3-7403bc7f52eb/resource/fbb7202e-c6c1-475b-849e-c5c2cfb65833/download/accelasubmitpermitsextract.csv";

        /// <summary>
        /// Manual mapping for neighborhood names in City's official neighborhood listing when they cannot be automatically
        /// matched with City's official GIS records.
        /// </summary>
        private static readonly Dictionary<string, string> manualMappingOverride = new Dictionary<string, string>
            {
                { "CANTERBERY FARMS", "Canterbury Farms" },
                { "CAMELOT I", "Camelot 1" },
                { "COUNTRYSIDE SAN PEDROHOA", "Countryside San Pedro POA" },
                { "CROWN MEADOWS TOWNHOMES", "Crown Meadows West Townhomes" },
                { "ESTATES AT ARROWHEAD", "Arrowhead" },
                { "GARDENS AT BROOKHOLLOW", "Gardens at Brook Hollow" }, // Brookhollow vs Brook Hollow
                { "GARDENS OF OAK HALLOW", "Gardens of Oak Hollow/North Central Thousand Oaks" }, // Hallow vs Hollow
                { "GREEN MOUNTAIN/EMERALD POINTE", "Green Mountain AKA Emerald Pointe" },
                { "HEIN ORCHARD", "Hein-Orchard" },
                { "DOWNTOWN RESIDENTS", "Downtown" },
                { "LORENCE CREEK", "Lorrence Creek" }, // Lorence vs Lorrence
                { "NEIGHBORS ON HONEY HILL", "Neighbors of Honeyhill" },
                { "165 ASSOCIATION", "165" }, // "165 ASSOCIATION HOA" is throwing off heuristics - association association...
                { "NORTHEAST CROSSING", "NE Crossing" },
                { "NORTHWOODS HILLS IMPROVEMENT CLUB", "Northwood Hills Improvement Club" }, // Northwoods vs Northwood
                { "OAK HAVEN HEIGHTS", "Oak Heaven Heights" },
                { "PARKLANDS", "Parkland" }, // Parklands vs Parkland
                { "SOJO CROSSING TOWNHOMES", "Sojo Crossings Townhomes/The Tobin Hill" }, // Crossing vs Crossings
                { "STEEPLECHASE CONDOMINUM", "Steeplechase Condominium" }, // Condominum vs Condominium
                { "HEIGHTS AT STONE OAK II", "Heights at Stone Oak" },
                { "HILLS OF RIVER MIST", "Hills of Rivermist" }, // River Mist vs Rivermist
                { "VILLAS AT OAKCREEK", "Villas of Oakcreek" }, // At vs Of
            };


        private readonly NeighborhoodListingManager listingMgr = new NeighborhoodListingManager();
        private readonly NeighborhoodGisManager gisMgr = new NeighborhoodGisManager();

        private readonly Dictionary<NeighborhoodListing, IShapefileFeature[]> neighborhoodMapping = new Dictionary<NeighborhoodListing, IShapefileFeature[]>();

        /// <summary>
        /// Maps a given neighborhood listing to all permits for that neighborhood.
        /// </summary>
        public Dictionary<NeighborhoodListing, HashSet<PermitEntry>> NeighborhoodsWithPermits { get; } = new Dictionary<NeighborhoodListing, HashSet<PermitEntry>>();

        public PermitRunner()
        {
            List<NeighborhoodListing> neighborhoodNameMisses = new List<NeighborhoodListing>();

            // Loop through all neighborhoods with email - don't bother if we have no POC we're able to email
            foreach (NeighborhoodListing l in listingMgr.NeighborhoodListing.Where(l => l.Email != null))
            {
                string match = gisMgr.NeighborhoodList.Keys.Where(k => string.Equals(k, l.Name, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                string manualMap = manualMappingOverride.ContainsKey(l.Name) ? manualMappingOverride[l.Name] : null;
                List<string> matches = gisMgr.NeighborhoodList.Keys.Where(k => k.Contains(l.Name, StringComparison.InvariantCultureIgnoreCase)).ToList();

                // We got a single good match using generalized matching
                if (match != null)
                {
                    neighborhoodMapping[l] = new[] { gisMgr.NeighborhoodList[match] };
                }

                // We encountered a known special case and made the manual mapping
                else if (manualMap != null)
                {
                    neighborhoodMapping[l] = new[] { gisMgr.NeighborhoodList[manualMap] };
                }

                // We found multiple matches - in some multi-phase neighborhoods this is actually correct
                else if (matches.Any())
                {
                    neighborhoodMapping[l] = matches.Select(m => gisMgr.NeighborhoodList[m]).ToArray();
                }

                // We got nothing
                else
                {
                    neighborhoodNameMisses.Add(l);
                }
            }

            DateTime start = DateTime.UtcNow;

            // Retrieve current city-wide permit application report
            string csvStr;
            using (WebClient wc = new WebClient())
            {
                csvStr = wc.DownloadString(PERMIT_REPORT_URL);
            }

            HashSet<PermitEntry> neighborhoodMisses = new HashSet<PermitEntry>();

            int permitCount = 0;

            // Read the permit application report
            using (StringReader r = new StringReader(csvStr))
            using (CsvReader csvReader = new CsvReader(r, CultureInfo.InvariantCulture))
            {
                IEnumerable<PermitEntry> records = csvReader.GetRecords<PermitEntry>();
                foreach (PermitEntry record in records)
                {
                    permitCount++;

                    // If we can't place the permit on a map, nothing we can do with it
                    if (!record.X_COORD.HasValue || !record.Y_COORD.HasValue)
                    {
                        continue;
                    }

                    // Hit test permit coordinate against all neighborhoods - identify all that match
                    // Note that some neighborhood boundaries overlap, so multiple neighborhood matches IS valid, though uncommon
                    bool neighborhoodHit = false;
                    Coordinate recordCoord = new Coordinate(record.X_COORD.Value, record.Y_COORD.Value);
                    foreach ((NeighborhoodListing listing, IShapefileFeature[] geometries) in neighborhoodMapping)
                    {
                        if (geometries.Any(f => NeighborhoodGisManager.IsCoordinateInNeighborhood(f, recordCoord)))
                        {
                            if (NeighborhoodsWithPermits.TryGetValue(listing, out HashSet<PermitEntry> set))
                            {
                                set.Add(record);
                            }
                            else
                            {
                                NeighborhoodsWithPermits.Add(listing, new HashSet<PermitEntry> { record });
                            }

                            neighborhoodHit = true;
                        }
                    }

                    if (!neighborhoodHit)
                    {
                        neighborhoodMisses.Add(record);
                    }
                }
            }

            TimeSpan duration = DateTime.UtcNow - start;

            Console.WriteLine($"PermitRunner completed in {duration:mm\\:ss}, reviewing {permitCount} permits and {neighborhoodMapping.Count} neighborhood boundaries.");
        }
    }
}
