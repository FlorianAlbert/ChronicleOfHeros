using ChronicleOfHeros.Identity.Contracts;

using Microsoft.AspNetCore.Identity;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

// This class gets used by the dependency injection system 
// and may not be directly instantiated.
#pragma warning disable CA1812 // Avoid uninstantiated internal classes

internal sealed class PlayerIdentityService(UserManager<ApplicationUser> userManager) : IPlayerIdentityService
{
    public async Task<IdentityOperationResult<PlayerIdentityResponse>> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(accountId.ToString()).ConfigureAwait(false);
        return user is null
            ? IdentityOperationResults.NotFound<PlayerIdentityResponse>()
            : IdentityOperationResults.Success(new PlayerIdentityResponse(Guid.Parse(user.Id)));
    }
}

#pragma warning restore CA1812 // Avoid uninstantiated internal classes
