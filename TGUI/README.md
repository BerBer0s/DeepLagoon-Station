# DeepLagoon TGUI

This port uses the existing `Robust.Client.WebView` engine module. No RobustToolbox
source changes are required. Local packaged UI and remote wiki documents have
separate request policies and separate controls. A wiki page never receives the
TGUI bridge.

Window resizing and automatic UI scaling keep the same browser document alive.
`MSBuild/DeepLagoon.WebView.targets`, imported by the root `Directory.Build.targets`,
adapts the WebView build to notify CEF of scale changes and reuse unchanged-size
textures. It reads two engine sources, checks that each expected source fragment
occurs exactly once, and compiles adapted copies in the WebView project's `obj`
directory instead of the original files. RobustToolbox sources and its pinned
commit stay unchanged. An incompatible engine update fails the build with an
adapter diagnostic; review the adapter before updating its source fragments.
Normal client builds automatically apply it; no separate command is needed.

`GameWebView` dispatches browser events through the UI manager's deferred-action
queue after frame traversal. Action and ready handlers can open or close windows
without modifying the collection currently being enumerated. Pending messages
are discarded when their browser is removed or disposed.

Use in-document TGUI `Dropdown` controls and RGB/HEX color controls. Native
HTML `<select>` and `<input type="color">` open CEF popup surfaces which this
WebView implementation does not composite separately; they can corrupt the
main view's paint buffer. Do not use native browser popup widgets in interfaces.

`TguiPanel` automatically moves its enclosing native window when that window is
directly attached to `WindowRoot`. Hold the left mouse button anywhere in the
browser and move it at least four UI pixels to drag; a stationary click works
normally. Dragging does not resize or reload CEF, and the release click is
suppressed. Embedded panels (chat, lobby and character editor) stay anchored.
No per-interface drag header, JavaScript action or registration is needed.

## Create an interface

Quick start: `pnpm new:interface MyInterface` generates a TSX interface, shared
component, server system with validated counter actions, and a working console
prototype (`ComputerMyInterface`). It checks all destinations and refuses to
overwrite existing files. Replace the example state/actions with the feature's
own logic. The transport, request policy and native window are shared.

1. Add `packages/tgui/interfaces/MyInterface.tsx`. Export a component named
   `MyInterface`; see `DeepLagoonDemo.tsx` for `useBackend`, `data`, and `act`.
2. Build any DeepLagoon content project or the solution; TGUI is rebuilt automatically.
   Commit the changed source and lockfile. Generated bundles in
   `Resources/Web/DeepLagoon` are ignored by Git.
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

## Automatic builds

Every normal build of a DeepLagoon content project (including server, client,
packaging and tools) rebuilds the interface and chat production bundles. The
shared `DeepLagoon.TGUI.csproj` dependency prevents concurrent bundle writes
within a parallel solution build. Debug, DebugOpt, Release, Rebuild and Publish
use the same production bundles. IDE design-time checks do not run pnpm.

Install Node.js 24 and pnpm 11.25.0 on build machines. JavaScript dependencies
are installed with `pnpm install --frozen-lockfile` on the first build and when
`package.json` or `pnpm-lock.yaml` changes. Any install or bundle error fails the
normal build. To use a pnpm executable outside PATH, pass
`-p:TguiPackageManager="/path/to/pnpm"` (quote the executable path inside the
property value if it contains spaces). `pnpm build` remains available for a
standalone web build. `--no-build` commands reuse existing bundles.

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

## TGUI chat

Settings → Misc → Classic chat (Классический чат) restores the original native `OutputPanel`
and horizontal input in lobby and round, with no chat CEF browser. During rounds
it selects the original floating chat HUD; disabling the option restores the
previous HUD layout. The archived client-only setting is `ui.vanilla_chat`.
The shared message history and input draft survive switching. Character TGUI
and wiki windows are independent of this chat setting.

The separated chat layout uses TGUI's renderer, tabs, search, highlights and
appearance settings. Enable the separated chat layout in game settings. SS14's
TGUI mode hides the native bottom input. Chat focus keybindings and the pen button
open the `ChatComposer` interface; Enter submits, Shift+Enter inserts a newline,
and Escape closes while retaining the draft. The mounted form confirms focus
to the host, which focuses the native browser and starts platform text input.
While that browser owns keyboard focus, Return is bridged to a DOM keydown;
this avoids the engine CEF adapter's extra Backspace char event. IME confirmation
is passed through normally, and the keyboard hook is removed on close.
Emotes expand the form from 440px
to 840px and the input from 66px to 250px over 180ms, inside a fixed browser
surface. The browser is disposed when the composer closes. Draft updates are
debounced; state replay supports dev-server reload without losing the draft.
Channel selection, typing indicators and server permissions remain connected
through the existing `ChatUIController`.

Only messages received by the client enter the web panel. SS14 markup is converted
to escaped HTML with supported rich-text styles (bold, italic, color and relative
font size). Channel metadata selects TGUI message classes; text prefixes never
determine the channel. Explicit server colors, including departmental radio colors,
are preserved. The renderer uses HTML for display and plain text for searching,
and never combines different message types or formatting just because text matches.
The light/dark message styles remain identical to the TGUI source files.
Message history is never restored from
browser storage; server deletions rebuild the panel from current client history.
Settings are stored in the existing chat preference record, in the YAML
`WebState` field, alongside native chat settings. Pinned emote IDs use the
`PinnedEmotes` field in the same record. Click the star above chat to search and
pin/unpin emotes (up to 24); pinned buttons execute the regular predictive SS14
event and follow species, whitelist and availability restrictions. The visible
12-pixel grip between viewport and chat adjusts its width (15–55%); the proportion
is saved in `ui.chat_panel_width`. The imported descriptive emotes are defined in
`Resources/Prototypes/_DeepLagoon/Voice/extended_emotes.yml`.

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

## Build

Build the normal client and server projects with .NET 10. CEF's subprocess,
native libraries and resources are required, not just the managed WebView DLL.
The client project references the existing WebView module and copies the
subprocess apphost for local builds. The content manifest declares the module;
the launcher's existing module downloader installs its matching engine release.
Confirm that the chosen engine release publishes that module before deployment.

```text
dotnet build Content.Client/Content.Client.csproj -c Debug
dotnet build Content.Server/Content.Server.csproj -c Debug
```

## Character editor and emote audio

The character editor uses one `CharacterEditor.jsx` browser for every tab,
identity, profile selection and save/import/export controls. Only the transparent
character sprite is drawn by the engine above the TGUI background. The editor
inherits the player's chat theme and background animation. Collapsible hair and
beard galleries show sprite previews; color squares open the color wheel.
Equipment customization uses modal dialogs and the existing per-profile data,
server validation and instance visuals. Paint dialogs also expose RGB and HSV.
Web actions modify the same `HumanoidCharacterProfile` draft; saving uses the
existing controller callbacks, species restrictions, prices and trait budgets.

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
Emote audio files and sex-dependent/random bindings are copied from TGUI;
regenerate with
`node tools/port-emote-sounds.cjs <source repository path>` (also invoked by
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
