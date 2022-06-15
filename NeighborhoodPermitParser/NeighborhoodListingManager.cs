using NanoXLSX;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NeighborhoodPermitParser
{
    public class NeighborhoodListingManager
    {
        /// <summary>
        /// Download source: https://www.sanantonio.gov/comm/Neighborhood-Engagement/Associations-Organizations
        /// </summary>
        private static readonly string NEIGHBORHOOD_LISTINGS_PATH = Path.Combine(Utilities.AssemblyDirectory, "Neighborhood Assoc Listing.xlsx");

        public NeighborhoodListingManager()
        {
            NeighborhoodListing = DownloadNeighborhoodListing();
        }

        /// <summary>
        /// Get all entries in official neighborhood listing
        /// </summary>
        public IReadOnlyCollection<NeighborhoodListing> NeighborhoodListing { get; private set; }

        /// <summary>
        /// Parse neighborhood listing Excel document
        /// </summary>
        /// <returns>Set of all neighborhood listings</returns>
        private static HashSet<NeighborhoodListing> DownloadNeighborhoodListing()
        {
            HashSet<NeighborhoodListing> ret = new HashSet<NeighborhoodListing>();
            using (FileStream fs = File.OpenRead(NEIGHBORHOOD_LISTINGS_PATH))
            {
                Workbook wb = Workbook.Load(fs);
                wb.SetCurrentWorksheet("Neighborhood Associations");
                int first = 1;
                int last = wb.CurrentWorksheet.GetLastDataRowNumber();

                for (int r = first; r <= last; r++)
                {
                    NeighborhoodListing l = new NeighborhoodListing();

                    string type = wb.CurrentWorksheet.GetCell(new Address(1, r)).Value.ToString();
                    l.Type = string.Equals(type, "neighborhood", System.StringComparison.InvariantCultureIgnoreCase) ? NeighborhoodType.Neighborhood : NeighborhoodType.HOA;
                    l.Name = wb.CurrentWorksheet.GetCell(new Address(2, r)).Value.ToString();
                    l.Name = Utilities.SanitizeNeighborhoodName(l.Name);

                    l.PocFirstName = wb.CurrentWorksheet.GetCell(new Address(3, r)).Value.ToString();
                    l.PocLastName = wb.CurrentWorksheet.GetCell(new Address(4, r)).Value.ToString();
                    l.MailingAddress = wb.CurrentWorksheet.GetCell(new Address(5, r)).Value + ", " +
                                       wb.CurrentWorksheet.GetCell(new Address(6, r)).Value + ", " +
                                       wb.CurrentWorksheet.GetCell(new Address(7, r)).Value + " " +
                                       wb.CurrentWorksheet.GetCell(new Address(8, r)).Value;

                    l.Phone = string.Concat(wb.CurrentWorksheet.GetCell(new Address(9, r)).Value.ToString().Where(char.IsNumber));
                    if (l.Phone.Length == 7)
                    {
                        l.Phone = "210" + l.Phone;
                    }
                    else if (l.Phone.Length == 0)
                    {
                        l.Phone = null;
                    }

                    l.Email = wb.CurrentWorksheet.GetCell(new Address(10, r)).Value.ToString();
                    if (!l.Email.Contains('@'))
                    {
                        l.Email = null;
                    }

                    string district = wb.CurrentWorksheet.GetCell(new Address(11, r)).Value.ToString();
                    if (int.TryParse(district, out int parsed) && parsed > 0 && parsed < 11)
                    {
                        l.District = parsed;
                    }

                    ret.Add(l);
                }
            }

            return ret;
        }
    }
}
