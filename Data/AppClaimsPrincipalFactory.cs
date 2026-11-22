using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RepairConnect.Models;
using RepairConnect.Models.Enums;

namespace RepairConnect.Data;

public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser>
{
    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName));

        if ((user.Roles & UserRoles.Client) == UserRoles.Client)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "Client"));
        }

        if ((user.Roles & UserRoles.Technician) == UserRoles.Technician)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "Technician"));
        }

        if ((user.Roles & UserRoles.Admin) == UserRoles.Admin)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
        }

        return identity;
    }
}
