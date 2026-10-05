# Good and Bad Tests

## Good Tests

**Integration-style**: Test through real interfaces, not mocks of internal parts.

Examples are illustrative; reuse the existing test fixtures and public contracts for the capability under test.

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
// BAD: Bypasses the public contract to query persistence directly.
[Fact]
public async Task Save_character_creates_a_database_row()
{
    await characterService.SaveAsync(character, TestContext.Current.CancellationToken);

    Assert.NotNull(await dbContext.Characters.FindAsync(character.Id));
}

// GOOD: Exercises both the write and read through public contracts.
[Fact]
public async Task Saved_character_is_retrievable()
{
    await characterService.SaveAsync(character, TestContext.Current.CancellationToken);

    CharacterDto? retrieved = await characterService.GetAsync(
        character.Id,
        TestContext.Current.CancellationToken);

    Assert.NotNull(retrieved);
    Assert.Equal(character.Name, retrieved.Name);
}
```

**Tautological tests**: Expected value restates the implementation, so the test passes by construction.

```csharp
// BAD: Expected value repeats the implementation's formula.
[Fact]
public void Ability_modifier_matches_its_formula()
{
    const int score = 14;

    Assert.Equal((int)Math.Floor((score - 10) / 2.0), CalculateAbilityModifier(score));
}

// GOOD: Expected value is independently known from the official rules.
[Fact]
public void Ability_score_of_fourteen_has_a_plus_two_modifier()
{
    Assert.Equal(2, CalculateAbilityModifier(14));
}
```
