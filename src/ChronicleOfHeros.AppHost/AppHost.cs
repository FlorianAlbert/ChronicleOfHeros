var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("postgres")
	.AddDatabase("chronicleofheros");

var api = builder.AddProject<Projects.ChronicleOfHeros_Api>("api")
	.WithReference(database)
	.WaitFor(database);

builder.AddProject<Projects.ChronicleOfHeros_Web>("web")
	.WithReference(api)
	.WaitFor(api);

builder.Build().Run();