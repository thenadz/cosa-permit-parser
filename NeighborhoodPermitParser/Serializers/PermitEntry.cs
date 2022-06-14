using CsvHelper.Configuration.Attributes;
using System;
using System.Diagnostics;

namespace NeighborhoodPermitParser.Serializers
{
    [DebuggerDisplay("{PermitType} - {ProjectName}")]
    public class PermitEntry
    {
        /// <summary>
        /// The text description of the type of record.
        /// Note: Application and permit records in Hansen share the same description. 
        /// For the Applications Submitted table, this is the name of the application record in Accela. 
        /// For the Permits Issued table, this is the name of the permit record in Accela.
        /// </summary>
        [Name("PERMIT TYPE")]
        public string PermitType { get; set; }

        /// <summary>
        /// The ID number assigned to either the application or the permit record in Accela. 
        /// Note: In Hansen, applications and records share the unique ID number. 
        /// Accela data includes combination permits that include more than one permit type in one ID. 
        /// In these instances, the value in the "Permit #" column will be the same for multiple rows, 
        /// but the value in the "Permit Type" column will reflect the type of permit that was issued 
        /// for a particular project.
        /// </summary>
        [Name("PERMIT #")]
        public string PermitNumber { get; set; }

        /// <summary>
        /// The name of the project provided by the applicant
        /// </summary>
        [Name("PROJECT NAME")]
        public string ProjectName { get; set; }

        /// <summary>
        /// Whether the application or permit describes NEW or EXISTING construction.
        /// </summary>
        [Name("WORK TYPE")]
        public string WorkType { get; set; }

        /// <summary>
        /// The project or site address (e.g. 1901 S Alamo St)
        /// </summary>
        [Name("ADDRESS")]
        public string Address { get; set; }

        /// <summary>
        /// Any additional location information provided by applicant.
        /// </summary>
        [Name("LOCATION")]
        public string Location { get; set; }

        /// <summary>
        /// The date that the application was submitted
        /// </summary>
        [Name("DATE SUBMITTED")]
        [Format("yyyy-MM-dd")]
        public DateTime DateSubmitted { get; set; }

        /// <summary>
        /// The date that the permit was issued
        /// </summary>
        [Name("DATE ISSUED")]
        [Format("yyyy-MM-dd")]
        [Optional]
        public DateTime? DateIssued { get; set; }

        /// <summary>
        /// The project’s declared valuation
        /// </summary>
        [Name("DECLARED VALUATION")]
        public string DeclaredValuation { get; set; }

        /// <summary>
        /// The project’s building area, in square feet
        /// </summary>
        [Name("AREA (SF)")]
        public string Area { get; set; }

        /// <summary>
        /// The primary contact on the application or permit record
        /// </summary>
        [Name("PRIMARY CONTACT")]
        public string PrimaryContact { get; set; }

        /// <summary>
        /// The council district where the project will take place (as applicable)
        /// </summary>
        [Name("CD")]
        public string CouncilDistrict { get; set; }

        /// <summary>
        /// The Neighborhood Conservation District where the project will take place (as applicable)
        /// </summary>
        [Name("NCD")]
        public string NeighborhoodConservationDistrict { get; set; }

        /// <summary>
        /// The Historic District where the project will take place (as applicable)
        /// </summary>
        [Name("HD")]
        public string HistoricDistrict { get; set; }

        /// <summary>
        /// State Plane Coordinate System (SPCS83 Code TX-4204) northing
        /// </summary>
        [Name("X_COORD")]
        public double? X_COORD { get; set; }

        /// <summary>
        /// State Plane Coordinate System (SPCS83 Code TX-4204) easting
        /// </summary>
        [Name("Y_COORD")]
        public double? Y_COORD { get; set; }
    }
}
