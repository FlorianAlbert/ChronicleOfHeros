var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("postgres")
	.AddDatabase("chronicleofheros");
var bootstrapOperatorUsername = builder.AddParameter("bootstrap-operator-username", secret: true);
var bootstrapOperatorTemporaryPassword = builder.AddParameter("bootstrap-operator-temporary-password", secret: true);

var migrations = builder.AddProject<Projects.ChronicleOfHeros_Migrations>("migrations")
	.WithReference(database)
	.WaitFor(database);

var api = builder.AddProject<Projects.ChronicleOfHeros_Api>("api")
	.WithReference(database)
	.WithEnvironment("BootstrapOperator__Username", bootstrapOperatorUsername)
	.WithEnvironment("BootstrapOperator__TemporaryPassword", bootstrapOperatorTemporaryPassword)
	.WaitForCompletion(migrations)
	.WithHttpHealthCheck("/health");

builder.AddProject<Projects.ChronicleOfHeros_Web>("web")
	.WithReference(api)
	.WaitFor(api);

builder.Build().Run();