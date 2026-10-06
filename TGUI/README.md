# DeepLagoon TGUI

This port uses the existing `Robust.Client.WebView` engine module. No RobustToolbox
source changes are required. Local packaged UI and remote wiki documents have
separate request policies and separate controls. A wiki page never receives the
TGUI bridge.

## Create an interface

Quick start: `pnpm new:interface MyInterface` generates a TSX interface, shared
component, server system with validated counter actions, and a working console
prototype (`ComputerMyInterface`). It checks all destinations and refuses to
overwrite existing files. Replace the example state/actions with the feature's
own logic. The transport, request policy and native window are shared.

1. Add `packages/tgui/interfaces/MyInterface.tsx`. Export a component named
   `MyInterface`; see `DeepLagoonDemo.tsx` for `useBackend`, `data`, and `act`.
2. In this directory, run `pnpm install --frozen-lockfile` and `pnpm build`.
   Commit the changed source, lockfile, and bundles in `Resources/Web/DeepLagoon`.
   Clients do not need Node.js. The bundles are distributed as game resources.
3. Register `WebUiBoundUserInterface` under a UI key on the entity's
   `UserInterface` component, and open it through the normal `UserInterfaceSystem`.
4. Publish `new WebUiState("MyInterface", jsonObject)` with `SetUiState`.
   Subscribe to `WebUiActionMessage` on the relevant server component and validate
   each supported action and its arguments. Never trust values supplied by JS.

The example entity is `ComputerTguiDemo`. Spawn it in a development session and
interact with it. Increment/reset are validated and applied by `WebUiDemoSystem`;
its state is shared by viewers through the normal bound UI transport.

Interfaces are automatically discovered by name; TSX/JSX files need no central
registry or separate client window class. For client-owned screens, instantiate
`TguiPanel`, call `SetState("MyInterface", jsonObject, title)`, and subscribe to
`OnAction`. `TguiActionData.TryParse` provides sandbox-compatible scalar argument
parsing; the consumer still validates field ranges and permissions. Use distinct
UI keys when one entity has multiple independent bound interfaces.

## Dev Server and hot reload

From `TGUI` with Node.js 20.19+ or 22.12+ and pnpm installed:

```text
pnpm install --frozen-lockfile
pnpm dev
```

Start the Debug client with `--cvar tgui.dev_server=http://127.0.0.1:5173`, or run
`cvar tgui.dev_server http://127.0.0.1:5173` in its console before opening a TGUI
window. Then open `tgui_preview MyInterface` or the actual entity-bound UI.
Chat uses the same development server. TSX/JSX and SCSS changes update the already
open window through Vite HMR/React Fast Refresh. Compatible component edits retain
React local state and the current backend data. Refresh-boundary/export changes
may reload the document; the native window remains open and the host replays its
latest state after `ready`.

Use port 5173 for the supplied configuration/CSP. The server listens only on
127.0.0.1. The opt-in CVar is client-only, not saved, and ignored in Release.
Only a validated loopback HTTP origin is admitted; wiki access remains confined
to the guidebook. Stop the server and restart without the CVar to use packaged
bundles. Production builds never require a development server.

Action payloads are limited to 8 KiB, with a frontend budget of 7900 characters.
Oversized BYOND messages, shell commands, BYOND hotkeys, external links and
embedded remote pages are not implemented. Native windows own resizing and
closing. New interfaces can use TGUI components, layouts and local state hooks.
BYOND-specific features such as `ByondUi` require an SS14-specific replacement.

`tgui_preview DeepLagoonDemo`, `tgui_preview chat`, and `tgui_preview wiki` open
developer previews from the client console. Preview actions do not change server
state. Use `ComputerTguiDemo` to test actual client/server actions.

## BlueMoon chat

The separated chat layout uses BlueMoon's renderer, tabs, search, highlights and
appearance settings. Enable the separated chat layout in game settings. SS14's
native input, channel selection, typing indicators and server permissions remain
connected through the existing `ChatUIController`.

Only messages received by the client enter the web panel. SS14 markup is converted
to escaped HTML with supported rich-text styles (bold, italic, color and relative
font size). Channel metadata selects BlueMoon message classes; text prefixes never
determine the channel. Explicit server colors, including departmental radio colors,
are preserved. The renderer uses HTML for display and plain text for searching,
and never combines different message types or formatting just because text matches.
The light/dark message styles remain identical to the BlueMoon source files.
Message history is never restored from
browser storage; server deletions rebuild the panel from current client history.
Settings are stored in the existing chat preference record, in the YAML
`WebState` field, alongside native chat settings. Pinned emote IDs use the
`PinnedEmotes` field in the same record. Click the star above chat to search and
pin/unpin emotes (up to 24); pinned buttons execute the regular predictive SS14
event and follow species, whitelist and availability restrictions. The visible
12-pixel grip between viewport and chat adjusts its width (15–55%); the proportion
is saved in `ui.chat_panel_width`. See [EMOTES.md](EMOTES.md) for the 50 ported
descriptive emotes and the comparison scope.

BYOND's audio player, map controls and arbitrary external chat images
are not included in this port. TGUI fonts and icons are packaged locally.

## Wiki guidebook

Edit `Resources/Prototypes/_DeepLagoon/WebUI/wiki.yml`. Each `wikiPage` has an ID,
localized name, explicit `/ru/...` path, optional `default: true`, and the old
`guideEntries` IDs it replaces. Initially the only allowed page is `/ru/rules`.
Unmapped contextual help displays a message instead of loading an unrelated page.

The guidebook button, keybinding, help API and new-player automatic opening use
the wiki window. Add more pages and mappings as the wiki grows. Only HTTPS on
`wiki.deep-lagoon-ss14.ru`, configured paths without query strings, and local
wiki assets are allowed. Other pages, origins, downloads, login/API requests and
external resources are blocked. In-page anchors are permitted.

Server code can show a configured page to one player:

```csharp
EntityManager.System<Content.Server._DeepLagoon.WebUI.WikiPageSystem>()
    .OpenPage(session, "Rules");
```

This sends a page ID, not a URL, and always opens the guidebook window. Local
TGUI windows and the chat reject every wiki/network request.

## Build and validation

Build the normal client and server projects with .NET 10. CEF's subprocess,
native libraries and resources are required, not just the managed WebView DLL.
The client project references the existing WebView module and copies the
subprocess apphost for local builds. The content manifest declares the module;
the launcher's existing module downloader installs its matching engine release.
Confirm that the chosen engine release publishes that module before deployment.

```text
dotnet build Content.Client/Content.Client.csproj -c Debug
dotnet build Content.Server/Content.Server.csproj -c Debug
dotnet test Content.Tests/Content.Tests.csproj --filter "FullyQualifiedName~ClientSandboxTest|FullyQualifiedName~ChatTabsSettingsTest"
```

Integration checks:

```text
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName~SeparatedChatTest"
```

Validation on 2026-10-06 includes actual C# chat payloads rendered with BlueMoon
light/dark styles and native in-game CEF Fast Refresh: the document stayed open
and its backend data survived the TSX edit. The user reports that the previously
added game/wikiguidebook integration works. Builds/headless checks alone do not
verify account-backed chat settings or every graphical character/editor path.
No engine source changes were made.

## Character editor and emote audio

The character editor uses one `CharacterEditor.jsx` browser for appearance,
jobs, traits and companies. Identity/save/import/export controls stay native
on the left to avoid a second CEF renderer. The original left-side sprite/equipment
preview, tab order and specialized markings/loadout/saved-item/flavor editors
remain native. Web actions modify the same `HumanoidCharacterProfile` draft;
Save/import/export use the existing controller callbacks. Species restrictions,
job requirements and trait budgets are checked by the content code. Hair and
beard expose RGB sliders, HEX input, presets and an immediate color swatch.

In a connected lobby, `tgui_preview character` opens the actual editor with a local test draft;
it does not save an account profile. TGUI Dev Server/Fast Refresh works through
the same `TguiPanel` used by other interfaces.

The web panel has a permanent parent above the native tab content; tab changes
update its data instead of closing/recreating CEF. Its viewport does not contribute
tab-specific minimum widths to the lobby layout. Hidden editors release their
browser, and reopening/reloading replays the latest cached draft on `ready`.
Identical states are not resent; chat history replacement uses `chat/replace`
inside the existing document instead of reloading the lobby chat page.
Both the lobby and the separated in-round screen use the same `ChatBox`
TGUI integration, native input and saved web settings. Hidden chat views release
CEF and restore settings/history on reopening. When TGUI is enabled, incoming
messages retain native audio/read handling without building a duplicate hidden
rich-text history. Performance validation must measure an actual round;
editor/menu lifecycle checks do not establish in-round FPS.
Server/character details scroll on shorter lobby windows so the chat viewport
retains a usable minimum height.
In Debug, `tgui_preview character-wait` waits for a local connected lobby, and
the `dev-select-tab` action is available to lifecycle tests. Native CEF checks
cover 12 tab switches and three page reloads with retained hair color.

Emote audio files and sex-dependent/random bindings are copied from BlueMoon;
see [EMOTE-SOUNDS.md](EMOTE-SOUNDS.md). Regenerate with
`node tools/port-emote-sounds.cjs <BlueMoon repository path>` (also invoked by
the emote importer). `SOURCE.txt` records hashes; upstream voice attribution
is preserved beside the imported files. The picker supports a responsive grid,
an explicit Close button, Escape, auto-close after performing an emote and
stars for pinning without closing the list.

## Upstream source

`packages/common`, `packages/tgui`, `packages/tgui-panel`, and
`packages/tgui-dev-server` were copied from the user's MOLOT-BlueMoon-Station
checkout at `a3c313de9c32aa8ed651a8009dbb389e9a15f438`. Unrelated game interfaces and upstream tests were excluded. Original
copyright/license headers are preserved; see `UPSTREAM-LICENSE` for the source
repository license. Local modifications adapt the transport, settings and chat
history. Font Awesome includes its original headers and README in game resources.
