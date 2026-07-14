var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("postgres")
	.AddDatabase("chronicleofheros");

var api = builder.AddProject<Projects.ChronicleOfHeros_Api>("Api")
	.WithReference(database);

builder.AddProject<Projects.ChronicleOfHeros_Web>("Web")
	.WithReference(api);

builder.Build().Run();