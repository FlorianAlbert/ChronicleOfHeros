using ChronicleOfHeros.Api.Data;
using ChronicleOfHeros.Api.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<ChronicleOfHerosDbContext>("chronicleofheros");
var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.ConfigurationSectionName)
    .Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");
var jwtKeyMaterial = JwtKeyMaterial.Create(jwtOptions);
builder.Services.Configure<BootstrapOperatorOptions>(
	builder.Configuration.GetSection(BootstrapOperatorOptions.ConfigurationSectionName));
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(jwtKeyMaterial);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AuthenticationTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.MapInboundClaims = false;
		options.TokenValidationParameters = new TokenValidationParameters
		{
			IssuerSigningKey = jwtKeyMaterial.ValidationKey,
			ValidIssuer = jwtOptions.Issuer,
			ValidAudience = jwtOptions.Audience,
			ValidateAudience = true,
			ValidateIssuer = true,
			ValidateIssuerSigningKey = true,
			ValidateLifetime = true,
			ClockSkew = TimeSpan.Zero,
			NameClaimType = JwtRegisteredClaimNames.Sub,
			RoleClaimType = ClaimTypes.Role,
		};
	});
builder.Services.AddAuthorization(options =>
	{
		options.AddPolicy("Player", policy => policy.RequireRole(ApplicationRoles.Player));
		options.AddPolicy(
			"PasswordChange",
			policy => policy.RequireAssertion(context =>
				context.User.IsInRole(ApplicationRoles.Player)
				|| context.User.HasClaim("scope", "password-change")));
		options.AddPolicy("Operator", policy => policy.RequireRole(ApplicationRoles.Operator));
	});
builder.Services.AddIdentityCore<ApplicationUser>(options =>
	{
		options.Password.RequiredLength = 8;
		options.Password.RequiredUniqueChars = 1;
		options.Password.RequireDigit = true;
		options.Password.RequireLowercase = true;
		options.Password.RequireNonAlphanumeric = true;
		options.Password.RequireUppercase = true;
	})
	.AddRoles<IdentityRole>()
	.AddEntityFrameworkStores<ChronicleOfHerosDbContext>()
	.AddDefaultTokenProviders();

var app = builder.Build();

await app.Services.InitializeBootstrapOperatorAsync(
	app.Lifetime.ApplicationStopping);

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

app.MapPost(
	"/authentication/sign-in",
	async (
		SignInRequest request,
		UserManager<ApplicationUser> userManager,
		AuthenticationTokenService tokenService,
		CancellationToken cancellationToken) =>
	{
		var username = request.Username?.Trim();
		var user = string.IsNullOrWhiteSpace(username)
			? null
			: await userManager.FindByNameAsync(username);
		if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, request.Password ?? string.Empty))
		{
			return Results.Unauthorized();
		}

		if (user.MustChangePassword)
		{
			return Results.Ok(tokenService.CreateRestrictedAccessToken(user));
		}

		var roles = await userManager.GetRolesAsync(user);
		return Results.Ok(await tokenService.CreateNormalTokenPairAsync(user, roles, cancellationToken));
	})
	.AllowAnonymous();

app.MapPost(
	"/authentication/change-password",
	async (
		ChangePasswordRequest request,
		HttpContext context,
		UserManager<ApplicationUser> userManager,
		AuthenticationTokenService tokenService,
		CancellationToken cancellationToken) =>
	{
		var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
		var user = userId is null ? null : await userManager.FindByIdAsync(userId);
		if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, request.CurrentPassword ?? string.Empty))
		{
			return Results.Unauthorized();
		}

		var passwordChange = await userManager.ChangePasswordAsync(
			user,
			request.CurrentPassword ?? string.Empty,
			request.NewPassword ?? string.Empty);
		if (!passwordChange.Succeeded)
		{
			return ToValidationProblem(passwordChange);
		}

		user.MustChangePassword = false;
		var userUpdate = await userManager.UpdateAsync(user);
		if (!userUpdate.Succeeded)
		{
			return ToValidationProblem(userUpdate);
		}

		await tokenService.RevokeAllRefreshSessionsAsync(user.Id, cancellationToken);
		var roles = await userManager.GetRolesAsync(user);
		return Results.Ok(await tokenService.CreateNormalTokenPairAsync(user, roles, cancellationToken));
	})
	.RequireAuthorization("PasswordChange");

app.MapPost(
	"/authentication/refresh",
	async (
		RefreshTokenRequest request,
		AuthenticationTokenService tokenService,
		UserManager<ApplicationUser> userManager,
		CancellationToken cancellationToken) =>
	{
		var tokenPair = await tokenService.RefreshNormalTokenPairAsync(
			request.RefreshToken,
			userManager,
			cancellationToken);
		return tokenPair is null ? Results.Unauthorized() : Results.Ok(tokenPair);
	})
	.AllowAnonymous();

app.MapPost(
	"/authentication/sign-out",
	async (
		RefreshTokenRequest request,
		AuthenticationTokenService tokenService,
		CancellationToken cancellationToken) =>
	{
		await tokenService.RevokeRefreshSessionFamilyForTokenAsync(
			request.RefreshToken,
			cancellationToken);
		return Results.NoContent();
	})
	.AllowAnonymous();

app.MapPost(
	"/players",
	async (
		EnrollPlayerRequest request,
		UserManager<ApplicationUser> userManager,
		CancellationToken cancellationToken) =>
	{
		var username = request.Username?.Trim();
		if (!UsernameValidator.IsValid(username))
		{
			return Results.ValidationProblem(new Dictionary<string, string[]>
			{
				["username"] = ["The username is invalid."],
			});
		}

		var temporaryCredential = CreateTemporaryCredential();
		var user = new ApplicationUser { UserName = username };
		var creation = await userManager.CreateAsync(user, temporaryCredential);
		if (!creation.Succeeded)
		{
			return ToValidationProblem(creation);
		}

		var roleAssignment = await userManager.AddToRoleAsync(user, ApplicationRoles.Player);
		if (!roleAssignment.Succeeded)
		{
			await userManager.DeleteAsync(user);
			return ToValidationProblem(roleAssignment);
		}

		return Results.Created($"/players/{user.Id}", new TemporaryCredentialResponse(temporaryCredential));
	})
	.RequireAuthorization("Operator");

app.MapPost(
	"/players/{username}/reset-password",
	async (
		string username,
		UserManager<ApplicationUser> userManager,
		AuthenticationTokenService tokenService,
		CancellationToken cancellationToken) =>
	{
		var user = await userManager.FindByNameAsync(username.Trim());
		if (user is null)
		{
			return Results.NotFound();
		}

		var temporaryCredential = CreateTemporaryCredential();
		user.MustChangePassword = true;
		var passwordReset = await userManager.ResetPasswordAsync(
			user,
			await userManager.GeneratePasswordResetTokenAsync(user),
			temporaryCredential);
		if (!passwordReset.Succeeded)
		{
			return ToValidationProblem(passwordReset);
		}

		await tokenService.RevokeAllRefreshSessionsAsync(user.Id, cancellationToken);
		return Results.Ok(new TemporaryCredentialResponse(temporaryCredential));
	})
	.RequireAuthorization("Operator");

app.MapGet("/players/me", () => Results.Ok())
	.RequireAuthorization("Player");

app.Run();

static string CreateTemporaryCredential() =>
	$"A{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}!1";

static IResult ToValidationProblem(IdentityResult result) =>
	Results.ValidationProblem(result.Errors.ToDictionary(
		error => error.Code,
		error => new[] { error.Description }));