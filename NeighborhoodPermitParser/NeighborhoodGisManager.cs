using GeoAPI.CoordinateSystems;
using GeoAPI.CoordinateSystems.Transformations;
using NeighborhoodPermitParser.Serializers;
using NetTopologySuite.IO.ShapeFile.Extended;
using NetTopologySuite.IO.ShapeFile.Extended.Entities;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Algorithm.Locate;
using NetTopologySuite.Geometries;

using GeoCoordinate = GeoAPI.Geometries.Coordinate;

namespace NeighborhoodPermitParser
{
    public class NeighborhoodGisManager
    {
        private const string NEIGHBORHOODS_SHP_FILE = "NeighborhoodAssociations.shp";

        private const string NEIGHBORHOODS_GIS_URL = "https://gis.sanantonio.gov/Download/NeighborhoodAssociations.zip";

        private static readonly string NEIGHBORHOODS_GIS_ROOT = Path.Combine(Utilities.AssemblyDirectory, "NeighborhoodAssociations");

        private static readonly string NEIGHBORHOODS_SHP_PATH = Path.Combine(NEIGHBORHOODS_GIS_ROOT, NEIGHBORHOODS_SHP_FILE);

        private static readonly ICoordinateTransformation coordinateTransform;

        private static readonly string LOCATION_CACHE = Path.Combine(Utilities.AssemblyDirectory, "location-cache.txt");

        private static readonly ConcurrentDictionary<string, Coordinate> locationCache = new ConcurrentDictionary<string, Coordinate>();

        private static readonly ConcurrentBag<string> newCacheEntries = new ConcurrentBag<string>();

        static NeighborhoodGisManager()
        {
            // Reference: https://spatialreference.org/ref/epsg/wgs-84/
            ICoordinateSystem sourceCoordSystem = new CoordinateSystemFactory().CreateFromWkt("GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563,AUTHORITY[\"EPSG\",\"7030\"]],AUTHORITY[\"EPSG\",\"6326\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],UNIT[\"degree\",0.01745329251994328,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4326\"]]");

            // Reference: https://spatialreference.org/ref/epsg/nad83-texas-south-central-ftus/
            ICoordinateSystem targetCoordSystem = new CoordinateSystemFactory().CreateFromWkt("PROJCS[\"NAD83 / Texas South Central (ftUS)\",GEOGCS[\"NAD83\",DATUM[\"North_American_Datum_1983\",SPHEROID[\"GRS 1980\",6378137,298.257222101,AUTHORITY[\"EPSG\",\"7019\"]],AUTHORITY[\"EPSG\",\"6269\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],UNIT[\"degree\",0.01745329251994328,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4269\"]],UNIT[\"US survey foot\",0.3048006096012192,AUTHORITY[\"EPSG\",\"9003\"]],PROJECTION[\"Lambert_Conformal_Conic_2SP\"],PARAMETER[\"standard_parallel_1\",30.28333333333333],PARAMETER[\"standard_parallel_2\",28.38333333333333],PARAMETER[\"latitude_of_origin\",27.83333333333333],PARAMETER[\"central_meridian\",-99],PARAMETER[\"false_easting\",1968500],PARAMETER[\"false_northing\",13123333.333],AUTHORITY[\"EPSG\",\"2278\"],AXIS[\"X\",EAST],AXIS[\"Y\",NORTH]]");

            coordinateTransform = new CoordinateTransformationFactory().CreateFromCoordinateSystems(sourceCoordSystem, targetCoordSystem);

            if (File.Exists(LOCATION_CACHE))
            {
                int count = 0;
                foreach (string entry in File.ReadAllLines(LOCATION_CACHE))
                {
                    count++;
                    string[] parts = entry.Split();
                    if (parts.Length == 3)
                    {
                        locationCache[parts[0]] = new Coordinate(double.Parse(parts[1]), double.Parse(parts[2]));
                    }
                    else
                    {
                        locationCache[parts[0]] = null;
                    }
                }
            }
        }

        public NeighborhoodGisManager()
        {
            if (!File.Exists(NEIGHBORHOODS_SHP_PATH))
            {
                DownloadShapefile();
            }

            ShapeDataReader reader = new ShapeDataReader(NEIGHBORHOODS_SHP_PATH);
            Envelope mbr = reader.ShapefileBounds;

            // Loop through each neighborhood in the shapefile and store for future reference
            foreach (IShapefileFeature n in reader.ReadByMBRFilter(mbr))
            {
                string name = n.Attributes["Name"] as string;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    name = Utilities.SanitizeNeighborhoodName(name);
                    NeighborhoodList[name] = n;
                }
            }
        }

        /// <summary>
        /// Dictionary of each neighborhood's geometry, keyed off of the neighborhood's sanitized name
        /// </summary>
        public Dictionary<string, IShapefileFeature> NeighborhoodList { get; } = new Dictionary<string, IShapefileFeature>();

        /// <summary>
        /// Dynamically retrieve from City's GIS website the current neighborhood shapefile
        /// </summary>
        private static void DownloadShapefile()
        {
            if (!Directory.Exists(NEIGHBORHOODS_GIS_ROOT))
            {
                Directory.CreateDirectory(NEIGHBORHOODS_GIS_ROOT);
            }

            using HttpClient wc = new HttpClient();
            using Task<HttpResponseMessage> getTask = wc.GetAsync(NEIGHBORHOODS_GIS_URL);
            getTask.Wait();
            using HttpContent gisData = getTask.Result.Content;
            using Stream ms = gisData.ReadAsStream();
            using ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Read);

            foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.Name != string.Empty))
            {
                using (FileStream fs = File.OpenWrite(Path.Combine(NEIGHBORHOODS_GIS_ROOT, entry.Name)))
                {
                    entry.Open().CopyTo(fs);
                }
            }
        }

        /// <summary>
        /// Returns whether provided coordinate exists within the provided neighborhood's boundaries
        /// </summary>
        /// <param name="neighborhood">The neighborhood to be checked</param>
        /// <param name="coordinate">The coordinate to be checked</param>
        /// <returns>Whether or not the coordinate lies within the neighborhood's boundaries</returns>
        public static bool IsCoordinateInNeighborhood(IShapefileFeature neighborhood, Coordinate coordinate)
        {
            bool ret = false;

            if (coordinate != null)
            {
                IPointOnGeometryLocator locator = GetNeighborhoodLocator(neighborhood);
                Location l = locator.Locate(coordinate);
                ret = (l == Location.Interior);
            }

            return ret;
        }

        /// <summary>
        /// Locators are needed to enable hit testing coordinate against neighborhood's geometry
        /// </summary>
        private static readonly ConcurrentDictionary<IShapefileFeature, IPointOnGeometryLocator> neighborhoodToLocator =
            new ConcurrentDictionary<IShapefileFeature, IPointOnGeometryLocator>();

        /// <summary>
        /// Retrieves the locator for a given neighborhood, creating one if not already created
        /// </summary>
        /// <param name="neighborhood"></param>
        /// <returns></returns>
        private static IPointOnGeometryLocator GetNeighborhoodLocator(IShapefileFeature neighborhood) =>
            neighborhoodToLocator.GetOrAdd(neighborhood, n => new IndexedPointInAreaLocator(n.Geometry));

        [Obsolete("No longer needed with use of provided X/Y coordinate in permit listing. Left for legacy purposes.")]
        public static Coordinate GetAddressCoord(string address)
        {
            Dictionary<string, string> sanitizedAddress = SanitizeAddress(address);

            string locKey = string.Join('&', sanitizedAddress.Select(kvp => $"{kvp.Key}={kvp.Value}").OrderBy(kvp => kvp));
            if (!locationCache.TryGetValue(locKey, out Coordinate coordinate))
            {
                const string baseQueryUrl = "https://services.arcgis.com/g1fRTDLeMgspWrYp/arcgis/rest/services/COSA_Address/FeatureServer/0/query?f=pgeojson&outFields=*&where=";
                string BuildQueryUrl() => baseQueryUrl + string.Join("+AND+", sanitizedAddress.Select(kvp => $"{kvp.Key}%3D%27{kvp.Value}%27"));

                WebClient c = new WebClient();
                string query = BuildQueryUrl();

                string resultStr = c.DownloadString(query);
                if (resultStr.Contains("\"error\""))
                {
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                    resultStr = c.DownloadString(query);
                }

                EsriOnline result = JsonSerializer.Deserialize<EsriOnline>(resultStr);
                if (result.features.Length == 0 && sanitizedAddress.TryGetValue("HouseNumber", out string houseNumStr))
                {
                    bool hasHouseNum = int.TryParse(houseNumStr, out int houseNum);
                    sanitizedAddress.Remove("HouseNumber");
                    query = BuildQueryUrl();

                    resultStr = c.DownloadString(query);
                    if (resultStr.Contains("\"error\""))
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(10));
                        resultStr = c.DownloadString(query);
                    }

                    result = JsonSerializer.Deserialize<EsriOnline>(resultStr);
                    if (result.features.Length > 1 && hasHouseNum)
                    {
                        result.features[0] = result.features.OrderBy(f => Math.Abs(f.properties.HouseNumber - houseNum)).First();
                        Console.WriteLine($"Unable to find {houseNum} {result.features[0].properties.Name} -- using {result.features[0].properties.HouseNumber} {result.features[0].properties.Name} as standin");
                    }

                    if (hasHouseNum)
                    {
                        sanitizedAddress["HouseNumber"] = houseNumStr;
                    }
                }

                if (result.features.Length == 0 && sanitizedAddress.TryGetValue("AbbrevType", out string abbrType))
                {
                    sanitizedAddress.Remove("AbbrevType");
                    query = BuildQueryUrl();

                    resultStr = c.DownloadString(query);
                    if (resultStr.Contains("\"error\""))
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(10));
                        resultStr = c.DownloadString(query);
                    }

                    result = JsonSerializer.Deserialize<EsriOnline>(resultStr);

                    if (result.features.Length == 0)
                    {
                        sanitizedAddress["Name"] += $" {abbrType}";
                        query = BuildQueryUrl();

                        resultStr = c.DownloadString(query);
                        if (resultStr.Contains("\"error\""))
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(10));
                            resultStr = c.DownloadString(query);
                        }

                        result = JsonSerializer.Deserialize<EsriOnline>(resultStr);
                    }
                }

                if (result.features.Length > 0)
                {
                    GeoCoordinate tmpCoordinate = coordinateTransform.MathTransform.Transform(new GeoCoordinate(result.features[0].geometry.coordinates[0], result.features[0].geometry.coordinates[1]));
                    coordinate = new Coordinate(tmpCoordinate.X, tmpCoordinate.Y);
                    locationCache[locKey] = coordinate;
                    newCacheEntries.Add($"{locKey} {tmpCoordinate.X} {tmpCoordinate.Y}");
                }
                else
                {
                    Console.WriteLine($"Unable to find address: {address}");
                    locationCache[locKey] = null;
                    newCacheEntries.Add(locKey);
                }
            }

            return coordinate;
        }

        [Obsolete("No longer needed with use of provided X/Y coordinate in permit listing. Left for legacy purposes.")]
        private static Dictionary<string, string> SanitizeAddress(string address)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();

            address = address.Replace("City of ", string.Empty);
            address = address.ToUpperInvariant().Split(new[] { "SAN ANTONIO" }, StringSplitOptions.TrimEntries)[0];

            // remove unit number if present -- except when it's a highway number
            if (!address.Contains(" IH ") && !address.Contains(" STATE HIGHWAY ") && !address.Contains(" LOOP "))
            {
                address = Regex.Replace(address, @"\s+\d+$", string.Empty);
            }
            else
            {
                address = Regex.Replace(address, @"\s+(\d+)\s+\d+$", @" $1");
            }

            Match m = Regex.Match(address, @"(?:(\d+)\s+)?(?:([NESW]|[NS][EW])\.?\s+)?([A-Z0-9\.' -]+)");
            if (m.Success)
            {
                string houseNum = m.Groups[1].Value;
                string preDir = m.Groups[2].Value;
                string streetName = m.Groups[3].Value;
                string abbrType = string.Empty;
                string postDir = string.Empty;

                string[] addrParts = streetName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                m = Regex.Match(addrParts[^1], @"^([NESW]|[NS][EW])\.?$");
                if (m.Success)
                {
                    postDir = m.Groups[1].Value;
                    addrParts = addrParts.Take(addrParts.Length - 1).ToArray();
                    streetName = string.Join(' ', addrParts);
                }

                if (addrParts.Length > 1 && new[] { "ST", "RD", "DR", "PL", "LN", "WY", "AVE", "WAY", "HWY", "RUN", "BLVD", "PKWY", "PARKWAY", "COURT", "COVE", "BEND", "PASS" }.Contains(addrParts[^1]))
                {
                    streetName = string.Join(' ', addrParts.Take(addrParts.Length - 1));
                    abbrType = addrParts[^1];
                }
                else if (addrParts.Length > 1 && new[] { "LOOP" }.Contains(addrParts[0]))
                {
                    streetName = string.Join(' ', addrParts.Skip(1));
                }

                ret["HouseNumber"] = houseNum;
                ret["Name"] = streetName;
                ret["AbbrevType"] = abbrType;

                if (!string.IsNullOrWhiteSpace(preDir))
                {
                    ret["PreDirection"] = preDir;
                }

                if (!string.IsNullOrWhiteSpace(postDir))
                {
                    ret["PostDirection"] = postDir;
                }
            }
            else
            {
                ret["Address"] = address;
            }

            foreach (string key in ret.Keys)
            {
                ret[key] = ret[key].Replace(' ', '+').Replace("'", "%27%27");
            }

            return ret;
        }
    }
}
