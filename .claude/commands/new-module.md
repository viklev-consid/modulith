---
description: Scaffold a new module with all projects wired up
argument-hint: <ModuleName>
allowed-tools: Bash(dotnet new:*), Bash(dotnet sln:*), Bash(ls:*), Read
---

Scaffold a new module.

Argument: `$ARGUMENTS` — the module name in PascalCase (e.g. `Billing`).

This is a structural change — confirm with the user before proceeding:

1. Echo the plan: module name, projects to be created (`Modulith.Modules.<Name>` and `Modulith.Modules.<Name>.Contracts`), solution file additions.
2. Ask the user to confirm before running anything.
3. On confirmation, run the module template: `dotnet new modulith-module --name <Name> --output src/Modules/<Name>`
4. Add the new projects to the solution.
5. List what was created and remind the user of the follow-ups they own:
   - Register the module in the host composition root (`src/Api/Program.cs` and `src/AppHost/AppHost.cs`)
   - Add a `CLAUDE.md` inside the module summarizing its invariants
   - Draft an ADR if this module introduces a significant design decision
