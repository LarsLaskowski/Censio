# Tasks: Layout, theming and navigation with MudBlazor

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: `UiTheme`, `UiLanguage`, `UiPreferences` (`ThemeCookieName`, `LanguageCookieName`, `ReadTheme`, `ReadLanguage`, `StoredValue`), `UiTexts` (`For`, `LanguageName`, `NavigationButton`, `HomeEntry`, `ProfileSwitch`, `SwitchToDarkTheme`, `SwitchToLightTheme`, `LanguageSwitch`), `CensioTheme` (`Create`), `UiLog` (`PreferenceNotStored`); MudBlazor and bunit in `Directory.Packages.props`, MudBlazor in `Censio.Server.csproj`; *Build* and the existing server tests pass (fact 9) | `src/Censio.Server/*.cs`, `Directory.Packages.props`, `src/Censio.Server/Censio.Server.csproj` | AC2, AC6, AC9, AC10 | — | Dev | [ ] |
| 2 | G1 | Tests first: preference reading, texts, palette contrast; test project gets the bunit reference, `InvariantGlobalization`, the `RecordingLoggerProvider.cs` link (facts 11) | `tests/Censio.Server.Tests/UiPreferencesTests.cs`, `UiTextsTests.cs`, `CensioThemeTests.cs`, `Censio.Server.Tests.csproj` | AC2, AC6, AC9, AC10 | 1 | Tester | [ ] |
| 3 | G1 | Implement `UiPreferences`, `UiTexts`, `CensioTheme` | `src/Censio.Server/*.cs` | AC2, AC6, AC9, AC10 | 2 | Dev | [ ] |
| 4 | G2 | Skeleton: `Routes` parameters `Theme`, `Language`; `MainLayout` cascading parameters `InitialTheme`, `InitialLanguage` | `Components/Routes.razor`, `Components/Layout/MainLayout.razor` | AC1, AC3, AC4, AC5, AC7, AC8 | 3 | Dev | [ ] |
| 5 | G2 | Tests first: `MainLayoutTests` (bUnit) and the new `ProgramTests` (fact 10) | `tests/Censio.Server.Tests/MainLayoutTests.cs`, `ProgramTests.cs`, `CascadedLanguageProbe.cs` | AC1, AC2, AC3, AC4, AC5, AC7, AC8, AC9, AC11, AC12 | 4 | Tester | [ ] |
| 6 | G2 | Implement the frame: `App.razor`, `Routes.razor`, `MainLayout.razor`, `Home.razor`, `_Imports.razor`, `Program.cs`, `wwwroot/js/censio.js`, `wwwroot/css/censio.css`, `UiLog`; *Image build check* | `src/Censio.Server/Components/**`, `Program.cs`, `wwwroot/js/censio.js`, `wwwroot/css/censio.css` | AC1, AC3, AC4, AC5, AC7, AC8, AC9, AC11, AC12 | 5 | Dev | [ ] |
| 7 | G2 | Coverage exclusion of `src/Censio.Server/wwwroot/js/**` | `.github/workflows/ci.yml` | AC11 | 6 | Dev | [ ] |
| 8 | G2 | Documentation: `docs/areas/web-ui.md` and its index row, `server.md`, `ARCHITECTURE.md`, `README.md`, `USER_GUIDE.md`; report the `.squad/project.md` text (Network exposure, Integration surface) | as plan *Documentation updates* | AC1, AC2, AC5, AC10 | 6 | Dev | [ ] |
| 9 | G2 | Report the `.squad/project.md` *Test doubles* lines for `RecordingLoggerProvider` and `CascadedLanguageProbe` | `.squad/project.md` (via the orchestrator) | AC9 | 5 | Tester | [ ] |
| 10 | PM | Manual check on a local run of the branch head: restart persistence, freed width, rendered contrast and focus rings, palette approval | — | AC4, AC5, AC8, AC10 | 8 | Product Manager | [ ] |
| 11 | PM | Set `Area: web-ui` in records 0071 and 0072 once the index row exists | `docs/decisions/0071-*.md`, `docs/decisions/0072-*.md` | AC1 | 8 | Lead | [ ] |
