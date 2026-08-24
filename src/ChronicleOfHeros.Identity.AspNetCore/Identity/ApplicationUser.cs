using Microsoft.AspNetCore.Identity;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

internal sealed class ApplicationUser : IdentityUser
{
    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; } = true;
}