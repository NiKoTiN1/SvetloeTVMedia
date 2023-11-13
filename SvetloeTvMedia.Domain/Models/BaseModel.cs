using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvetloeTvMedia.Domain.Models
{
    public abstract class BaseModel
    {
        public Guid Id { get; set; }
    }
}
