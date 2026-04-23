---
description: Scaffold a new vertical slice in an existing module
argument-hint: <module> <feature>
allowed-tools: Bash(dotnet new:*), Bash(ls:*), Bash(find:*), Read, Edit
---

Scaffold a new vertical slice.

Arguments: `$ARGUMENTS` — expected format: `<Module> <Feature>`

Steps:

1. Parse the two arguments. If either is missing, stop and ask the user.
2. Verify the module exists: `ls src/Modules/`. If not, stop — do not create a new module implicitly (use `/new-module` for that).
3. Run: `dotnet new modulith-slice --module <Module> --name <Feature> --output src/Modules/<Module>/Modulith.Modules.<Module>/Features/<Feature>`
   (Adjust flags to match the actual template name in this repo — check `dotnet new list` if unsure.)
4. List the generated files with `ls src/Modules/<Module>/Modulith.Modules.<Module>/Features/<Feature>/` and verify all six pieces exist: Request, Response, Command, Handler, Validator, Endpoint.
5. Report back what was created and what the user should fill in next (typically: Command properties, Validator rules, Handler logic, Endpoint route).

Do not write handler logic yet — the point of this command is scaffolding only.
