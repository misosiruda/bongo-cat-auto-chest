# Validation

## v1.1.0 — Windows wizard

- 12 wizard model checks passed: own-only defaults, existing choices, pause, target selection, delay validation, INI parsing, JSON roundtrip, secondary Steam libraries and Windows argument quoting.
- Additional integration checks passed for saving explicit target choices, removing duplicate old keys, preserving comments/unknown settings, invariant decimal formatting, and refusing invalid settings without changing the file.
- The existing dispatch and game-copy installation/update/restore tests passed with the new defaults and settings writer.
- Native Windows UI was exercised: automatic game discovery, loading existing choices, independently unchecking other players' chests, blocking an empty target selection, and pausing automation. An initially misplaced footer button was corrected and visually checked.
- The packaged EXE completed real installation, settings save and Steam launch on the existing local game. Its prior own/others choices and 3–8 second interval were retained. The game logged a successful helper load after restart.
- Desktop shortcut creation succeeded. The shortcut points to a retained EXE in the user's local app-data directory, so deleting the downloaded copy does not break it.
- Known UI condition: Bongo Cat's always-on-top overlay can cover the wizard. Close the game before setup as described in the README.
- No second physical PC, high-DPI display matrix, administrator restart/UAC flow, or manual global-hotkey test was performed. The executable is not code-signed.

## v1.0.0 — Original launcher

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
