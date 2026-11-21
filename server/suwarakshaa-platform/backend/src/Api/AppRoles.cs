namespace Api;

public static class AppRoles
{
    public const string Coordinator = "EmergencyCoordinator";
    public const string Staff = "EmergencyCoordinator,CaseManager";
    public const string ShelterOps = "ShelterAdmin,EmergencyCoordinator,CaseManager";
    public const string LogisticsOps = "LogisticsManager,EmergencyCoordinator";
    public const string VolunteerOps = "VolunteerCoordinator,EmergencyCoordinator";
    public const string DonorOps = "DonorOfficer,EmergencyCoordinator,SystemAdmin";
    public const string Admin = "SystemAdmin";
}
