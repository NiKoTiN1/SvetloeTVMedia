using SvetloeTvMedia.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvetloeTvMedia.Domain.Models
{
    public class LegalEntity : BaseModel
    {
        public string Name { get; set; }
        public OrganizationStatus OrganizationStatus { get; set; }
        public int OGRN { get; set; }
        public int INN { get; set; }
        public string Address { get; set; }
        public string SuperviserName { get; set; }
        public IEnumerable<string> PhoneNumbers { get; set; }
        public IEnumerable<string> Emails { get; set; }
    }
}
