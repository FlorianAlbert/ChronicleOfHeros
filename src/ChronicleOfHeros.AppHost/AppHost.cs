using Aspire.Hosting.Dotnet;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<PostgresDatabaseResource> database = builder.AddPostgres("postgres")
    .AddDatabase("chronicleofheros");

IResourceBuilder<ParameterResource> bootstrapOperatorUsername = builder.AddParameter("bootstrap-operator-username");
IResourceBuilder<ParameterResource> bootstrapOperatorTemporaryPassword = builder.AddParameter("bootstrap-operator-temporary-password", secret: true);
IResourceBuilder<ParameterResource> jwtSigningPrivateKey = builder.AddParameter("jwt-signing-private-key", secret: true);
IResourceBuilder<ParameterResource> jwtIssuer = builder.AddParameter("jwt-issuer");
IResourceBuilder<ParameterResource> jwtAudience = builder.AddParameter("jwt-audience");

IResourceBuilder<DotnetProjectResource> migrations = builder.AddDotnetProject("migrations", "../ChronicleOfHeros.Migrations/ChronicleOfHeros.Migrations.csproj")
    .WithReference(database)
    .WaitFor(database);

IResourceBuilder<DotnetProjectResource> api = builder.AddDotnetProject("api", "../ChronicleOfHeros.Api/ChronicleOfHeros.Api.csproj")
    .WithReference(database)
    .WithEnvironment("BootstrapOperator__Username", bootstrapOperatorUsername)
    .WithEnvironment("BootstrapOperator__TemporaryPassword", bootstrapOperatorTemporaryPassword)
    .WithEnvironment("Jwt__SigningPrivateKey", jwtSigningPrivateKey)
    .WithEnvironment("Jwt__Issuer", jwtIssuer)
    .WithEnvironment("Jwt__Audience", jwtAudience)
    .WaitForCompletion(migrations)
    .WithHttpHealthCheck("/health");

builder.AddDotnetProject("web", "../ChronicleOfHeros.Web/ChronicleOfHeros.Web.csproj")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();