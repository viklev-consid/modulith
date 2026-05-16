var builder = DistributedApplication.CreateBuilder(args);

var dbPassword = builder.AddParameter("db-password", secret: true);

var postgres = builder.AddPostgres("db", password: dbPassword)
    .WithPgAdmin();

var redis = builder.AddRedis("cache");

var mailpit = builder.AddMailPit("mailpit", httpPort: 8025, smtpPort: 1025);

var migrations = builder.AddProject<Projects.Modulith_MigrationService>("migrations")
    .WithReference(postgres)
    .WaitFor(postgres);

_ = builder.AddProject<Projects.Modulith_Api>("api")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(mailpit)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitForCompletion(migrations);

await builder.Build().RunAsync();
