using ChronicleOfHeros.Identity.Contracts;

using Microsoft.AspNetCore.Identity;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

internal sealed class PlayerIdentityService(UserManager<ApplicationUser> userManager) : IPlayerIdentityService
{
    public async Task<IdentityOperationResult<PlayerIdentityResponse>> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(accountId.ToString()).ConfigureAwait(false);
        return user is null
            ? IdentityOperationResults.NotFound<PlayerIdentityResponse>()
            : IdentityOperationResults.Success(new PlayerIdentityResponse(Guid.Parse(user.Id)));
    }
}