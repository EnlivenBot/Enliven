using Projects;

var builder = DistributedApplication.CreateBuilder(args);

string lavalinkPassword = "6d623631-4d1f-4b7d-81e7-72900217c31c"; // Replace with the actual random password

var lavalink = builder.AddContainer("lavalink", "freyacodes/lavalink")
    .WithEndpoint(2333)
    .WithVolume("application.yml", "/app/application.yml")
    .WithEnvironment("LAVALINK_PASSWORD", lavalinkPassword);

builder.AddProject<Enliven>("enliven")
    .WithExternalHttpEndpoints();

builder.Build().Run();
