using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace epy
{
    public class PaymentRequest
    {
        [BsonId]
        public string memo { get; set; }
        public float value { get; set; }
        public string token { get; set; }

        public DateTime createdAt { get; set; }
        public uint visibleAt { get; set; }

        internal PaymentRequest()
        {
        }
    }
}
