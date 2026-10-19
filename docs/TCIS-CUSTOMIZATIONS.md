# TCIS customization notes

Compared with template baseline `ddc2ebbe0a325c1d060951dde02a2bff4dca8a80` on branch `main` (2026-10-19), the repository diff contains 39 added or modified files.

## API
- `src/Api/Endpoints/ContainerEndpoints.cs` — Added authenticated REST routes for container operations.
- `src/Api/Program.cs` — Registers container endpoint mapping.

## Application
- `src/Application/Abstractions/Data/IAppDbContext.cs` — Adds the Containers DbSet to the persistence abstraction.
- `src/Application/Features/Containers/Create/CreateContainerCommand.cs` — Create-container request contract.
- `src/Application/Features/Containers/Create/CreateContainerCommandHandler.cs` — Creates a container and handles duplicates.
- `src/Application/Features/Containers/Create/CreateContainerValidator.cs` — Validates container number and depot fields.
- `src/Application/Features/Containers/Delete/DeleteContainerCommand.cs` — Delete request contract.
- `src/Application/Features/Containers/Delete/DeleteContainerCommandHandler.cs` — Deletes containers while protecting visit history.
- `src/Application/Features/Containers/Get/GetContainerQuery.cs` — Single-container query/response contract.
- `src/Application/Features/Containers/Get/GetContainerQueryHandler.cs` — Loads a container by ID.
- `src/Application/Features/Containers/GetAll/GetAllContainersQuery.cs` — Paginated list query contract.
- `src/Application/Features/Containers/GetAll/GetAllContainersQueryHandler.cs` — Loads and maps the paginated list.

## Domain
- `src/Domain/Entities/Container.cs` — Replaces the simple status-based container with depot attributes (ISO code, size, category, weights, manufacture date, owner/operator, condition).
- `src/Domain/Entities/Block.cs` — Block and slot rules, including 20ft/40ft bay parity and virtual blocks.
- `src/Domain/Entities/ContainerVisit.cs` — Gate-in, movement, gate-out and dwell-time rules; validates delivery orders on gate-out.
- `src/Domain/Entities/Customer.cs` — Customer master data.
- `src/Domain/Entities/DeliveryOrder.cs` — Delivery-order master data, expiry and order lines.
- `src/Domain/Entities/DeliveryOrderLine.cs` — Requested and remaining quantities by container type.
- `src/Domain/Entities/LineOperator.cs` — Shipping-line/operator master data.
- `src/Domain/Enums/ContainerEnums.cs` — Container size/category/condition/grade enums.
- `src/Domain/Services/ContainerNumberRules.cs` — ISO 6346 container-number format and check-digit validation.

## Infrastructure / persistence
- `src/Infrastructure/Persistence/AppDbContext.cs` — Registers depot DbSets and configuration.
- `src/Infrastructure/Persistence/AppDbSeeder.cs` — Seeds sample containers and depot master data for development.
- `src/Infrastructure/Persistence/Configurations/ContainerConfiguration.cs` — Container mapping, uniqueness and enum storage.
- `src/Infrastructure/Persistence/Configurations/BlockConfiguration.cs` — Block mapping and constraints.
- `src/Infrastructure/Persistence/Configurations/ContainerVisitConfiguration.cs` — Visit mapping and partial unique indexes for one open visit per container and one active container per slot.
- `src/Infrastructure/Persistence/Configurations/CustomerConfiguration.cs` — Customer mapping and unique tax code.
- `src/Infrastructure/Persistence/Configurations/DeliveryOrderConfiguration.cs` — Order mapping and unique order number.
- `src/Infrastructure/Persistence/Configurations/DeliveryOrderLineConfiguration.cs` — Order-line mapping and relationship.
- `src/Infrastructure/Persistence/Configurations/LineOperatorConfiguration.cs` — Operator mapping and unique code.

## Migrations
- `src/Infrastructure/Persistence/Migrations/20261019030813_AddContainer.cs` — Initial migration for the simple container model.
- `src/Infrastructure/Persistence/Migrations/20261019030813_AddContainer.Designer.cs` — EF Core metadata for that migration.
- `src/Infrastructure/Persistence/Migrations/20261019081943_DepotDataModel.cs` — Evolves schema to the depot model; inspect data-deletion operations before applying to important data.
- `src/Infrastructure/Persistence/Migrations/20261019081943_DepotDataModel.Designer.cs` — EF Core metadata for the depot migration.
- `src/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` — Updates EF Core's model snapshot.

## Tests
- `tests/Application.UnitTests/Features/Depot/BlockTests.cs` — Tests slot range, bay parity and virtual blocks.
- `tests/Application.UnitTests/Features/Depot/ContainerNumberRulesTests.cs` — Tests valid/invalid container numbers and check-digit calculation.
- `tests/Application.UnitTests/Features/Depot/ContainerVisitTests.cs` — Tests gate-in/out, expired orders, exhausted quantities and wrong operator.
- `tests/Application.UnitTests/Features/Depot/CreateContainerCommandHandlerTests.cs` — Tests create success, duplicate conflict and invalid check digit.

## Review notes
1. Container status is now derived from the current yard visit rather than the old `Container.Status` workflow; the status PATCH endpoint was removed.
2. Confirm the partial unique indexes exist in the generated PostgreSQL migration, not only in EF configuration.
3. The `DepotDataModel` migration changes schema and deletes old-model container rows according to its commit message. Back up and review SQL before applying to valuable data.
4. These notes cover committed changes on GitHub, not uncommitted local working-tree edits.
