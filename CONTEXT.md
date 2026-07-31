# ChronicleOfHeros Domain Glossary

## Display language

The language used for user-facing application text. It is selected in this order: an explicit user choice persisted in the browser, a supported browser language preference, then English.

## Translation fallback

The display-language behavior for a missing localized string: use the English string for the same resource key. If that string is also absent, show the resource key and record a diagnostic without interrupting the application.

## Localizable application text

Every user-visible string, accessibility label, and page title controlled by the Web or Web.Client projects. Browser, framework, Aspire dashboard, and third-party-package text are outside this boundary. Unreachable scaffolded UI is removed rather than retained for translation.

## Display culture

The formatting culture paired with the display language: `de-DE` for German and `en-US` for English. It controls localized formatting but never Dungeons & Dragons rules units or calculations.

## Supported display language

A display language represented by one concrete locale resource and one formatting culture. Browser variants match a supported display language through their parent language. Initial supported display languages are English (`en-US`) and German (`de-DE`).

## Document language

The base language declared on every rendered HTML document for assistive technology and language-aware tooling. It matches the active display language, initially `en` or `de`; document direction remains left-to-right. The concrete locale remains available for formatting.

## Canonical translation resource

The English resource that defines every key for localizable application text. Every supported display language has the same key set before a change is complete. Documentation and automated checks preserve this parity.

## Translation resource ownership

Each page or reusable component owns the translation resources for its localizable application text. A small shared resource contains only genuinely cross-cutting strings, such as display-language names and common actions.

## Display-language preference

An explicit display-language choice stored in a first-party browser cookie. It is applied on the next full page load or redirect so server-rendered and interactive UI use the same display language. It persists for 400 days and is renewed when changed, subject to browser or privacy restrictions. The cookie contains only a supported locale value and uses `Secure`, `HttpOnly`, and `SameSite=Lax`. Without an explicit choice, the application selects the first browser language preference with an exact or parent-language match in the supported display languages; English is used only when no preference matches.

## Display-language selector

The application control used to choose a display language. It appears in the header of every reachable application page and redirects to the current URL after a choice. Its return URL accepts only local application paths and falls back to `/` when absent or invalid. Its options use each language's self-name, initially `English` and `Deutsch`.

## German product voice

German user-facing copy addresses the player consistently with informal singular `du`.

## Dungeons & Dragons terminology

Localized labels for Dungeons & Dragons concepts use the official German Dungeons & Dragons 5e term whenever one exists. English proper names remain only where no official German term exists.