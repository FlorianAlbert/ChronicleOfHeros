var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.ChronicleOfHeros_Api>("Api");

builder.AddProject<Projects.ChronicleOfHeros_Web>("Web")
	.WithReference(api);

builder.Build().Run();