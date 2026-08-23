using Microsoft.AspNetCore.Identity;

namespace ChronicleOfHeros.Api.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; } = true;
}