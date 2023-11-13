using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvetloeTvMedia.Domain.Models
{
    public class Seller : BaseModel
    {
        public string MarketplaceId { get; set; }
        public double Rating { get; set; }
        public int NumberProductsSold { get; set; }
        public int NumberProductsActive { get; set; }
        public LegalEntity LegalEntity { get; set; }
    }
}
