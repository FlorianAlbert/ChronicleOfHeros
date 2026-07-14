# When to Mock

Mock at **system boundaries** only:

- External APIs (payment, email, etc.)
- Databases (sometimes - prefer test DB)
- Time/randomness
- File system (sometimes)

Don't mock:

- Your own classes/modules
- Internal collaborators
- Anything you control

## Designing for Mockability

At system boundaries, design interfaces that are easy to mock:

**1. Use dependency injection**

Pass external dependencies in rather than creating them internally:

```csharp
// Easy to mock
public class PaymentProcessor
{
    private readonly IPaymentClient _paymentClient;
    
    public PaymentProcessor(IPaymentClient paymentClient)
    {
        _paymentClient = paymentClient;
    }
    
    public Task<PaymentResult> ProcessPaymentAsync(Order order)
    {
        return _paymentClient.ChargeAsync(order.Total);
    }
}

// Hard to mock
public class PaymentProcessor
{
    public Task<PaymentResult> ProcessPaymentAsync(Order order)
    {
        var client = new StripeClient(Environment.GetEnvironmentVariable("STRIPE_KEY"));
        return client.ChargeAsync(order.Total);
    }
}
```

**2. Prefer SDK-style interfaces over generic fetchers**

Create specific methods for each external operation instead of one generic method with conditional logic:

```csharp
// GOOD: Each method is independently mockable
public interface IApiClient
{
    Task<User> GetUserAsync(int id);
    Task<IEnumerable<Order>> GetOrdersAsync(int userId);
    Task<Order> CreateOrderAsync(OrderData data);
}

// BAD: Mocking requires conditional logic inside the mock
public interface IApiClient
{
    Task<HttpResponseMessage> FetchAsync(string endpoint, HttpMethod method, object? body = null);
}
```

The SDK approach means:
- Each mock returns one specific shape
- No conditional logic in test setup
- Easier to see which endpoints a test exercises
- Strong typing per endpoint
