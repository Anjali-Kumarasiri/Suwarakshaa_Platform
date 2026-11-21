using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Api.Services;

/// Development only: demo users, one active crisis, sample shelters / stock / campaign, so every screen has data.
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        if (!await db.Roles.AnyAsync())
        {
            logger.LogWarning("Roles table is empty. Run db/HumanitarianPlatform_Schema.sql first.");
            return;
        }
        if (await db.Users.AnyAsync()) return;

        var org = new Organisation { Name = "National Disaster Relief Authority", Type = "Government", Country = "LK", ContactEmail = "ops@relief.test" };
        db.Organisations.Add(org);
        await db.SaveChangesAsync();

        var roles = await db.Roles.ToDictionaryAsync(r => r.Name, r => r.RoleId);
        var seed = new[]
        {
            ("admin@relief.test",       "Admin@12345",   "System Admin",          "SystemAdmin"),
            ("coordinator@relief.test", "Coord@12345",   "Emergency Coordinator", "EmergencyCoordinator"),
            ("casemanager@relief.test", "Case@12345",    "Case Manager",          "CaseManager"),
            ("logistics@relief.test",   "Logistics@123", "Logistics Manager",     "LogisticsManager"),
            ("shelter@relief.test",     "Shelter@1234",  "Shelter Admin",         "ShelterAdmin"),
            ("volunteers@relief.test",  "Volunteer@123", "Volunteer Coordinator", "VolunteerCoordinator"),
            ("donations@relief.test",   "Donation@123",  "Donor Officer",         "DonorOfficer"),
            ("citizen@relief.test",     "Citizen@12345", "Test Citizen",          "Citizen"),
        };

        int adminId = 0;
        foreach (var (email, pwd, name, role) in seed)
        {
            var u = new User { Email = email, FullName = name, PasswordHash = BCrypt.Net.BCrypt.HashPassword(pwd) };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            db.UserRoles.Add(new UserRole { UserId = u.UserId, RoleId = roles[role] });
            await db.SaveChangesAsync();
            if (role == "SystemAdmin") adminId = u.UserId;
        }

        var crisis = new CrisisEvent { Name = "Southwest Floods 2026", Type = "Flood", Severity = 4, Status = "Active", CreatedBy = adminId };
        db.CrisisEvents.Add(crisis);
        await db.SaveChangesAsync();

        db.OperationalAreas.AddRange(
            new OperationalArea { CrisisId = crisis.CrisisId, Name = "Colombo District", CentreLat = 6.9271m, CentreLng = 79.8612m, RadiusKm = 20 },
            new OperationalArea { CrisisId = crisis.CrisisId, Name = "Negombo and Gampaha", CentreLat = 7.1500m, CentreLng = 79.9100m, RadiusKm = 25 });
        db.Shelters.AddRange(
            new Shelter { CrisisId = crisis.CrisisId, OrganisationId = org.OrganisationId, Name = "Negombo Community Hall", Address = "Negombo", Latitude = 7.2090m, Longitude = 79.8380m, Capacity = 150, IsPublic = true },
            new Shelter { CrisisId = crisis.CrisisId, OrganisationId = org.OrganisationId, Name = "Colombo Central School", Address = "Colombo 07", Latitude = 6.9147m, Longitude = 79.8612m, Capacity = 300, IsAccessible = true, IsPublic = true },
            new Shelter { CrisisId = crisis.CrisisId, OrganisationId = org.OrganisationId, Name = "Gampaha Temple Hall", Address = "Gampaha", Latitude = 7.0873m, Longitude = 79.9925m, Capacity = 80, IsPublic = false });

        var types = await db.ResourceTypes.ToDictionaryAsync(t => t.Name, t => t.ResourceTypeId);
        if (types.Count > 0)
            db.ResourceInventories.AddRange(
                new ResourceInventory { ResourceTypeId = types["Dry ration pack"], OrganisationId = org.OrganisationId, CrisisId = crisis.CrisisId, LocationName = "Colombo Depot", Latitude = 6.9271m, Longitude = 79.8612m, QuantityTotal = 200, ReorderLevel = 40 },
                new ResourceInventory { ResourceTypeId = types["Drinking water"], OrganisationId = org.OrganisationId, CrisisId = crisis.CrisisId, LocationName = "Negombo Depot", Latitude = 7.2008m, Longitude = 79.8737m, QuantityTotal = 1000, ReorderLevel = 200 },
                new ResourceInventory { ResourceTypeId = types["Medical kit"], OrganisationId = org.OrganisationId, CrisisId = crisis.CrisisId, LocationName = "Colombo Depot", Latitude = 6.9271m, Longitude = 79.8612m, QuantityTotal = 10, ReorderLevel = 3 });

        db.Campaigns.Add(new Campaign { CrisisId = crisis.CrisisId, OrganisationId = org.OrganisationId, Name = "Flood Relief Fund", TargetAmount = 1_000_000m, StartsOn = DateOnly.FromDateTime(DateTime.UtcNow) });
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded demo users, crisis, shelters, stock and a campaign.");
    }
}
