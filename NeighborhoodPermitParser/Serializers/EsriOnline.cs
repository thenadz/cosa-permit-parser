using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeighborhoodPermitParser.Serializers
{
    public class EsriOnline
    {
        public string type { get; set; }
        public Feature[] features { get; set; }
    }

    public class Feature
    {
        public string type { get; set; }
        public int id { get; set; }
        public Geometry geometry { get; set; }
        public Properties properties { get; set; }
    }

    public class Geometry
    {
        public string type { get; set; }
        public float[] coordinates { get; set; }
    }

    public class Properties
    {
        public int OBJECTID { get; set; }
        public int AddrKey { get; set; }
        public int HouseNumber { get; set; }
        public int StreetNameID { get; set; }
        public string PreDirection { get; set; }
        public string Name { get; set; }
        public string AbbrevType { get; set; }
        public object PostDirection { get; set; }
        public string TownCode { get; set; }
        public string CouncilDist { get; set; }
        public float XCoord { get; set; }
        public float YCoord { get; set; }
        public long EntryDate { get; set; }
        public long ModifiedDate { get; set; }
        public string Address { get; set; }
        public object ExpiredDate { get; set; }
    }

}
