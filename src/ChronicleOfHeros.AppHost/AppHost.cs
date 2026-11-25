using Aspire.Hosting.Dotnet;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ParameterResource> bootstrapUsername = builder.AddParameter("bootstrap-operator-username");
IResourceBuilder<ParameterResource> bootstrapPassword = builder.AddParameter("bootstrap-operator-password", secret: true);

IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("postgres");

IResourceBuilder<PostgresDatabaseResource> keycloakDatabase = postgres.AddDatabase("keycloakdb");
IResourceBuilder<KeycloakResource> keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithRealmImport("./chronicleofheroes-realm.json")
    .WithEnvironment("CHRONICLE_BOOTSTRAP_USERNAME", bootstrapUsername)
    .WithEnvironment("CHRONICLE_BOOTSTRAP_PASSWORD", bootstrapPassword)
    .WithPostgres(keycloakDatabase);

IResourceBuilder<DotnetProjectResource> api = builder.AddDotnetProject("api", "../ChronicleOfHeros.Api/ChronicleOfHeros.Api.csproj")
    .WithHttpHealthCheck("/health")
    .WithReference(keycloak)
    .WaitFor(keycloak);

IResourceBuilder<DotnetProjectResource> web = builder.AddDotnetProject("web", "../ChronicleOfHeros.Web/ChronicleOfHeros.Web.csproj")
    .WithHttpHealthCheck("/health")
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReference(api)
    .WaitFor(api);

keycloak.WithEnvironment("WEB_PUBLIC_ORIGIN", web.GetEndpoint("https").PrimaryBrowserOrigin);

builder.Build().Run();