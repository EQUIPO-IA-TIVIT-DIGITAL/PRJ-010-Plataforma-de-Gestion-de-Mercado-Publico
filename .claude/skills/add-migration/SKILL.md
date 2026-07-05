---
name: add-migration
description: Create a new SQL migration file for MPM with correct VXXX__ numbering and usp_ stored procedure naming
---

Create a new SQL migration for the MPM project using the argument as the description suffix.

Steps:
1. Scan `src/MPM.Api/Database/Scripts/` and find the highest VXXX number currently used (files follow pattern `VXXX__Description.sql`).
2. Increment by 1 to get the next version number (zero-padded to 3 digits).
3. Create `src/MPM.Api/Database/Scripts/VXXX__$ARGUMENTS.sql`.
4. If the description contains "usp" or "sp" or "procedure", scaffold a stored procedure template using `CREATE OR REPLACE PROCEDURE usp_Module_Action(...)`. Otherwise scaffold a `CREATE TABLE IF NOT EXISTS` template.
5. Print the created filename and remind the user that no `.csproj` changes are needed — all `.sql` files in `Database/Scripts/` are already picked up by the embedded resource glob in `MPM.Api.csproj`.
6. Migrations are applied automatically on next API startup by `DatabaseInitializer` in order of filename.
