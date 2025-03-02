using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Enliven>("enliven")
    .WithExternalHttpEndpoints();

builder.Build().Run();