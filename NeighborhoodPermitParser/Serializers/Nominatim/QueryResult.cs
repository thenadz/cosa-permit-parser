using System.Diagnostics;

namespace NeighborhoodPermitParser.Serializers.Nominatim
{
    [DebuggerDisplay("{display_name}")]
    public class QueryResult
    {
        public int place_id { get; set; }
        public string licence { get; set; }
        public string osm_type { get; set; }
        public long osm_id { get; set; }
        public string[] boundingbox { get; set; }
        public string lat { get; set; }
        public string lon { get; set; }
        public string display_name { get; set; }
        public int place_rank { get; set; }
        public string category { get; set; }
        public string type { get; set; }
        public float importance { get; set; }
    }

}
