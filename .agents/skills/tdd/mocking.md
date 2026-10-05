# When to Mock

Mock at **system boundaries** only:

- External APIs (payment, email, etc.)
- Time/randomness
- File system (sometimes)

For relational persistence, use real PostgreSQL in Testcontainers, not a mock.

Don't mock:

- Your own classes/modules
- Internal collaborators
- Anything you control

For capability tests, enter through the public registration API and resolve only public contract interfaces. Read `docs/agents/architecture.md` before changing that seam. For Blazor UI behavior, prefer an Aspire integration test or C# Playwright browser test over mocking a component's own services.

## Designing for Mockability

At system boundaries, design interfaces that are easy to mock:

**1. Use dependency injection**

Pass external dependencies in rather than creating them internally:

```csharp
// Easy to replace at a system boundary.
internal sealed class PaymentProcessor(IPaymentGateway paymentGateway)
{
    public Task<PaymentResult> ProcessAsync(Order order, CancellationToken cancellationToken) =>
        paymentGateway.ChargeAsync(order.Total, cancellationToken);
}

// Hard to replace and binds domain logic to a concrete external adapter.
internal sealed class PaymentProcessor
{
    public Task<PaymentResult> ProcessAsync(Order order, CancellationToken cancellationToken)
    {
        StripePaymentGateway gateway = new();
        return gateway.ChargeAsync(order.Total, cancellationToken);
    }
}
```

**2. Prefer specific typed clients over generic request wrappers**

Define one operation for each external capability instead of exposing a generic request method with conditional test setup:

```csharp
// GOOD: Each member has a precise contract and return type.
public interface ICharacterCatalogClient
{
    Task<CharacterDto?> GetCharacterAsync(Guid characterId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CharacterDto>> GetCharactersAsync(CancellationToken cancellationToken);
    Task<CharacterDto> CreateCharacterAsync(CreateCharacterRequest request, CancellationToken cancellationToken);
}

// BAD: Tests must branch on strings and anonymous payloads.
public interface IApiClient
{
    Task<TResponse> SendAsync<TResponse>(string path, object? body, CancellationToken cancellationToken);
}
```

The typed-client approach means:
- Each mock returns one specific shape
- No conditional logic in test setup
- Easier to see which endpoints a test exercises
- C# type safety per endpoint
