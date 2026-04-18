# ADR-0016: Centralized Package and Build Configuration

## Status

Accepted

## Context

In a multi-project solution, package versions drift unless managed centrally. One project ends up on Serilog 3.0 while another is on 3.2, and the result is diamond-dependency issues, runtime surprises, or inconsistent behavior.

.NET's Central Package Management (CPM) via `Directory.Packages.props` solves this cleanly: package versions are declared once at the solution level, and individual projects reference packages without specifying versions.

Similarly, many MSBuild properties (language version, nullable reference types, treat-warnings-as-errors, analyzer configuration) should be consistent across projects. `Directory.Build.props` solves this.

## Decision

### Directory.Packages.props

Lives at the solution root. Controls all package versions. Individual `.csproj` files reference packages without version numbers:

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="WolverineFx" Version="3.x.x" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.x.x" />
    <!-- ... -->
  </ItemGroup>
</Project>

<!-- Any .csproj -->
<ItemGroup>
  <PackageReference Include="WolverineFx" />
</ItemGroup>
```

Transitive pinning is enabled to avoid surprises from transitive dependencies.

### Directory.Build.props

Lives at the solution root. Applies properties to every project unless overridden:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>14.0</LangVersion>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsNotAsErrors></WarningsNotAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
  </PropertyGroup>
</Project>
```

### .editorconfig and .globalconfig

Live at the solution root. Control analyzer severities and code style rules uniformly.

### Changes to these files are capability-bounded

Modifying `Directory.Packages.props` or `Directory.Build.props` is not an autonomous agent action. Package choices are deliberate (documented in ADRs); changing language-level settings affects every project and every contributor. These files are listed in `CLAUDE.md` as things that require explicit instruction.

## Consequences

**Positive:**

- Package versions are consistent by construction.
- Upgrading a package is a one-line change.
- Transitive pinning prevents drift.
- Shared MSBuild settings apply uniformly — no drift across projects in nullable, analyzer rules, or language versions.
- Warnings as errors forces a clean baseline from day one.

**Negative:**

- Adding a package requires two edits (declaration + reference). Minor.
- A single bad version bump affects the whole solution. Mitigated by the CI pipeline and by agent guidance around not editing these files autonomously.
- `TreatWarningsAsErrors` can be annoying during refactors. Temporary `<WarningsNotAsErrors>` escape hatch is documented.

## Related

- ADR-0027 (Agentic Development): these files are capability-bounded for agents.
