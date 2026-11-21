using System.Security.Claims;
namespace Api.Extensions;

public static class UserExtensions
{
    private static readonly string[] OpsRoles =
        { "EmergencyCoordinator", "CaseManager", "LogisticsManager", "ShelterAdmin", "VolunteerCoordinator" };

    public static int GetUserId(this ClaimsPrincipal user) => int.Parse(user.FindFirstValue("sub")!);
    /// Operational staff who may read request details (citizens only ever see their own).
    public static bool IsStaff(this ClaimsPrincipal user) => OpsRoles.Any(user.IsInRole);
}
