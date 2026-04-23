var builder = DistributedApplication.CreateBuilder(args);

var dbPassword = builder.AddParameter("db-password", secret: true);

var postgres = builder.AddPostgres("db", password: dbPassword)
    .WithPgAdmin();

var redis = builder.AddRedis("cache");

var mailpit = builder.AddMailPit("mailpit");

_ = builder.AddProject<Projects.Modulith_Api>("api")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(mailpit)
    .WaitFor(postgres)
    .WaitFor(redis);

await builder.Build().RunAsync();
