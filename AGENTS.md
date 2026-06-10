# Beehive

Beehive is a utility to manage a cluster of [Bee Swarm](https://github.com/ethersphere/bee) nodes. It is an **ASP.NET Core application** (not a set of libraries): it exposes a REST API to register and interact programmatically with different Bee nodes, acts as a Bee-compatible Swarm gateway in front of the cluster, and runs an async task engine (Hangfire) for recurring operations like automatic scheduled cashout. It uses a MongoDB instance to keep cluster configuration and cached/persisted Swarm data.

## Build, run, test

Target framework is **.NET 10** with `TreatWarningsAsErrors=true` and `AnalysisMode=AllEnabledByDefault` (and `Nullable=enable`, `EnableNETAnalyzers=true`) on every project — warnings break the build.

```bash
dotnet restore Beehive.sln
dotnet build Beehive.sln -c Release
dotnet test  Beehive.sln -c Release                 # runs the xUnit test project
dotnet test test/Beehive.Persistence.Tests/Beehive.Persistence.Tests.csproj           # single project
dotnet test --filter "FullyQualifiedName~BeehiveDbContextDeserializationTest"          # single class
dotnet test --filter "FullyQualifiedName~BeehiveDbContextDeserializationTest.BeeNodeDeserialization"        # single test
dotnet run  --project src/Beehive                   # local dev server, listens on http://localhost:1633
```

There is no frontend build step: the host has only a minimal Razor Pages landing page plus static `wwwroot` assets (no npm, no bundler). A plain `dotnet build` produces a runnable host.

Running the app requires a reachable **MongoDB** instance — supply `ConnectionStrings:BeehiveDb` and `ConnectionStrings:HangfireDb` (config or environment), plus `Elastic:Urls` for the Serilog sink and the `ASPNETCORE_ENVIRONMENT` variable. The easiest way to run locally with its dependencies is the Docker Compose sample under [samples/docker-beehive-sample](samples/docker-beehive-sample). `Directory.Build.props` sets `NuGetAuditMode=direct`, so only direct package vulnerabilities are audited.

Docker: `docker build .` (uses `Dockerfile`, which runs `dotnet test` as part of the build stage and exposes port `1633`).

## Architecture

Four-project layered solution, plus one test project. Root namespace is `Etherna.Beehive[.<Layer>]` and the namespace mirrors the folder path under it.

- **`src/Beehive.Domain`** (`Etherna.Beehive.Domain`) — Pure domain layer. Entities live flat under `Models/` (e.g. `BeeNode`, `Chunk`, `ChunkPin`, `PostageBatchCache`, `PostageStamp`, the `ResourceLockBase` hierarchy). Base classes: `ModelBase`, `EntityModelBase`, `EntityModelBase<TKey>`. Domain events under `Events/` are dispatched via `Etherna.DomainEvents`. The layer exposes the `IBeehiveDbContext` interface (repositories + a GridFS `ChunksBucket` + the `IEventDispatcher`). Depends on `MongODM.Core`, `Etherna.DomainEvents`, `Nethereum.Web3`, and `SwarmSdk` (Swarm value types like `SwarmHash`, `SwarmReference`, `PostageBatchId`).
- **`src/Beehive.Persistence`** (`Etherna.Beehive.Persistence`) — MongODM implementation. `BeehiveDbContext` declares the repositories and their indexes; `ModelMaps/` defines how each entity serializes; `Repositories/DomainRepository` is a generic repository that dispatches domain events on create/delete; `Serializers/` holds custom BSON `SerializerBase<T>` implementations for SwarmSdk value types (`SwarmHash`, `SwarmAddress`, `SwarmReference`, `SwarmUri`, `PostageBatchId`). References `Beehive.Domain` only.
- **`src/Beehive.Services`** (`Etherna.Beehive.Services`) — Application services and side effects. `Domain/` holds services that orchestrate domain operations (`BeeNodeService`, `DataService`, `PinService`, `PostageBatchService`, `ResourceLockService`). `EventHandlers/` follows the `On<Event>Then<Action>Handler` convention and is auto-discovered by reflection in `ServiceCollectionExtensions.AddDomainServices` — any `IEventHandler` (typically via `EventHandlerBase<TEvent>`) placed in this namespace is registered automatically. `Tasks/` holds Hangfire jobs split by trigger (`Cron/`, `Trigger/`, `Background/`). `Utilities/` holds `BeeNodeLiveManager` (the singleton tracking live node status) and `BeehiveChunkStore`. `Options/` holds configuration objects. References `Beehive.Domain`.
- **`src/Beehive`** (`Etherna.Beehive`) — ASP.NET Core host (Minimal APIs + a minimal Razor Pages site). `Program.cs` wires everything: the Minimal API endpoints, three OpenAPI documents + Scalar API Reference, MongODM with Hangfire (Mongo storage), Serilog → Elasticsearch, CORS, the MongODM admin dashboard at `/admin/db`, and the Hangfire dashboard at `/admin/hangfire`. The API lives under `Areas/Api/`; cross-cutting host code is under `Configs/`, `Extensions/`, `JsonConverters/`, `Options/`. References all three other projects.
- **`test/Beehive.Persistence.Tests`** (xUnit + Moq) — focuses on model-map deserialization: each `ModelMap` GUID/schema version is pinned by a stored BSON document and asserted to deserialize into the expected entity.

### API design

The API is built with **Minimal APIs**, not MVC controllers — this is the central pattern to follow when adding endpoints:

- Routes are registered in static `*ApiMapper` classes (`BeehiveApiMapper`, `SwarmApiMapper`) and mounted from `Program.ConfigureApplication` via `app.MapBeehiveApi()` / `app.MapSwarmApi()`. Each mapper builds a `RouteGroupBuilder` and tags it with a **marker** (`BeehiveApiMarker`, `SwarmApiMarker`, `SwarmV1ApiMarker`).
- Every route delegates to a **handler interface** (`IBeehiveApiHandler`, `IBytesApiHandler`, `IChunksApiHandler`, …) implemented by a `sealed` handler class in `Areas/Api/` or `Areas/Api/SwarmApiHandlers/`, registered `AddScoped` in `Program`. Handler methods return `Task<IResult>` and wrap their body in `ExceptionHandler.RunAsync(ApiVersion.X, async () => { … })`.
- Responses are serialized with the curated `JsonSerializerOptions` in `Configs/CommonConsts` (camelCase + the SwarmSdk/Beehive JSON converters): return `Results.Json(dto, CommonConsts.BeehiveV04JsonSerializerOptions)` (or `SwarmJsonSerializerOptions`). DTOs use the `Dto` suffix (`Areas/Api/DtoModels/`), request bodies use the `Input` suffix (`Areas/Api/InputModels/`).
- There are **three OpenAPI documents**, each registered with its own `AddOpenApi` pipeline in `Program` and filtered to its marker via `MetadataFilterDocumentTransformer<TMarker>`: `beehive04` (Beehive's own management API), `swarm` (a Bee-node-compatible Swarm API, no prefix), and `swarmv1` (the same surface under `/v1`). The Swarm surface mirrors the Bee HTTP API so SwarmSdk's generated client can talk to Beehive as if it were a Bee node. Transformers live in `Configs/OpenApi/`.

### Key cross-cutting points

- **OpenAPI binary request bodies must be inlined.** Binary upload bodies (`[FromBody] Stream`) must be emitted inline as `{type: string, format: binary}`, never as `$ref: #/components/schemas/Stream` — NSwag (the SwarmSdk client generator) mishandles a `$ref`-to-binary *request* body and produces a JSON-serialized DTO that breaks uploads (responses keep `$ref: Stream`, which NSwag maps correctly to a streaming response, so the component stays). **Two mechanisms are both required and non-redundant**: `BinaryRequestBodyDocumentTransformer` (registered in all three `AddOpenApi` pipelines) fixes the `.Accepts<Stream>` endpoints by matching the resolved `Type==String && Format=="binary"` schema, and the `AcceptsUnrestrictedOperationTransformer` fix handles `/bzz` (`.AcceptsUnrestricted<Stream>`), which the document transformer cannot catch. Don't "simplify" by deleting either one.
- **MongODM change tracking is opt-in per method.** Every domain method that mutates a property *must* be annotated with `[PropertyAlterer(nameof(Prop))]` for each modified property — this is required, not optional (see `ChunkPin.UpdateProcessed`).
- **Model map IDs are fresh random GUIDs.** Every `MapRegistry.AddModelMap<T>("<guid>")` call (and every map inside a `ReferenceSerializer` config) needs a brand-new, randomly generated GUID (e.g. `uuidgen`) that collides with no existing map ID anywhere in the solution — never copy, edit, or hand-craft one. The ID permanently identifies that schema version; a collision silently corrupts serialization. When you add or change a map, add a matching deserialization test in `Beehive.Persistence.Tests` that pins the GUID against a sample document.
- **Index definitions are strongly typed.** In `BeehiveDbContext` `IndexBuilders`, always select fields with lambda expressions (`Builders<BeeNode>.IndexKeys.Ascending(n => n.ConnectionString)`), never magic strings — this keeps compile-time safety against renames.
- **Domain events fire from persistence.** `BeehiveDbContext.SaveChangesAsync` dispatches the accumulated `EntityModelBase.Events` after saving, and `DomainRepository` additionally dispatches `EntityCreatedEvent`/`EntityDeletedEvent` (plus custom events) on create/delete. To get this behavior a repository must be wired as `DomainRepository<T, TKey>` rather than the plain `Repository<T, TKey>` — choose deliberately (in `BeehiveDbContext`, `BeeNodes`, the lock repositories and `ChunkPins` are `DomainRepository`; `Chunks`, `ChunkPushQueue`, `PostageBatchesCache`, `PostageStamps` are plain `Repository`).
- **Hangfire queues and jobs.** Queues are declared in `Services/Tasks/Queues.cs` (`DB_MAINTENANCE`, `DOMAIN_MAINTENANCE`, `NODE_MAINTENANCE`, `PIN_CONTENTS`) and pinned in `Program.AddHangfireServer` alongside `"default"`. The Hangfire **server is not started in Staging** (see the `!env.IsStaging()` guard). Recurring jobs are registered in `Program.ConfigureApplication` via `RecurringJob.AddOrUpdate<I…Task>(…, task => task.RunAsync(), Cron.…)` using each task's `TaskId` const.
- **Tasks are split by trigger.** `Tasks/Cron/` = recurring jobs (e.g. `CashoutAllNodesChequesTask`, `CleanupExpiredLocksTask`, `NodesAddressMaintainerTask`); `Tasks/Trigger/` = enqueued-on-demand jobs (e.g. `PinChunksTask`); `Tasks/Background/` = `IHostedService` workers (e.g. `PushChunksBackgroundService`, gated by config `PushChunks:Enabled`). Each task exposes an `I<Task>` interface and is registered `AddTransient`.
- **`BeeNodeLiveManager` is a singleton** that tracks the live status of registered Bee nodes; it is started in `Program` via `app.StartBeeNodeLiveManager()` and kept in sync by the `OnBeeNodeCreated…`/`OnBeeNodeDeleted…` event handlers.
- **Swarm types use the SwarmSdk libraries** (`Etherna.SwarmSdk` / `Etherna.SwarmSdk.Client`); the project migrated off bee.net. `IChunkService`/`IFeedService` from SwarmSdk are registered in DI. Persistence serializes Swarm value types via the custom serializers in `Persistence/Serializers/`; the API serializes them via the JSON converters wired in `CommonConsts`. Chunk payloads are stored in a GridFS bucket (`BeehiveDbContext.ChunksBucket`).

## Issue tracker

Bugs and features are tracked in Jira project **BHM** (https://etherna.atlassian.net/projects/BHM). Branch names follow `feature/BHM-<id>-<slug>` / `improve/BHM-<id>-<slug>` / `fix/BHM-<id>-<slug>` — match this when creating branches.

# Coding Style

## General Principles

- Keep commits clean: only include changes strictly necessary for the task at hand.
- Never reference AI agents or assistants in commits or code — no agent names, no `Co-Authored-By` agent trailers, no "generated/assisted by" notes. Commit messages and code must read as the team's own work.
- Exceptions to these conventions are accepted when strictly necessary or when they significantly improve code quality. Justify with a comment where needed.
- All elements (usings, properties, methods, fields, enum members, etc.) are always alphabetically ordered within their respective sections.
- Primary constructors are preferred everywhere the constructor is a simple parameter assignment — not limited to DI services.
- Keep code clean: remove unused variables, dead code, and redundant imports.
- Every source file starts with the standard AGPL-3.0 copyright header (`// Copyright 2021-present Etherna SA` … see any existing file).

## Naming

- **Classes/Structs**: PascalCase (`BeeNodeService`, `ChunkPin`)
- **Interfaces**: `I` prefix (`IBeehiveDbContext`, `IPinService`)
- **Async methods**: always `Async` suffix (`CreateAsync`, `FindFeedUpdateAsync`)
- **Properties**: PascalCase (`ConnectionString`, `IsBatchCreationEnabled`)
- **Private fields**: `_camelCase` only when backing a same-named property (`_items` for `Items`); otherwise plain `camelCase`
- **Primary constructor parameters**: `camelCase` without underscore
- **Constants**: PascalCase, except the all-caps underscore style used for Hangfire queue names (`DB_MAINTENANCE`) — `CA1707` is disabled to allow these
- **Enums**: PascalCase type and members (`BeeNodeSelectionMode.RoundRobin`)
- **Namespaces**: `Etherna.Beehive.<Layer>.<Feature>` mirroring the folder under the root namespace (e.g. `Etherna.Beehive.Domain.Models`, `Etherna.Beehive.Services.Tasks.Cron`)
- **DTOs**: `Dto` suffix (`BeeNodeDto`); **API input models**: `Input` suffix (`BeeNodeInput`)
- **API handlers**: `<Feature>ApiHandler` implementing `I<Feature>ApiHandler`
- **Event handlers**: `On<Event>Then<Action>Handler` (e.g. `OnBeeNodeCreatedThenAddNodeStatusHandler`)
- **Hangfire tasks**: `<Name>Task` implementing `I<Name>Task`, with a `public const string TaskId`
- **Custom exceptions**: `Exception` suffix, always `sealed`

## Code Organization

- One class per file, filename matches class name
- Namespace mirrors folder structure exactly (under the `Etherna.Beehive` root namespace)
- Block-scoped namespaces: `namespace X { ... }` — NOT file-scoped
- Using directives inside the namespace block, always alphabetically ordered and kept to the minimum necessary
- No global usings — each file declares its own imports

## Comments

Principal comments (generally multiline, important):
```csharp
// Capital start, ending period.
// Continued on next line if needed.
```

Secondary/separator comments:
```csharp
//no space, no capital, no ending period
```

XML doc comments (`///`) document public API where they aid understanding (`CS1591` is disabled, so they are not mandatory on every public member).

## Member Ordering Within a Class

Use principal-style section comments to delimit groups, in this order:

```csharp
// Consts.
public const string TaskId = "myTask";

// Fields.
private List<Item> _items = [];

// Constructors.
public MyEntity(string name) { ... }
protected MyEntity() { }

// Properties.
public virtual string Name { get; set; }

// Methods.
public virtual void DoSomething() { ... }

// Helpers.
private void InternalHelper() { ... }
```

## Class Design

- `sealed` (often `internal sealed`) for service, handler, and event-handler implementations
- Primary constructors everywhere the constructor is a simple assignment
- Reflection-discovered types (event handlers, model-map collectors) are matched by namespace — keep them in the expected namespace

### Domain Entity Classes

- `public abstract` for base entity classes (`ModelBase`, `EntityModelBase`, `ResourceLockBase`)
- `virtual` on all properties (and mutating methods) for MongODM proxy support
- Use `public set` on properties only when external mutation is intended (e.g. `BeeNode.ConnectionString`); use `protected set` when the property requires validation or invariant enforcement, mutating it through a method
- Protected parameterless constructor for ORM deserialization:
  ```csharp
  #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
  protected EntityName() { }
  #pragma warning restore CS8618
  ```
- Collection encapsulation with backing fields and collection-expression setters:
  ```csharp
  private HashSet<SwarmHash> missingChunks = [];
  public virtual IEnumerable<SwarmHash> MissingChunks
  {
      get => missingChunks;
      protected set => missingChunks = new HashSet<SwarmHash>(value ?? []);
  }
  ```
- Equality is by ID for entities (implemented in `EntityModelBase<TKey>`); value objects compare by value
- `[PropertyAlterer(nameof(MyProp))]` on every method, for each property the method modifies — a MongODM change-tracking requirement:
  ```csharp
  [PropertyAlterer(nameof(IsProcessed))]
  [PropertyAlterer(nameof(MissingChunks))]
  [PropertyAlterer(nameof(TotPinnedChunks))]
  public virtual void UpdateProcessed(IEnumerable<SwarmHash> missingChunks, long totPinnedChunks) { ... }
  ```

## Async Patterns

- Always suffix with `Async`
- `CancellationToken cancellationToken = default` as the optional last parameter (non-nullable, `default` — not `CancellationToken? = null`)
- Return `Task` or `Task<T>`, never `async void`
- **No `ConfigureAwait(false)`** — this is an ASP.NET Core application, and `CA2007` is disabled in `.editorconfig`. (This is the opposite of the SwarmSdk *library* rule.)

## Null Handling

- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- `ArgumentNullException.ThrowIfNull(param)` for parameter validation
- `is null` / `is not null` (not `== null`)
- Prefer `null` over `default` as default value for optional reference parameters
- `??` and `??=` operators

## Formatting

- Allman braces (opening brace on new line)
- 4-space indentation (2 spaces in `.csproj` files, per `.editorconfig`)
- Expression-bodied members for single expressions:
  ```csharp
  public virtual bool IsSucceeded => IsProcessed && missingChunks.Count == 0;
  ```
- LINQ method chains: one operation per line, aligned
- Blank line between member sections

## C# Language Features

- Pattern matching: `is`, `is not`, type patterns, property patterns
- Switch expressions for multi-branch returns
- Primary constructors everywhere applicable
- Collection expressions: `[]`, `[..spread]`
- Prefer collection expressions over constructors to initialize any collection: `[]` not `new()`, `["a", "b"]` not `new List<string> { "a", "b" }`. Use a constructor only when a collection expression can't express the intent (e.g. presizing capacity, or building a specific set type like `new HashSet<T>(value ?? [])`).
- Target-typed `new()` when type is clear from context (for non-collection types)
- Tuple deconstruction for multiple return values
- Span/`Memory<byte>` and ranges (`data[..32]`, `data[cursor..]`) for byte-level slicing of chunk data — avoid unnecessary copies

## LINQ

- Method syntax preferred over query syntax
- Query syntax only for complex join/groupby with multiple `from` clauses (e.g. the reflection queries in `BeehiveDbContext`/`ServiceCollectionExtensions`)
- Fluent chaining, one operation per line for readability

## Dependency Injection

- Constructor injection exclusively
- Reflection-based event-handler discovery (by namespace) in `AddDomainServices`
- `AddScoped` for domain services, the DbContext, and API handlers
- `AddTransient` for Hangfire tasks
- `AddSingleton` for `BeeNodeLiveManager` and options/configuration objects
- `AddHostedService` for background workers

## Testing (xUnit + Moq)

- `[Fact]` for basic tests, `[Theory]` with `[MemberData]` for parameterized cases (e.g. the per-schema-version deserialization tables)
- AAA pattern with section comments: `// Setup.`, `// Action.`, `// Assert.`
- xUnit assertions: `Assert.Equal()`, `Assert.NotNull()`, `Assert.Throws<T>()` / `Assert.ThrowsAsync<T>()`
- Moq for mocking: `new Mock<IBeehiveDbContext>()`; entities under test are often built as `Mock<TEntity>` with `Setup(...)` on virtual members
- The test project mirrors the project under test (`Beehive.Persistence.Tests` mirrors `Beehive.Persistence`); add a new mirror test project only when a layer grows logic that needs covering
- When adding or changing a `ModelMap`, add a deserialization test that pins the new map GUID against a representative stored document (see `BeehiveDbContextDeserializationTest`)
