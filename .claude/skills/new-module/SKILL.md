---
name: new-module
description: Scaffold a complete new MPM domain module with Controllers/Services/Data/Models structure, .csproj, solution registration, and test project
---

Scaffold a new MPM domain module named $ARGUMENTS (e.g. "Contratos" → `MPM.Modules.Contratos`).

## Steps

### 1. Create the module project
Create `src/MPM.Modules.$ARGUMENTS/` with:
- `Controllers/` — empty folder placeholder
- `Services/` — empty folder placeholder  
- `Data/` — empty folder placeholder
- `Models/` — empty folder placeholder
- `MPM.Modules.$ARGUMENTS.csproj` referencing `MPM.Shared` and `MPM.Core`, targeting `net8.0`, with `<FrameworkReference Include="Microsoft.AspNetCore.App" />` and Dapper + Npgsql package references
- `ModuleRegistration.cs` with a static class containing `Add${ARGUMENTS}Module(this IServiceCollection services)` that returns `services`

### 2. Create the test project
Create `tests/MPM.Modules.$ARGUMENTS.Tests/`:
- `MPM.Modules.$ARGUMENTS.Tests.csproj` with xUnit, Moq, FluentAssertions, and a project reference to the module
- One stub test class `${ARGUMENTS}ModuleTests.cs` with a placeholder test

### 3. Register in the solution
Add both projects to `MPM.sln`:
- The module project under the `src` solution folder (GUID `{33E1A69A-8FD8-475E-B07A-5D48899703B4}`)
- The test project under the `tests` solution folder (GUID `{6FF04732-217E-4552-9425-96347221B392}`)
- Add both to the `GlobalSection(ProjectConfigurationPlatforms)` section for Debug and Release

### 4. Register in Program.cs
Add `builder.Services.Add${ARGUMENTS}Module();` to `src/MPM.Api/Program.cs` alongside the existing module registrations (after the last `Add...Module()` call).
Also add the using: `using MPM.Modules.$ARGUMENTS;`

### 5. Add project reference to API
Add a `<ProjectReference>` entry in `src/MPM.Api/MPM.Api.csproj` pointing to the new module.

### 6. Confirm
Print a summary of all created/modified files.
