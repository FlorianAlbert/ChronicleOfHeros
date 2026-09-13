IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<PostgresDatabaseResource> database = builder.AddPostgres("postgres")
    .AddDatabase("chronicleofheros");
IResourceBuilder<RedisResource> browserSessions = builder.AddRedis("browser-sessions")
    .WithDataVolume();

IResourceBuilder<ParameterResource> bootstrapOperatorUsername = builder.AddParameter("bootstrap-operator-username");
IResourceBuilder<ParameterResource> bootstrapOperatorTemporaryPassword = builder.AddParameter("bootstrap-operator-temporary-password", secret: true);
IResourceBuilder<ParameterResource> jwtSigningPrivateKey = builder.AddParameter("jwt-signing-private-key", secret: true);
IResourceBuilder<ParameterResource> jwtSigningPublicKey = builder.AddParameter("jwt-signing-public-key", secret: true);
IResourceBuilder<ParameterResource> jwtIssuer = builder.AddParameter("jwt-issuer");
IResourceBuilder<ParameterResource> jwtAudience = builder.AddParameter("jwt-audience");

IResourceBuilder<ProjectResource> migrations = builder.AddProject<Projects.ChronicleOfHeros_Migrations>("migrations")
    .WithReference(database)
    .WaitFor(database);

IResourceBuilder<ProjectResource> api = builder.AddProject<Projects.ChronicleOfHeros_Api>("api")
    .WithReference(database)
    .WithEnvironment("BootstrapOperator__Username", bootstrapOperatorUsername)
    .WithEnvironment("BootstrapOperator__TemporaryPassword", bootstrapOperatorTemporaryPassword)
    .WithEnvironment("Jwt__SigningPrivateKey", jwtSigningPrivateKey)
    .WithEnvironment("Jwt__SigningPublicKey", jwtSigningPublicKey)
    .WithEnvironment("Jwt__Issuer", jwtIssuer)
    .WithEnvironment("Jwt__Audience", jwtAudience)
    .WaitForCompletion(migrations)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.ChronicleOfHeros_Web>("web")
    .WithReference(api)
    .WithReference(browserSessions)
    .WithEnvironment("Jwt__SigningPublicKey", jwtSigningPublicKey)
    .WithEnvironment("Jwt__Issuer", jwtIssuer)
    .WithEnvironment("Jwt__Audience", jwtAudience)
    .WaitFor(api)
    .WaitFor(browserSessions);

builder.Build().Run();