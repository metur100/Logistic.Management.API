using LogisticsAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LogisticsAPI.Data;

/// <summary>
/// Optional, idempotent bootstrap. Credentials are never stored in source code:
/// they are supplied through environment variables on the machine that seeds the database.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration cfg, ILogger log)
    {
        await SeedAdminAsync(db, cfg, log);
        await SeedSmtpAsync(db, log);
        await SeedDemoCompanyAsync(db, log);
    }

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static async Task SeedAdminAsync(AppDbContext db, IConfiguration cfg, ILogger log)
    {
        if (await db.Users.AnyAsync(u => u.Role == "Admin")) return;

        var password = Env("SEED_ADMIN_PASSWORD");
        if (password == null)
        {
            log.LogWarning("No admin account exists. Set SEED_ADMIN_PASSWORD and restart once to create it.");
            return;
        }

        var username = Env("SEED_ADMIN_USERNAME") ?? cfg["Seed:AdminUsername"] ?? "admin";
        var email = Env("SEED_ADMIN_EMAIL") ?? cfg["Seed:AdminEmail"];
        var fullName = Env("SEED_ADMIN_NAME") ?? cfg["Seed:AdminFullName"] ?? "Platform Administrator";

        db.Users.Add(new User
        {
            FullName = fullName,
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        log.LogInformation("Admin account '{Username}' created.", username);
    }

    private static async Task SeedSmtpAsync(AppDbContext db, ILogger log)
    {
        if (await db.SmtpSettings.AnyAsync()) return;

        var user = Env("SMTP_USER");
        var password = Env("SMTP_PASSWORD");
        if (user == null || password == null)
        {
            log.LogInformation("SMTP settings are not configured. Invites will return a shareable link without sending email.");
            return;
        }

        db.SmtpSettings.Add(new SmtpSetting
        {
            Host = Env("SMTP_HOST") ?? "smtp.gmail.com",
            Port = int.TryParse(Env("SMTP_PORT"), out var port) ? port : 587,
            EnableSsl = !string.Equals(Env("SMTP_SSL"), "false", StringComparison.OrdinalIgnoreCase),
            Username = user,
            Password = password,
            FromAddress = Env("SMTP_FROM") ?? user,
            FromName = Env("SMTP_FROM_NAME") ?? "LogistiTrack",
            ReportsTo = Env("SMTP_REPORTS_TO") ?? user,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        log.LogInformation("SMTP settings stored in the database.");
    }

    private static async Task SeedDemoCompanyAsync(AppDbContext db, ILogger log)
    {
        if (!string.Equals(Env("SEED_DEMO"), "true", StringComparison.OrdinalIgnoreCase)) return;
        if (await db.Companies.AnyAsync(c => c.Name == "TransLog d.o.o.")) return;

        var managerPassword = Env("SEED_MANAGER_PASSWORD");
        var driver1Password = Env("SEED_DRIVER1_PASSWORD");
        var driver2Password = Env("SEED_DRIVER2_PASSWORD");
        if (managerPassword == null || driver1Password == null || driver2Password == null)
        {
            log.LogWarning("SEED_DEMO is set but demo passwords are missing; skipping demo data.");
            return;
        }

        var now = DateTime.UtcNow;
        var company = new Company
        {
            Name = "TransLog d.o.o.",
            LegalName = "TransLog Logistics d.o.o.",
            TaxId = "4201987650004",
            Email = "office@translog.ba",
            Phone = "+387 33 555 120",
            Address = "Zmaja od Bosne 12",
            City = "Sarajevo",
            Country = "Bosna i Hercegovina",
            Status = "Active",
            IsVerified = true,
            CreatedAt = now.AddDays(-72)
        };

        var manager = new User
        {
            Company = company,
            FullName = "Selma BegiÄ‡",
            Username = "manager",
            Email = "selma@translog.ba",
            Phone = "+387 61 220 340",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(managerPassword),
            Role = "Manager",
            IsActive = true,
            CreatedAt = now.AddDays(-72),
            LastLoginAt = now.AddHours(-2)
        };

        var driver1 = new User
        {
            Company = company,
            FullName = "Amar HodÅ¾iÄ‡",
            Username = "driver1",
            Email = "amar@translog.ba",
            Phone = "+387 62 118 220",
            LicenseNumber = "B-884211",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(driver1Password),
            Role = "Driver",
            IsActive = true,
            CreatedAt = now.AddDays(-68),
            LastLoginAt = now.AddHours(-3)
        };

        var driver2 = new User
        {
            Company = company,
            FullName = "Lejla KariÄ‡",
            Username = "driver2",
            Email = "lejla@translog.ba",
            Phone = "+387 63 440 187",
            LicenseNumber = "B-991034",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(driver2Password),
            Role = "Driver",
            IsActive = true,
            CreatedAt = now.AddDays(-54),
            LastLoginAt = now.AddHours(-5)
        };

        db.Companies.Add(company);
        db.Users.AddRange(manager, driver1, driver2);
        await db.SaveChangesAsync();

        var v1 = new Vehicle
        {
            CompanyId = company.Id, RegistrationNumber = "A12-M-345", FleetName = "Sarajevo 01",
            Type = "Truck", Capacity = 24, OwnershipType = "Own", OwnerName = "TransLog d.o.o.",
            OwnerPhone = "+387 33 555 120", CreatedAt = now.AddDays(-70)
        };
        var v2 = new Vehicle
        {
            CompanyId = company.Id, RegistrationNumber = "A45-J-789", FleetName = "Sarajevo 02",
            Type = "Truck", Capacity = 18, OwnershipType = "Own", OwnerName = "TransLog d.o.o.",
            OwnerPhone = "+387 33 555 120", CreatedAt = now.AddDays(-64)
        };
        var v3 = new Vehicle
        {
            CompanyId = company.Id, RegistrationNumber = "J11-K-222", FleetName = "HladnjaÄa 01",
            Type = "Refrigerated", Capacity = 12, OwnershipType = "Leased", OwnerName = "Frigo Leasing",
            OwnerPhone = "+387 33 777 210", CreatedAt = now.AddDays(-40)
        };
        db.Vehicles.AddRange(v1, v2, v3);

        var c1 = Cargo("Palete potroÅ¡aÄke elektronike", "Tehnomax d.o.o.", "Media Market Sarajevo", 8.5m, 32, "General", "PaÅ¾ljivo rukovati. Dvije palete moraju ostati uspravne.");
        var c2 = Cargo("Kancelarijska oprema", "Kika Bosnia", "Hotel Europa", 4.2m, 18, "General", null);
        var c3 = Cargo("Medicinski materijal", "MedicPro d.o.o.", "KCUS Central Pharmacy", 2.8m, 9, "General", "Dostaviti na recepciju apoteke prije 16:00.");
        var c4 = Cargo("Palete smrznute hrane", "Ledo d.o.o.", "Konzum Sarajevo", 6.7m, 24, "Perishable", "ÄŒuvati na -18 Â°C. Ne prekidati hladni lanac.");
        var c5 = Cargo("GraÄ‘evinski ÄeliÄni namotaji", "ArcelorMittal Zenica", "Gradnja d.o.o. Tuzla", 18m, 14, "Oversized", "Osigurati remenjem na svakih 1,5 m.");
        var c6 = Cargo("AmbalaÅ¾ni materijal", "DS Smith", "Bingo Export", 3.4m, 22, "General", null);
        var c7 = Cargo("Palete piÄ‡a", "Coca-Cola HBC", "Amko Komerc", 11.6m, 28, "General", null);
        foreach (var c in new[] { c1, c2, c3, c4, c5, c6, c7 }) c.CompanyId = company.Id;
        db.Cargoes.AddRange(c1, c2, c3, c4, c5, c6, c7);
        await db.SaveChangesAsync();

        var t1 = new Trip
        {
            CompanyId = company.Id, TripNumber = "TRP-260101-4101", DriverId = driver1.Id, VehicleId = v1.Id,
            OriginLocation = "Sarajevo, Bosna i Hercegovina", DestinationLocation = "Zenica, Bosna i Hercegovina",
            DistanceKm = 71, EstimatedDurationMinutes = 65, Status = "Assigned",
            PlannedDepartureDate = now.Date.AddDays(1).AddHours(8),
            ExpectedArrivalDate = now.Date.AddDays(1).AddHours(9).AddMinutes(5),
            Remarks = "Utovarna rampa 3. Nazovite primatelja 30 minuta prije dolaska.",
            CreatedAt = now.AddHours(-26), UpdatedAt = now.AddHours(-26)
        };
        var t2 = new Trip
        {
            CompanyId = company.Id, TripNumber = "TRP-260101-4102", DriverId = driver2.Id, VehicleId = v2.Id,
            OriginLocation = "Sarajevo, Bosna i Hercegovina", DestinationLocation = "Banja Luka, Bosna i Hercegovina",
            DistanceKm = 195, EstimatedDurationMinutes = 175, Status = "CargoLoading",
            PlannedDepartureDate = now.Date.AddHours(14), ExpectedArrivalDate = now.Date.AddHours(17),
            LoadingArrivalTime = now.AddMinutes(-35),
            CreatedAt = now.AddHours(-7), UpdatedAt = now.AddMinutes(-35)
        };
        var t3 = new Trip
        {
            CompanyId = company.Id, TripNumber = "TRP-260101-4103", DriverId = driver1.Id, VehicleId = v3.Id,
            OriginLocation = "Sarajevo, Bosna i Hercegovina", DestinationLocation = "Mostar, Bosna i Hercegovina",
            DistanceKm = 130, EstimatedDurationMinutes = 125, Status = "InTransit",
            PlannedDepartureDate = now.Date.AddHours(6), ExpectedArrivalDate = now.AddHours(2),
            ActualDepartureDate = now.AddHours(-2),
            LoadingArrivalTime = now.AddHours(-3), LoadingEndTime = now.AddHours(-2).AddMinutes(-10),
            CreatedAt = now.AddHours(-9), UpdatedAt = now.AddHours(-2)
        };
        var t4 = new Trip
        {
            CompanyId = company.Id, TripNumber = "TRP-251231-4104", DriverId = driver2.Id, VehicleId = v1.Id,
            OriginLocation = "Sarajevo, Bosna i Hercegovina", DestinationLocation = "Tuzla, Bosna i Hercegovina",
            DistanceKm = 118, EstimatedDurationMinutes = 110, Status = "DeliveryCompleted",
            PlannedDepartureDate = now.Date.AddDays(-1).AddHours(7),
            ExpectedArrivalDate = now.Date.AddDays(-1).AddHours(9),
            ActualDepartureDate = now.Date.AddDays(-1).AddHours(7).AddMinutes(5),
            ActualArrivalDate = now.Date.AddDays(-1).AddHours(8).AddMinutes(55),
            LoadingArrivalTime = now.Date.AddDays(-1).AddHours(6).AddMinutes(20),
            LoadingEndTime = now.Date.AddDays(-1).AddHours(6).AddMinutes(55),
            UnloadingArrivalTime = now.Date.AddDays(-1).AddHours(8).AddMinutes(50),
            UnloadingEndTime = now.Date.AddDays(-1).AddHours(9).AddMinutes(20),
            PodNumber = "POD-88412", PodReceived = true, CmrNumber = "CMR-2026-00412",
            CreatedAt = now.AddDays(-2), UpdatedAt = now.Date.AddDays(-1).AddHours(9).AddMinutes(20)
        };
        var t5 = new Trip
        {
            CompanyId = company.Id, TripNumber = "TRP-251229-4105", DriverId = driver1.Id, VehicleId = v2.Id,
            OriginLocation = "Sarajevo, Bosna i Hercegovina", DestinationLocation = "BihaÄ‡, Bosna i Hercegovina",
            DistanceKm = 312, EstimatedDurationMinutes = 290, Status = "DeliveryCompleted",
            PlannedDepartureDate = now.Date.AddDays(-5).AddHours(5),
            ExpectedArrivalDate = now.Date.AddDays(-5).AddHours(10),
            ActualDepartureDate = now.Date.AddDays(-5).AddHours(5).AddMinutes(12),
            ActualArrivalDate = now.Date.AddDays(-5).AddHours(10).AddMinutes(8),
            PodNumber = "POD-88390", PodReceived = true,
            CreatedAt = now.AddDays(-6), UpdatedAt = now.Date.AddDays(-5).AddHours(10).AddMinutes(8)
        };

        t1.CargoItems.Add(c1);
        t2.CargoItems.Add(c2);
        t2.CargoItems.Add(c3);
        t3.CargoItems.Add(c4);
        t4.CargoItems.Add(c5);
        t5.CargoItems.Add(c7);

        db.Trips.AddRange(t1, t2, t3, t4, t5);
        await db.SaveChangesAsync();

        History(t1, manager.Id, "Assigned", t1.CreatedAt, "Transport kreiran");
        History(t2, manager.Id, "Assigned", t2.CreatedAt, "Transport kreiran");
        History(t2, manager.Id, "CargoLoading", t2.LoadingArrivalTime!.Value, "Utovar zapoÄet");
        History(t3, manager.Id, "Assigned", t3.CreatedAt, "Transport kreiran");
        History(t3, manager.Id, "CargoLoading", t3.LoadingArrivalTime!.Value, "Utovar zapoÄet");
        History(t3, driver1.Id, "LoadingComplete", t3.LoadingEndTime!.Value, "Teret osiguran");
        History(t3, driver1.Id, "InTransit", t3.ActualDepartureDate!.Value, "Ruta zapoÄeta");
        History(t4, manager.Id, "Assigned", t4.CreatedAt, "Transport kreiran");
        History(t4, manager.Id, "CargoLoading", t4.LoadingArrivalTime!.Value, "Utovar zapoÄet");
        History(t4, driver2.Id, "LoadingComplete", t4.LoadingEndTime!.Value, "Teret osiguran");
        History(t4, driver2.Id, "InTransit", t4.ActualDepartureDate!.Value, "Ruta zapoÄeta");
        History(t4, driver2.Id, "Unloading", t4.UnloadingArrivalTime!.Value, "Stiglo do primatelja");
        History(t4, driver2.Id, "UnloadingComplete", t4.UnloadingEndTime!.Value, "Istovar zavrÅ¡en");
        History(t4, driver2.Id, "DeliveryCompleted", t4.ActualArrivalDate!.Value, "POD potpisan");
        History(t5, manager.Id, "Assigned", t5.CreatedAt, "Transport kreiran");
        History(t5, driver1.Id, "InTransit", t5.ActualDepartureDate!.Value, "Ruta zapoÄeta");
        History(t5, driver1.Id, "DeliveryCompleted", t5.ActualArrivalDate!.Value, "Dostavljeno");

        db.Incidents.AddRange(
            new Incident
            {
                CompanyId = company.Id, TripId = t3.Id, Type = "Breakdown", Severity = "High", Status = "Open",
                Description = "Pojavilo se upozorenje na zadnji zraÄni ovjes na A1 kod Konjica. Vozilo se kreÄ‡e polako do sljedeÄ‡eg izlaza.",
                Location = "Autoput A1, kod Konjica", ReportedByUserId = driver1.Id, ReportedAt = now.AddMinutes(-42)
            },
            new Incident
            {
                CompanyId = company.Id, TripId = t4.Id, Type = "Delay", Severity = "Low", Status = "Resolved",
                Description = "KaÅ¡njenje od 30 minuta na utovarnoj rampi zbog nedostajuÄ‡eg carinskog dokumenta.",
                Location = "Utovarni terminal Sarajevo", ReportedByUserId = driver2.Id,
                ReportedAt = now.Date.AddDays(-1).AddHours(6), ResolvedAt = now.Date.AddDays(-1).AddHours(6).AddMinutes(25),
                Resolution = "Dokument je elektronski poslan od dispeÄera; utovar je nastavljen."
            });

        db.Messages.AddRange(
            Msg(company.Id, driver1.Id, manager.Id, manager.Id, false, now.AddHours(-4), "Dobro jutro Amar. Tvoj teret za Mostar je spreman na rampi 2.", true),
            Msg(company.Id, driver1.Id, manager.Id, driver1.Id, true, now.AddHours(-3).AddMinutes(-50), "Na putu sam, dolazim za 10 minuta.", true),
            Msg(company.Id, driver1.Id, manager.Id, manager.Id, false, now.AddHours(-2).AddMinutes(-20), "Hvala. PoÅ¡alji broj CMR-a kad kreneÅ¡.", true),
            Msg(company.Id, driver1.Id, manager.Id, driver1.Id, true, now.AddMinutes(-38), "CMR je CMR-2026-00418. Postoji upozorenje na ovjes, prijavio sam incident.", false),
            Msg(company.Id, driver2.Id, manager.Id, manager.Id, false, now.AddHours(-1), "Lejla, primatelj u Banjoj Luci traÅ¾i odgodu od 30 minuta.", true),
            Msg(company.Id, driver2.Id, manager.Id, driver2.Id, true, now.AddMinutes(-25), "Razumijem, krenut Ä‡u u 14:30.", false)
        );

        db.SupportTickets.AddRange(
            new SupportTicket
            {
                CompanyId = company.Id, Subject = "Dodaj joÅ¡ dva vozaÄka raÄuna",
                Description = "SljedeÄ‡e sedmice uvodimo dva sezonska vozaÄa. Postoji li ograniÄenje broja vozaÄkih raÄuna na naÅ¡em planu?",
                Status = "Open", Priority = "Medium", CreatedByUserId = manager.Id,
                CreatedAt = now.AddHours(-20), UpdatedAt = now.AddHours(-20)
            },
            new SupportTicket
            {
                CompanyId = company.Id, Subject = "Aplikacija vozaÄa prikazuje staro vozilo",
                Description = "Nakon dodjele drugog vozila, aplikacija vozaÄa je joÅ¡ nekoliko minuta prikazivala prethodnu registraciju.",
                Status = "Resolved", Priority = "Low", CreatedByUserId = driver1.Id,
                Resolution = "KeÅ¡ je osvjeÅ¾en povlaÄenjem naniÅ¾e; pratimo situaciju.",
                CreatedAt = now.AddDays(-4), UpdatedAt = now.AddDays(-3), ResolvedAt = now.AddDays(-3)
            });

        await db.SaveChangesAsync();
        log.LogInformation("Demo company, drivers, fleet and transports seeded.");
    }

    private static Cargo Cargo(string description, string consignor, string consignee, decimal tons, decimal volume, string type, string? instructions)
        => new()
        {
            Description = description,
            Consignor = consignor,
            Consignee = consignee,
            WeightTons = tons,
            VolumeCbm = volume,
            CargoType = type,
            SpecialInstructions = instructions,
            CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(2, 20))
        };

    private static void History(Trip trip, int userId, string status, DateTime at, string? remarks)
        => trip.StatusHistory.Add(new TripStatusHistory
        {
            Status = status,
            ChangedAt = at,
            Remarks = remarks,
            ChangedByUserId = userId
        });

    private static Message Msg(int companyId, int driverId, int managerId, int senderId, bool fromDriver, DateTime at, string content, bool read)
        => new()
        {
            CompanyId = companyId,
            DriverId = driverId,
            ManagerId = managerId,
            SenderId = senderId,
            IsFromDriver = fromDriver,
            CreatedAt = at,
            Content = content,
            IsRead = read
        };
}

