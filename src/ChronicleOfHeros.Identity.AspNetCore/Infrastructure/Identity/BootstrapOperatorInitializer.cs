using ChronicleOfHeros.Identity.AspNetCore.Infrastructure.Data;
using ChronicleOfHeros.Identity.AspNetCore.Domain;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ChronicleOfHeros.Identity.AspNetCore.Infrastructure.Identity;

internal static class BootstrapOperatorInitializer
{
    extension(IServiceProvider services)
    {
        internal async Task InitializeBootstrapOperatorAsync(CancellationToken cancellationToken)
        {
            AsyncServiceScope scope = services.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                ChronicleOfHerosDbContext dbContext = scope.ServiceProvider.GetRequiredService<ChronicleOfHerosDbContext>();
                bool operatorExists = await dbContext.UserRoles.AnyAsync(
                    userRole => dbContext.Roles.Any(role =>
                        role.Id == userRole.RoleId && role.Name == ApplicationRoles.Operator),
                    cancellationToken).ConfigureAwait(false);

                if (operatorExists)
                {
                    return;
                }

                BootstrapOperatorOptions options = scope.ServiceProvider.GetRequiredService<IOptions<BootstrapOperatorOptions>>().Value;
                string? username = options.Username?.Trim();

                if (!IsValidConfiguration(username, options.TemporaryPassword))
                {
                    throw new InvalidOperationException(
                        "Bootstrap Operator configuration must contain a valid username and temporary password.");
                }

                RoleManager<IdentityRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                await EnsureRoleExistsAsync(roleManager, ApplicationRoles.Player).ConfigureAwait(false);
                await EnsureRoleExistsAsync(roleManager, ApplicationRoles.Operator).ConfigureAwait(false);

                UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                ApplicationUser user = new()
                {
                    UserName = username,
                };
                EnsureSucceeded(await userManager.CreateAsync(user, options.TemporaryPassword!).ConfigureAwait(false));
                EnsureSucceeded(await userManager.AddToRolesAsync(user, [ApplicationRoles.Player, ApplicationRoles.Operator]).ConfigureAwait(false));
            }
        }
    }

    private static async Task EnsureRoleExistsAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
        {
            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(roleName)).ConfigureAwait(false));
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