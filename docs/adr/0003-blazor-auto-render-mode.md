# Blazor Web App with server rendering

The frontend renders statically by default. Components opt into `@rendermode InteractiveServer` only when they need Blazor interactivity. This avoids creating a SignalR circuit for pages that only need server-side rendering and native browser behavior. `ChronicleOfHeros.Web` contains the frontend, its resources, and the interactive server renderer.

The separate WebAssembly client and its interactive render modes are not needed at the current stage. They can be reconsidered if a feature requires browser-executed .NET. The Web-to-API call path is a separate decision documented in ADR 0004.
