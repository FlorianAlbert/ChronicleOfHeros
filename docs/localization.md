# Localization

ChronicleOfHeros localizes all application-owned visible text, page titles, and accessibility labels in the Web and Web.Client projects. English is the canonical translation resource and German is the other supported display language.

## Resource ownership and names

Each page or reusable component owns its resources beside its implementation:

- `Home.razor` owns `Home.resx` and `Home.de-DE.resx`.
- `Error.razor` owns `Error.resx` and `Error.de-DE.resx`.

The neutral `.resx` file is the canonical English resource for the `en-US` display language. Locale files use the concrete supported locale suffix, such as `.de-DE.resx`. Keep keys descriptive of the component's text, such as `ReturnToCharacterSheet`, rather than reusing generic names with unrelated meanings.

`Web.Client/Localization/SharedResources` is intentionally small. It contains only genuinely cross-cutting vocabulary, currently the display-language self-names. Do not move page or component copy into it merely to avoid adding a local resource.

## Adding or changing text

1. Add the key and English value to the owning neutral `.resx` resource.
2. Add the same key and translated value to every locale resource owned by that component.
3. Use `IStringLocalizer<TComponent>` in the page or reusable component. Use `IStringLocalizer<SharedResources>` only for shared vocabulary.
4. Run the translation resource test before submitting the change:

```bash
dotnet test --project src/ChronicleOfHeros.AppHost.Tests/ChronicleOfHeros.AppHost.Tests.csproj --filter-class ChronicleOfHeros.AppHost.Tests.TranslationResourceTests
```

The test discovers every production `.resx` file in `Web` and `Web.Client`, requires a resource for each supported display language, and compares its key set with the canonical English resource. Do not make production resources intentionally incomplete to test fallback behavior; focused tests use test-only resources for that contract.

## Adding a display language

1. Add its concrete locale to `supportedCultures` in `Web/Program.cs`.
2. Add its locale resource beside every production canonical resource, with exactly the same key set.
3. Add the locale to `SupportedDisplayLanguages` in `TranslationResourceTests` so parity remains enforced.
4. Add the display-language self-name to `SharedResources` and expose it through the selector.
5. Add browser and selector coverage for the new language.

Browser preferences match a supported locale exactly or by parent language, so `de`, `de-AT`, and `de-CH` select `de-DE`. An explicit choice takes precedence: the display-language selector writes a first-party, `Secure`, `HttpOnly`, `SameSite=Lax` cookie for 400 days, then redirects to a validated local path. The next full page load applies the choice consistently to server rendering and the interactive UI.

## German copy

Use informal singular `du` consistently. For Dungeons & Dragons concepts, use the official German Dungeons & Dragons 5e term whenever one exists; retain English proper names only when no official German term exists.

## Fallback diagnostics

If a requested-language key is absent, the standard resource localizer presents the canonical English value and the player can continue. If the canonical English key is also absent, the player sees the resource key and `MissingTranslationDiagnosticStringLocalizerFactory` records a warning with that key. Treat either case as a translation defect: restore parity for a requested-language omission, or add the missing English key and all localized values for a canonical omission.

## Application text audit

The reachable application-owned text has been audited against its owning resources:

- `Home` owns the landing-page title, navigation contributions, controls, headings, descriptions, and character-record labels.
- `NavMenu`, `DisplayLanguageSelector`, and `MainLayout` own header navigation, selector, error-bar, and accessibility text.
- `ReconnectModal` owns reconnect status, recovery actions, and accessibility text.
- `NotFound` and `Error` own their titles, status copy, recovery actions, and request identifier label.

The production parity test protects every resource in these Web and Web.Client surfaces. The public-host tests exercise English and German titles, navigation labels, status and recovery messages, and not-found accessibility labels. Browser, framework, Aspire dashboard, and third-party-package text remain outside this scope.

The product name `ChronicleOfHeros`, the example character name `Elian Thorn`, ordinal markers, and Dungeons & Dragons rule units such as `30 ft.` are intentionally invariant rather than translated. Display culture never changes Dungeons & Dragons rules units or calculations.