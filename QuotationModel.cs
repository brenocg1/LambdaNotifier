using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LambdaNotifier
{
    public class QuotationModel
    {
        public DateTime timestamp { get; set; } = DateTime.Now;
        public Decimal quote_value { get; set; }
        public string quotation_id { get; set; } = Guid.NewGuid().ToString();
    }
}
