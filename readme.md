eospay
====

Payment with EOS and EOS based tokens.

```cs
var ep = new EosPay("YOUR_WALLETID");
ep.OnPaymentComplete += OnPaymentComplete;
ep.Run();
```
```cs
void OnUserRequestedPayment() {
    var p = ep.CreatePaymentRequest("EOS", 100);
    var memo = p.memo;

    Console.WriteLine(
        $"Please send me {p.value} {p.token} with a following memo:");
    Console.WriteLine(p.memo);
}
```
```cs
void OnPaymentComplete(PaymentRequest p) {
    Console.WriteLine("Thank you!");
}
```


Payment Flow
----

```cs
ep.OnPaymentVisible += OnPaymentVisible;
ep.OnPaymentComplete += OnPaymentComplete;
```

__OnPaymentVisible__<br>
Transaction has been included and visible.<br>
However, you should wait until the block is irreversible to prevent fraud payments.
```cs
void OnPaymentVisible(PaymentRequest p) {
    Console.WriteLine("Hold On! almost done");
}
```

__OnPaymentComplete__<br>
You have received the token successfully.<br>
You're now safe to give them a item.
```cs
void OnPaymentComplete(PaymentRequest p) {
    Console.WriteLine("Complete");
}
```