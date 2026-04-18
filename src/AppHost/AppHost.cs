var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Modulith_Api>("api");

builder.Build().Run();
