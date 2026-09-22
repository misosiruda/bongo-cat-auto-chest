# Validation

Release v1.0.0 was checked on Windows with Windows PowerShell 5.1 and the built-in .NET Framework C# compiler.

- Steam install discovery found Bongo Cat app 3419430.
- Current game compatibility: Steam build 25400987, Unity 6000.2.8f1, Mono x64.
- 9 dispatch gate checks passed (including repeated ready-state observations and independent chest cycles).
- PowerShell scripts parsed successfully.
- All 170 helper runtime member references resolved against the installed game's stripped managed assemblies.
- All 3,339 original method bodies were preserved after accounting for the two inserted prefixes.
- The earlier local patch and backup were recognized without changing the running game.
- Integration checks used disposable local copies of game assemblies: check without mutation, verified original backup, idempotent installation, byte-exact restore, simulated new version, separate backup per version, preserved custom settings, refusal of unexpected method changes, refusal of corrupt backup, and refusal of incompatible private fields.

The simulated update inserts an inert instruction into a copied game assembly. It validates installer behavior when game content changes; it cannot predict compatibility with future game behavior.

The underlying auto-opener was observed receiving own normal/emote items and sending lobby gift requests on the tested build. Recipient delivery is not verified by a local `SENT` log. The toggle hotkey and operation on a second physical PC have not been manually verified.

CI builds tools, checks script syntax, runs the dispatch gate tests and packages the distribution. Game-dependent integration checks require a user's local installation and are not run in public CI. No game assemblies, game code extracts or gameplay logs are included in the repository or release.
