using System.Diagnostics;

namespace NeighborhoodPermitParser
{
    [DebuggerDisplay("{Name}")]
    public class NeighborhoodListing
    {
        public NeighborhoodType Type { get; set; }

        public string Name { get; set; }

        public string PocFirstName { get; set; }

        public string PocLastName { get; set; }

        public string MailingAddress { get; set; }

        public string Phone { get; set; }

        public string Email { get; set; }

        public int? District { get; set; }

        public override bool Equals(object obj)
        {
            return obj is NeighborhoodListing other && other.Name == Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }

    public enum NeighborhoodType
    {
        Neighborhood,
        HOA
    }
}
