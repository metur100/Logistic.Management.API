using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Models;
using LogisticsAPI.Services;

namespace LogisticsAPI.Controllers;

[Route("api/[controller]")]
public class CompaniesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    private readonly EmailService _email;
    private readonly IConfiguration _cfg;

    public CompaniesController(AppDbContext db, JwtService jwt, EmailService email, IConfiguration cfg)
    {
        _db = db; _jwt = jwt; _email = email; _cfg = cfg;
    }

    /// <summary>Public company self-registration. Creates the company and its first manager.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterCompanyDto req)
    {
        var username = req.Username.Trim();
        var email = req.Email.Trim().ToLowerInvariant();
        var companyName = req.CompanyName.Trim();

        if (username.Length < 3)
            return BadRequest(new { message = "Username must have at least 3 characters." });

        if (await _db.Users.AnyAsync(u => u.Username == username))
            return BadRequest(new { message = "That username is already taken." });

        if (await _db.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == email))
            return BadRequest(new { message = "That email address is already registered." });

        if (await _db.Companies.AnyAsync(c => c.Name == companyName))
            return BadRequest(new { message = "A company with that name is already registered." });

        var company = new Company
        {
            Name = companyName,
            LegalName = req.LegalName?.Trim(),
            TaxId = req.TaxId?.Trim(),
            Email = email,
            Phone = req.Phone?.Trim(),
            Address = req.Address?.Trim(),
            City = req.City?.Trim(),
            Country = string.IsNullOrWhiteSpace(req.Country) ? "Bosnia and Herzegovina" : req.Country.Trim(),
            Status = "Active",
            IsVerified = false,
            CreatedAt = DateTime.UtcNow
        };

        var manager = new User
        {
            Company = company,
            FullName = req.FullName.Trim(),
            Username = username,
            Email = email,
            Phone = req.Phone?.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = "Manager",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Companies.Add(company);
        _db.Users.Add(manager);
        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(manager);
        return StatusCode(201, new LoginResponse(
            token, manager.Role, manager.Id, manager.FullName,
            company.Id, company.Name, manager.Email));
    }

    /// <summary>Information shown on the public "accept invitation" screen.</summary>
    [HttpGet("invite/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInvite(string token)
    {
        var user = await _db.Users.Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.InviteToken == token);

        if (user?.Company == null)
            return NotFound(new { message = "This invitation link is not valid." });

        if (user.InviteExpiresAt.HasValue && user.InviteExpiresAt.Value < DateTime.UtcNow)
            return StatusCode(410, new { message = "This invitation link has expired. Ask your company to send a new one." });

        if (!user.IsActive)
            return StatusCode(403, new { message = "This account is disabled." });

        if (user.Company.Status != "Active")
            return StatusCode(403, new { message = "The company account is suspended." });

        return Ok(new
        {
            fullName = user.FullName,
            username = user.Username,
            email = user.Email,
            companyName = user.Company.Name,
            expiresAt = user.InviteExpiresAt
        });
    }

    /// <summary>Public: driver chooses a password using the invite token.</summary>
    [HttpPost("invite/{token}/accept")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvite(string token, AcceptInviteDto req)
    {
        var user = await _db.Users.Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.InviteToken == token);

        if (user?.Company == null)
            return NotFound(new { message = "This invitation link is not valid." });

        if (user.InviteExpiresAt.HasValue && user.InviteExpiresAt.Value < DateTime.UtcNow)
            return StatusCode(410, new { message = "This invitation link has expired. Ask your company to send a new one." });

        if (!user.IsActive)
            return StatusCode(403, new { message = "This account is disabled." });

        if (user.Company.Status != "Active")
            return StatusCode(403, new { message = "The company account is suspended." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.InviteToken = null;
        user.InviteExpiresAt = null;
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var jwt = _jwt.GenerateToken(user);
        return Ok(new LoginResponse(
            jwt, user.Role, user.Id, user.FullName,
            user.CompanyId, user.Company.Name, user.Email));
    }
}

[Route("api/company")]
[Authorize(Roles = "Manager")]
public class CompanyController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;
    private readonly IConfiguration _cfg;

    private static readonly string[] ActiveStatuses =
    {
        "Assigned", "CargoLoading", "LoadingComplete", "InTransit",
        "NearDestination", "Unloading", "UnloadingComplete"
    };

    public CompanyController(AppDbContext db, EmailService email, IConfiguration cfg)
    {
        _db = db; _email = email; _cfg = cfg;
    }

    private int Cid => CurrentCompanyId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetCompany()
    {
        var company = await _db.Companies
            .Where(c => c.Id == Cid)
            .Select(c => new
            {
                c.Id, c.Name, c.LegalName, c.TaxId, c.Email, c.Phone,
                c.Address, c.City, c.Country, c.Status, c.IsVerified, c.CreatedAt,
                Drivers = c.Users.Count(u => u.Role == "Driver"),
                Vehicles = c.Vehicles.Count(v => v.IsActive),
                Cargo = c.Cargoes.Count(),
                Trips = c.Trips.Count()
            })
            .FirstOrDefaultAsync();

        if (company == null) return NotFound(new { message = "Company not found." });
        return Ok(company);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateCompany(UpdateCompanyProfileDto req)
    {
        var company = await _db.Companies.FindAsync(Cid);
        if (company == null) return NotFound();

        company.Name = req.Name.Trim();
        company.LegalName = req.LegalName?.Trim();
        company.TaxId = req.TaxId?.Trim();
        company.Email = req.Email?.Trim();
        company.Phone = req.Phone?.Trim();
        company.Address = req.Address?.Trim();
        company.City = req.City?.Trim();
        company.Country = req.Country?.Trim();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var company = await _db.Companies.FindAsync(Cid);
        if (company == null) return NotFound();

        var trips = await _db.Trips
            .Include(t => t.Driver)
            .Include(t => t.Vehicle)
            .Include(t => t.CargoItems)
            .Where(t => t.CompanyId == Cid)
            .AsNoTracking()
            .ToListAsync();

        var vehicles = await _db.Vehicles.Where(v => v.CompanyId == Cid && v.IsActive).AsNoTracking().ToListAsync();
        var drivers = await _db.Users.Where(u => u.CompanyId == Cid && u.Role == "Driver").AsNoTracking().ToListAsync();
        var cargo = await _db.Cargoes.Where(c => c.CompanyId == Cid).AsNoTracking().ToListAsync();
        var openIncidents = await _db.Incidents.CountAsync(i => i.CompanyId == Cid && i.Status == "Open");
        var unread = await _db.Messages.CountAsync(m => m.CompanyId == Cid && m.ManagerId == CurrentUserId && m.IsFromDriver && !m.IsRead);

        var active = trips.Where(t => ActiveStatuses.Contains(t.Status)).OrderBy(t => t.ExpectedArrivalDate).ToList();
        var busyVehicleIds = active.Where(t => t.VehicleId.HasValue).Select(t => t.VehicleId!.Value).ToHashSet();
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var last6 = DateTime.UtcNow.Date.AddMonths(-5);
        last6 = new DateTime(last6.Year, last6.Month, 1);

        var monthly = trips
            .Where(t => t.CreatedAt >= last6)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new
            {
                monthKey = g.Key.Year * 100 + g.Key.Month,
                total = g.Count(),
                delivered = g.Count(t => t.Status == "DeliveryCompleted")
            }).ToList();

        return Ok(new
        {
            company = new { company.Id, company.Name, company.Status, company.IsVerified },
            stats = new
            {
                tripsTotal = trips.Count,
                activeTrips = active.Count,
                deliveredThisMonth = trips.Count(t => t.Status == "DeliveryCompleted" && t.ActualArrivalDate >= monthStart),
                pending = trips.Count(t => t.Status == "Assigned"),
                vehiclesTotal = vehicles.Count,
                vehiclesAvailable = vehicles.Count(v => !busyVehicleIds.Contains(v.Id)),
                driversTotal = drivers.Count,
                driversActive = drivers.Count(d => d.IsActive),
                driversInvited = drivers.Count(d => string.IsNullOrEmpty(d.PasswordHash)),
                cargoTotal = cargo.Count,
                unassignedCargo = cargo.Count(c => c.TripId == null),
                tonsInTransit = active.SelectMany(t => t.CargoItems).Sum(c => c.WeightTons ?? 0),
                openIncidents,
                unreadMessages = unread
            },
            activeTrips = active.Select(t => new
            {
                t.Id, t.TripNumber, t.OriginLocation, t.DestinationLocation, t.Status,
                t.DistanceKm, t.PlannedDepartureDate, t.ExpectedArrivalDate,
                driverName = t.Driver?.FullName,
                vehicleReg = t.Vehicle?.RegistrationNumber,
                cargoCount = t.CargoItems.Count,
                weightTons = t.CargoItems.Sum(c => c.WeightTons ?? 0)
            }),
            upcoming = trips
                .Where(t => t.Status == "Assigned" && t.PlannedDepartureDate >= DateTime.UtcNow.Date)
                .OrderBy(t => t.PlannedDepartureDate)
                .Take(5)
                .Select(t => new { t.Id, t.TripNumber, t.OriginLocation, t.DestinationLocation, t.PlannedDepartureDate, driverName = t.Driver?.FullName }),
            recent = trips.OrderByDescending(t => t.CreatedAt).Take(6).Select(t => new
            {
                t.Id, t.TripNumber, t.OriginLocation, t.DestinationLocation, t.Status,
                t.DistanceKm, t.CreatedAt, driverName = t.Driver?.FullName, vehicleReg = t.Vehicle?.RegistrationNumber
            }),
            fleet = vehicles.Select(v => new
            {
                v.Id, v.RegistrationNumber, v.Type, v.Capacity,
                onTrip = busyVehicleIds.Contains(v.Id)
            }),
            monthly
        });
    }

    // ── Drivers / team ──────────────────────────────────────────────

    [HttpGet("drivers")]
    public async Task<IActionResult> GetDrivers()
    {
        var list = await _db.Users
            .Where(u => u.CompanyId == Cid && u.Role == "Driver")
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id, u.FullName, u.Username, u.Email, u.Phone, u.LicenseNumber,
                u.IsActive, u.LastLoginAt, u.CreatedAt,
                u.InviteToken, u.InviteExpiresAt,
                status = string.IsNullOrEmpty(u.PasswordHash) ? "Invited" : (u.IsActive ? "Active" : "Blocked"),
                trips = u.Trips.Count(),
                activeTrips = u.Trips.Count(t => ActiveStatuses.Contains(t.Status))
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost("drivers/invite")]
    public async Task<IActionResult> InviteDriver(InviteDriverDto req)
    {
        var company = await _db.Companies.FindAsync(Cid);
        if (company == null) return NotFound();
        if (company.Status != "Active")
            return StatusCode(403, new { message = "Your company is suspended." });

        var username = req.Username.Trim();
        var email = req.Email.Trim().ToLowerInvariant();

        if (username.Length < 3)
            return BadRequest(new { message = "Username must have at least 3 characters." });

        if (await _db.Users.AnyAsync(u => u.Username == username))
            return BadRequest(new { message = "That username is already taken." });

        if (await _db.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == email))
            return BadRequest(new { message = "That email address is already used by another account." });

        var driver = new User
        {
            CompanyId = Cid,
            FullName = req.FullName.Trim(),
            Username = username,
            Email = email,
            Phone = req.Phone?.Trim(),
            LicenseNumber = req.LicenseNumber?.Trim(),
            Role = "Driver",
            PasswordHash = "",
            IsActive = true,
            InviteToken = JwtService.NewInviteToken(),
            InviteExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(driver);
        await _db.SaveChangesAsync();

        var link = InviteLink(driver.InviteToken!);
        var (sent, error) = await _email.SendInviteAsync(
            driver.Email!, driver.FullName, company.Name, driver.Username, link, driver.InviteExpiresAt!.Value);

        return Ok(new
        {
            driver.Id,
            driver.FullName,
            driver.Username,
            driver.Email,
            inviteLink = link,
            expiresAt = driver.InviteExpiresAt,
            emailSent = sent,
            emailError = error
        });
    }

    [HttpPost("drivers/{id:int}/resend-invite")]
    public async Task<IActionResult> ResendInvite(int id)
    {
        var company = await _db.Companies.FindAsync(Cid);
        var driver = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == Cid && u.Role == "Driver");
        if (driver == null) return NotFound(new { message = "Driver not found." });
        if (company == null) return NotFound();
        if (!driver.IsActive)
            return BadRequest(new { message = "Activate the driver account before sending an invitation." });
        if (string.IsNullOrWhiteSpace(driver.Email))
            return BadRequest(new { message = "This driver has no email address." });

        driver.InviteToken = JwtService.NewInviteToken();
        driver.InviteExpiresAt = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync();

        var link = InviteLink(driver.InviteToken);
        var (sent, error) = await _email.SendInviteAsync(
            driver.Email, driver.FullName, company.Name, driver.Username, link, driver.InviteExpiresAt.Value);

        return Ok(new { inviteLink = link, expiresAt = driver.InviteExpiresAt, emailSent = sent, emailError = error });
    }

    [HttpPatch("drivers/{id:int}/block")]
    public async Task<IActionResult> BlockDriver(int id)
    {
        var driver = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == Cid && u.Role == "Driver");
        if (driver == null) return NotFound();
        driver.IsActive = false;
        driver.InviteToken = null;
        driver.InviteExpiresAt = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("drivers/{id:int}/unblock")]
    public async Task<IActionResult> UnblockDriver(int id)
    {
        var driver = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == Cid && u.Role == "Driver");
        if (driver == null) return NotFound();
        driver.IsActive = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("drivers/{id:int}")]
    public async Task<IActionResult> RemoveDriver(int id)
    {
        var driver = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == Cid && u.Role == "Driver");
        if (driver == null) return NotFound();

        var active = await _db.Trips.AnyAsync(t => t.CompanyId == Cid && t.DriverId == id && ActiveStatuses.Contains(t.Status));
        if (active)
            return BadRequest(new { message = "This driver has an active transport. Reassign or complete it first." });

        driver.IsActive = false;
        driver.InviteToken = null;
        driver.InviteExpiresAt = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private string InviteLink(string token)
    {
        var baseUrl = (_cfg["App:PublicUrl"] ?? "https://logistic-management-ui.pages.dev").TrimEnd('/');
        return $"{baseUrl}/accept-invite/{token}";
    }
}
