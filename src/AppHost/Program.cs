var builder = DistributedApplication.CreateBuilder(args);

// Named data volume: the depot data (and its history) stays when the Postgres container is
// recreated, e.g. after an Aspire or Docker Desktop update. A persistent container alone keeps
// the data only as long as that same container exists.
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume("depot-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("TechSpherex-db");

var redis = builder.AddRedis("TechSpherex-cache")
    .WithRedisInsight()
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.TechSpherex_CleanArchitecture_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(redis)
    .WaitFor(redis)
    .WithExternalHttpEndpoints();

builder.Build().Run();
