var builder = DistributedApplication.CreateBuilder(args);

// Named data volume: the depot data (and its history) stays when the Postgres container is
// recreated, e.g. after an Aspire or Docker Desktop update. A persistent container alone keeps
// the data only as long as that same container exists.
// Fixed password from user secrets (Parameters:postgres-password). The data volume keeps the password
// of its first start, so a new generated password on each run would lock the API out of its own data.
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword)
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
