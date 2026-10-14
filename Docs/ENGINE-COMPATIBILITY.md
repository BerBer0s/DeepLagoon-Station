# Official client compatibility

The production client engine is the signed official **277.0.0** release at
`08a3d120b7029d03e60b44b23fed2b2659ed3224`. Do not replace its DLLs, loader,
authentication, transport, serialization, sandbox or module trust configuration.
Never relabel a custom client build as an official version.

`ENGINE-COMPATIBILITY.json` pins the Station engine dependency to
`c67977d80c919c1e16b9c7686b1b995142b4443d`. Its entire difference from the official
baseline is a reviewed prototype hot-reload method implementation and its tests.
It changes no public method signature, prototype/network schema, component
registration, serializer or authentication code. It composes missing ancestors
and preserves the previous prototype when a hot-reload parent is unavailable.
The normal client uses the official implementation supplied by its launcher.

Engine changes are authored in the standalone `DeepLagoon/RobustToolbox` fork.
The Station checkout is a pinned consumer, never an engine-source patch owner.
The old `MSBuild/DeepLagoon.Network.targets` has been removed. No build task may
rewrite or substitute engine source files. The submodule has no floating branch
setting and CI does not pull engine dependency updates behind the pin.

## Preserved experiments

The standalone local branch `codex/deeplagoon-client-experimental-277` preserves
the CEF/WebView/texture changes through `bf07fcf5d`, plus commit `0e50b64db` holding
the shader uniform fix/test and the former Station network race patch in native
engine source files. The active standalone branch is
`codex/deeplagoon-server-277` at `c67977d80`. No branch has been published by this
change. Do not advance the production submodule to the experimental branch.
Client changes can become production dependencies only through a compatible,
trusted official release, following an explicit baseline update and validation.

## Build and release checks

Run `python Tools/check_engine_compatibility.py` before building. It checks the
exact gitlink and engine HEAD, clean nested dependencies, the version, every
engine difference and its reviewed blob, and known fork-only content APIs.
The MSBuild target runs this check for client, server and packaging builds.
The publisher scripts use the pinned official version instead of the nearest
Git tag. Packaging repeats validation, including when started with
`dotnet run --no-build`, and embeds `engine-compatibility.json` in client/server
archives. This records the official client version/SHA and separate server SHA.

The packager's `--skip-build` is rejected: preexisting content DLLs have no
verified build provenance. Rebuild without that argument. `dotnet run
--no-build` remains supported and only skips rebuilding the packaging tool;
the packaging tool still rebuilds game content.

CI first validates the approved fork, then uses a separate job checkout at the
exact official engine commit to compile release content and run sandbox/API and
JSON-parser tests. `DeepLagoonStockValidation=true` is only for this exact stock
revision; it cannot generate release metadata. Ordinary release builds must use
the pinned approved source. Do not update either checkout during a build.

The content bridge uses the stock GET/chunk API. JSON actions are parsed by a
strict bounded content parser using sandbox-supported primitives; no custom
WebView JSON or POST API is referenced. Stock modules close/recreate hidden
browsers. Content shutdown detaches registered browsers before engine CEF
shutdown, avoiding a second close during later entity/UI teardown.

## Release acceptance

After the automated checks, use a clean official launcher against a test slot:
login, lobby/TGUI, spawn, movement, inventory, network state updates, reconnect
and official authentication. Check hub visibility and the exact advertised
`engine_version` in `/info` and `build.json`. A local compile or sandbox check
does not establish those live outcomes. Keep existing production state until
the isolated slot passes these checks.

The official CEF module itself still has `NoSandbox=true`. Origin allowlisting
does not isolate a compromised remote wiki. Prefer local sanitized guide data
or the system browser for remote pages; fixing CEF isolation belongs upstream
and must not introduce a custom-client requirement into production.
