---
description: Run format, build, arch tests, and unit tests on changed projects
allowed-tools: Bash(dotnet:*), Bash(git diff:*), Bash(git status:*)
---

Run the local validation suite, scoped to what changed.

1. Find changed `.cs` files: `git diff --name-only HEAD` plus `git status --short`.
2. From those, derive the set of affected `.csproj` paths (walk up from each file to the nearest `.csproj`).
3. For each affected project:
   - `dotnet format <csproj> --verify-no-changes` (whitespace + style + analyzer fixes)
   - `dotnet build <csproj> --nologo` (triggers Roslynator, Sonar, Meziantou analyzers as part of build with warnings-as-errors)
4. Always run the architecture test project (full suite is cheap and non-negotiable):
   `dotnet test tests/**/Architecture*.csproj --nologo`
5. Run unit tests for affected modules only (not integration — those are slow):
   `dotnet test <module-unit-test-csproj> --nologo`
6. Report results as a short table: project | format | build | tests.

If anything fails, stop and summarize the failures clearly — do not attempt fixes without being asked. For format failures specifically, the user can run `dotnet format <csproj>` to auto-fix, or you can offer to — but wait for confirmation.
