# Good and Bad Tests

## Good Tests

**Integration-style**: Test through real interfaces, not mocks of internal parts.

```csharp
// GOOD: Tests observable behavior
[Fact]
public async Task UserCanCheckoutWithValidCart()
{
    var cart = CreateCart();
    cart.Add(product);
    var result = await CheckoutAsync(cart, paymentMethod);
    Assert.Equal("confirmed", result.Status);
}
```

Characteristics:

- Tests behavior users/callers care about
- Uses public API only
- Survives internal refactors
- Describes WHAT, not HOW
- One logical assertion per test

## Bad Tests

**Implementation-detail tests**: Coupled to internal structure.

```csharp
// BAD: Tests implementation details
[Fact]
public async Task CheckoutCallsPaymentServiceProcess()
{
    var mockPayment = Substitute.For<IPaymentService>();
    await CheckoutAsync(cart, payment);
    await mockPayment.Received(1).ProcessAsync(cart.Total);
}
```

Red flags:

- Mocking internal collaborators
- Testing private methods
- Asserting on call counts/order
- Test breaks when refactoring without behavior change
- Test name describes HOW not WHAT
- Verifying through external means instead of interface

```csharp
// BAD: Bypasses interface to verify
[Fact]
public async Task CreateUserSavesToDatabase()
{
    await CreateUserAsync(new User { Name = "Alice" });
    var row = await dbContext.Users.FirstOrDefaultAsync(u => u.Name == "Alice");
    Assert.NotNull(row);
}

// GOOD: Verifies through interface
[Fact]
public async Task CreateUserMakesUserRetrievable()
{
    var user = await CreateUserAsync(new User { Name = "Alice" });
    var retrieved = await GetUserAsync(user.Id);
    Assert.Equal("Alice", retrieved.Name);
}
```

**Tautological tests**: Expected value restates the implementation, so the test passes by construction.

```csharp
// BAD: Expected value is recomputed the way the code computes it
[Fact]
public void CalculateTotalSumsLineItems_Bad()
{
    var items = new[] { new LineItem { Price = 10 }, new LineItem { Price = 5 } };
    var expected = items.Sum(i => i.Price);
    Assert.Equal(expected, CalculateTotal(items));
}

// GOOD: Expected value is an independent, known literal
[Fact]
public void CalculateTotalSumsLineItems_Good()
{
    var items = new[] { new LineItem { Price = 10 }, new LineItem { Price = 5 } };
    Assert.Equal(15, CalculateTotal(items));
}
```
