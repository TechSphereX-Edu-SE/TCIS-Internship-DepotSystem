var builder = DistributedApplication.CreateBuilder(args);

// Named data volume: the depot data (and its history) stays when the Postgres container is
// recreated, e.g. after an Aspire or Docker Desktop update. A persistent container alone keeps
// the data only as long as that same container exists.
// Fixed password from user secrets (Parameters:postgres-password). The data volume keeps the password
// of its first start, so a new generated password on each run would lock the API out of its own data.
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    // pgAdmin kept between runs too: otherwise it is recreated and slow to start on every AppHost run
    .WithPgAdmin(pgAdmin => pgAdmin.WithLifetime(ContainerLifetime.Persistent))
    .WithDataVolume("depot-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("TechSpherex-db");

var redis = builder.AddRedis("TechSpherex-cache")
    .WithRedisInsight()
    .WithLifetime(ContainerLifetime.Persistent);

/*
    Feedback 10: demo data buttons on the api resource (dashboard: api > ... > commands).
    The key is new on every AppHost run and only the dashboard knows it, so the demo routes cannot be
    called by anything else on the machine; an Admin token also works (Scalar).
    Reset deletes the depot data, so it asks for confirmation and sends confirm=reset-demo.
*/
var demoKey = Guid.NewGuid().ToString("N");

builder.AddProject<Projects.TechSpherex_CleanArchitecture_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(redis)
    .WaitFor(redis)
    .WithExternalHttpEndpoints()
    .WithEnvironment("Demo__Enabled", "true")
    .WithEnvironment("Demo__Key", demoKey)
    .WithHttpCommand("/api/demo/seed", "Tạo / bổ sung dữ liệu demo", commandName: "demo-seed",
        commandOptions: DemoCommand(demoKey,
            "Tạo bộ 140 container demo, hoặc chỉ thêm phần còn thiếu. Không sửa dữ liệu đã có, không đổi ngày.",
            icon: "Database", confirmation: null))
    .WithHttpCommand("/api/demo/reset?confirm=reset-demo", "Reset dữ liệu demo", commandName: "demo-reset",
        commandOptions: DemoCommand(demoKey,
            "Xoá toàn bộ dữ liệu depot (giữ tài khoản) rồi tạo lại bộ demo với mốc ngày mới.", icon: "Delete",
            confirmation: "Xoá TOÀN BỘ dữ liệu depot (container, lượt vào bãi, lệnh, sửa chữa, đối tác, block) " +
                          "rồi tạo lại bộ demo? Không hoàn tác được."));

builder.Build().Run();

// POST with the demo key; the JSON report (anchor, containers created, refused steps) is shown as the result
static HttpCommandOptions DemoCommand(string demoKey, string description, string icon, string? confirmation) => new()
{
    Method = HttpMethod.Post,
    Description = description,
    ConfirmationMessage = confirmation,
    IconName = icon,
    ResultMode = HttpCommandResultMode.Json,
    PrepareRequest = context =>
    {
        context.Request.Headers.Add("X-Demo-Key", demoKey);
        return Task.CompletedTask;
    }
};
