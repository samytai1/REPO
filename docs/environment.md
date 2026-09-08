# Environment reference

Read when the toolchain misbehaves.

## Frontend

- **PrimeNG must not be an `-lts` version.** The LTS builds render a red "invalid license" banner over every page. Pinned to `20.4.0`.
- PrimeNG 20 peers need `@angular/animations` and `@angular/cdk` installed explicitly; `ng new` does not add them.
- PrimeNG 20 module names that differ from the tag: `TextareaModule` (`pTextarea`), `DatePickerModule` (`p-datepicker`), `ToggleSwitchModule` (`p-toggleswitch`), `SelectModule` (`p-select`), `DrawerModule` (`p-drawer`).
- After changing an npm dependency, **restart `ng serve`** — Vite serves a stale bundle and reports phantom module-resolution errors otherwise.
- Production bundle budgets were raised to 1 MB / 2 MB; PrimeNG + Aura puts the baseline near 670 kB.
- Karma needs `$env:CHROME_BIN = "C:\Program Files\Google\Chrome\Application\chrome.exe"`.

## Backend

- `global.json` pins .NET SDK 9.0.316; SDK 10 is also installed — do not let templates target `net10.0`.
- Connection string lives in `src\CMS.API\appsettings.json` under `ConnectionStrings:CMS` (`Server=.\SQLEXPRESS;Database=CMS;Trusted_Connection=True;…`).
- While `dotnet run` is up, `bin\Debug\net9.0\CMS.API.exe` / `.dll` are locked: a plain `dotnet build` fails on the copy step even though compilation succeeded. Test with `-p:OutDir=<scratch>/`, or stop the API first. A running API also keeps serving the **old** build — restart it to see new endpoints.

## Database

- Read-only checks against the real schema: `sqlcmd -S ".\SQLEXPRESS" -d CMS -C -i file.sql` (PowerShell). `-C` is required — ODBC Driver 18 rejects the self-signed certificate otherwise. Do not pass `-E`; Windows auth is the default and `-E` conflicts with an environment-set user.
- Live data sizes that matter for UI thresholds: ~1,084 courses, 215 course groups (over the 100-row virtual-scroll threshold), 39 certifications, 18 job categories.

## Shell (Claude Code sessions)

- `python` on this machine is the Windows Store stub — it silently does nothing. Use Node, PowerShell, or the Edit tool for file patches.
- Very long Bash heredocs fail to spawn (`ENAMETOOLONG`); write large files with the Write tool.
