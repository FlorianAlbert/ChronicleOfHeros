# Good and Bad Tests

## Good Tests

**Integration-style**: Test through real interfaces, not mocks of internal parts.

```csharp
using Microsoft.Playwright;

// GOOD: Tests browser-visible behavior.
[Fact]
public async Task Display_language_selector_changes_the_rendered_language()
{
    await page.GotoAsync(baseAddress.AbsoluteUri);

    await page.GetByLabel("Display language").SelectOptionAsync("de-DE");

    await Assertions.Expect(page)
        .ToHaveTitleAsync("ChronicleOfHeros | Dein Charakterbogen am Spieltisch");
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
// BAD: Tests an internal collaboration instead of the rendered result.
[Fact]
public async Task Display_language_selector_calls_the_language_preference_service()
{
    await selector.SetLanguageAsync("de-DE");

    languagePreferenceServiceMock.Verify(
        service => service.SetAsync("de-DE"),
        Times.Once);
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
// BAD: Bypasses the endpoint to query persistence directly.
[Fact]
public async Task Save_character_creates_a_database_row()
{
    await characterService.SaveAsync(character, TestContext.Current.CancellationToken);

    Assert.NotNull(await dbContext.Characters.FindAsync(character.Id));
}

// GOOD: Verifies the behavior through the application's public HTTP interface.
[Fact]
public async Task Saved_character_is_retrievable_from_its_endpoint()
{
    using var response = await webClient.GetAsync(
        $"/api/characters/{character.Id}",
        TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

**Tautological tests**: Expected value restates the implementation, so the test passes by construction.

```csharp
// BAD: Expected value is recomputed the way the code computes it.
[Fact]
public void Armor_class_sums_its_known_modifiers()
{
    var modifiers = new[] { 10, 2, 1 };

    Assert.Equal(modifiers.Sum(), CalculateArmorClass(modifiers));
}

// GOOD: Expected value is an independent, known literal from the rule.
[Fact]
public void Armor_class_includes_base_dexterity_and_shield()
{
    Assert.Equal(13, CalculateArmorClass([10, 2, 1]));
}
```