using Aspire.Hosting.Dotnet;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<DotnetProjectResource> api = builder.AddDotnetProject("api", "../ChronicleOfHeros.Api/ChronicleOfHeros.Api.csproj")
    .WithHttpHealthCheck("/health");

builder.AddDotnetProject("web", "../ChronicleOfHeros.Web/ChronicleOfHeros.Web.csproj")
    .WithHttpHealthCheck("/health")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();