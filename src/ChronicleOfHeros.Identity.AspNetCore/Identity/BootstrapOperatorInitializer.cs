using ChronicleOfHeros.Identity.AspNetCore.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

internal static class BootstrapOperatorInitializer
{
    extension(IServiceProvider services)
    {
        internal async Task InitializeBootstrapOperatorAsync(CancellationToken cancellationToken)
        {
            await using var scope = services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ChronicleOfHerosDbContext>();
            var operatorExists = await dbContext.UserRoles.AnyAsync(
                userRole => dbContext.Roles.Any(role =>
                    role.Id == userRole.RoleId && role.Name == ApplicationRoles.Operator),
                cancellationToken);

            if (operatorExists)
            {
                return;
            }

            var options = scope.ServiceProvider.GetRequiredService<IOptions<BootstrapOperatorOptions>>().Value;
            var username = options.Username?.Trim();

            if (!IsValidConfiguration(username, options.TemporaryPassword))
            {
                throw new InvalidOperationException(
                    "Bootstrap Operator configuration must contain a valid username and temporary password.");
            }

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await EnsureRoleExistsAsync(roleManager, ApplicationRoles.Player);
            await EnsureRoleExistsAsync(roleManager, ApplicationRoles.Operator);

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = username,
            };
            EnsureSucceeded(await userManager.CreateAsync(user, options.TemporaryPassword!));
            EnsureSucceeded(await userManager.AddToRolesAsync(user, [ApplicationRoles.Player, ApplicationRoles.Operator]));
        }
    }

    private static async Task EnsureRoleExistsAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(roleName)));
        }
    }

    private static bool IsValidConfiguration(string? username, string? temporaryPassword) =>
        UsernameValidator.IsValid(username)
        && temporaryPassword is { Length: >= 8 and <= 128 };

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }
}