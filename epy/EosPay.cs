using EosSharp;
using EosSharp.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using LiteDB;

namespace epy
{
    public class EosPay
    {
        public TimeSpan ExpireTime = TimeSpan.MaxValue; 

        public Action<PaymentRequest> onPaymentExpire;
        public Action<PaymentRequest> onPaymentVisible;
        public Action<PaymentRequest> onPaymentComplete;

        private string endpoint;
        private string chain;
        private string accountName;

        private Eos eos;
        private Thread sentinel;

        private LiteDatabase db;
        private LiteCollection<PaymentRequest> dbPendingPayments;
        private LiteCollection<PaymentRequest> dbVisiblePayments;

        /// <summary>
        /// Requested(Created) but not visible in chain.
        /// </summary>
        private Dictionary<string, PaymentRequest> pendingPayments;
        /// <summary>
        /// A list of requests which is waiting for LastIrriversibleBlock.
        /// </summary>
        private SortedList<uint, PaymentRequest> visiblePayments;

        public EosPay(string accountName) :
            this("https://proxy.eosnode.tools",
                "e70aaab8997e1dfce58fbfac80cbbb8fecec7b99cf982a9444273cbc64c41473",
                accountName)
        {
        }
        public EosPay(string endpoint, string chain, string accountName)
        {
            this.endpoint = endpoint;
            this.chain = chain;
            this.accountName = accountName;
            this.pendingPayments = new Dictionary<string, PaymentRequest>();
            this.visiblePayments = new SortedList<uint, PaymentRequest>();
        }

        public void Run()
        {
            InitializeDB();
            ExpireOldRequests();
            SyncWithDB();

            eos = new Eos(new EosConfigurator()
            {
                HttpEndpoint = endpoint, //Mainnet
                ChainId = chain,
                ExpireSeconds = 60
            });

            sentinel = new Thread(() => { Sentinel(); });
            sentinel.Start();
        }
        private void InitializeDB()
        {
            db = new LiteDatabase("epy_payments.db");
            dbPendingPayments = db.GetCollection<PaymentRequest>("pending_payments");
            dbVisiblePayments = db.GetCollection<PaymentRequest>("visible_payments");

            db.Engine.EnsureIndex("pending_payments", "memo", true);
            db.Engine.EnsureIndex("visible_payments", "memo", true);
        }
        private void ExpireOldRequests()
        {
            dbPendingPayments.Delete(x =>
                (DateTime.Now - x.createdAt) >= ExpireTime);
        }
        private void SyncWithDB()
        {
            var allRequests = dbPendingPayments.FindAll();
            foreach (var req in allRequests)
                pendingPayments[req.memo] = req;

            var allVisibleRequests = dbVisiblePayments.FindAll();
            foreach (var req in allVisibleRequests)
                visiblePayments.Add(req.visibleAt, req);
        }

        public PaymentRequest CreatePaymentRequest(string token, float value)
        {
            var memo = Guid.NewGuid().ToString();
            var req = new PaymentRequest()
            {
                _id = memo,
                token = token,
                value = value,
                memo = memo,
                createdAt = DateTime.Now
            };
            pendingPayments.Add(memo, req);
            dbPendingPayments.Insert(req);

            return req;
        }
        public bool RemovePaymentRequest(PaymentRequest p)
        {
            return RemovePaymentRequestWithMemo(p.memo);
        }
        public bool RemovePaymentRequestWithMemo(string memo)
        {
            if (pendingPayments.ContainsKey(memo) == false)
                return false;

            pendingPayments.Remove(memo);
            dbPendingPayments.Delete(x => x.memo == memo);

            return true;
        }

        public async Task<double> GetAccountBalance()
        {
            var balance = await eos.GetCurrencyBalance("eosio.token", accountName, "EOS");
            // this account is empty!
            if (balance.Count == 0)
                return 0;
            return double.Parse(balance.First().Split(' ')[0]);
        }

        private async void Sentinel()
        {
            while (true)
            {
                try
                {
                    await CheckActions();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                Thread.Sleep(1000);
            }
        }
        private async Task CheckActions()
        {
            var info = await eos.GetInfo();
            var actions = await eos.GetActions(accountName, -1, -10);

            foreach (var action in actions.Actions)
            {
                var jobj = action.ActionTrace.Act.Data as JObject;

                if (IsTransferAction(jobj))
                {
                    var from = jobj.Value<string>("from");
                    var to = jobj.Value<string>("to");
                    var memo = jobj.Value<string>("memo");
                    var quantity = jobj.Value<string>("quantity");

                    if (pendingPayments.ContainsKey(memo) == false)
                        continue;
                    if (to != accountName)
                        continue;

                    var req = pendingPayments[memo];

                    if (CompareQuantity(quantity, req.token, req.value))
                    {
                        onPaymentVisible?.Invoke(req);
                        pendingPayments.Remove(memo);

                        req.visibleAt = action.BlockNum.Value;
                        dbPendingPayments.Delete(x => x.memo == memo);
                        dbVisiblePayments.Insert(req);
                    }
                }
            }

            foreach (var p in visiblePayments)
            {
                if (p.Key >= info.LastIrreversibleBlockNum)
                    onPaymentComplete?.Invoke(p.Value);
                else
                    break;
            }
            while (visiblePayments.Count > 0)
            {
                var p = visiblePayments.First();
                if (p.Key >= info.LastIrreversibleBlockNum)
                    visiblePayments.RemoveAt(0);
            }
        }

        private bool IsTransferAction(JObject jobj)
        {
            if (jobj == null) return false;

            if (jobj.ContainsKey("from") &&
                jobj.ContainsKey("to") &&
                jobj.ContainsKey("memo") &&
                jobj.ContainsKey("quantity"))
            {
                return true;
            }
            return false;
        }
        private bool CompareQuantity(string quantityString, string token, float value)
        {
            var q = quantityString.Split(' ');
            if (q.Length == 2)
            {
                float valueB;
                if (float.TryParse(q[0], out valueB) == false)
                    return false;
                if (q[1] != token)
                    return false;

                if (valueB >= value)
                    return true;
                else
                    return false;
            }
            else
                return false;
        }
    }
}
