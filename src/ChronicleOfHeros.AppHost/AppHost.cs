var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ChronicleOfHeros_Api>("Api");

builder.Build().Run();