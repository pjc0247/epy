using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace epy.testbed
{
    class Program
    {
        static void Main(string[] args)
        {
            var e = new EosPay("eosusrwallet");
            e.Run();

            var r = e.CreatePaymentRequest("EOS", 100);
            Console.WriteLine(r.memo);

            Console.WriteLine(e.GetAccountBalance().Result);
            Console.Read();
        }
    }
}
