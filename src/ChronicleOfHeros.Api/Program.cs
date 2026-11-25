WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        serviceName: "keycloak",
        realm: "chronicleofheroes",
        options =>
        {
            // The default "https+http://" authority fails the HTTPS metadata check; resolve HTTPS only.
            options.Authority = "https://keycloak/realms/chronicleofheroes";
            options.Audience = "chronicleofheroes-api";
        });
builder.Services.AddAuthorizationBuilder();

WebApplication app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

app.Run();