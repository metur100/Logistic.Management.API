using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Models;
using LogisticsAPI.Services;

namespace LogisticsAPI.Controllers;

[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ApiControllerBase
{
    private static readonly string[] ActiveStatuses =
    {
        "Assigned", "CargoLoading", "LoadingComplete", "InTransit",
        "NearDestination", "Unloading", "UnloadingComplete"
    };

    private readonly AppDbContext _db;
    private readonly EmailService _email;

    public AdminController(AppDbContext db, EmailService email)
    {
        _db = db; _email = email;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> Overview()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var trendStart = new DateTime(now.AddMonths(-5).Year, now.AddMonths(-5).Month, 1);

        var companiesTotal = await _db.Companies.CountAsync();
        var companiesActive = await _db.Companies.CountAsync(c => c.Status == "Active");
        var companiesVerified = await _db.Companies.CountAsync(c => c.IsVerified);
        var companiesNew = await _db.Companies.CountAsync(c => c.CreatedAt >= now.AddDays(-30));

        var drivers = await _db.Users.CountAsync(u => u.Role == "Driver");
        var managers = await _db.Users.CountAsync(u => u.Role == "Manager");
        var admins = await _db.Users.CountAsync(u => u.Role == "Admin");
        var blockedUsers = await _db.Users.CountAsync(u => !u.IsActive && u.Role != "Admin");

        var vehicles = await _db.Vehicles.CountAsync(v => v.IsActive);
        var cargoItems = await _db.Cargoes.CountAsync();
        var cargoTons = await _db.Cargoes.SumAsync(c => (decimal?)c.WeightTons) ?? 0;

        var tripsTotal = await _db.Trips.CountAsync();
        var tripsActive = await _db.Trips.CountAsync(t => ActiveStatuses.Contains(t.Status));
        var tripsDelivered = await _db.Trips.CountAsync(t => t.Status == "DeliveryCompleted");
        var tripsMonth = await _db.Trips.CountAsync(t => t.ActualArrivalDate >= monthStart && t.Status == "DeliveryCompleted");

        var openTickets = await _db.SupportTickets.CountAsync(t => t.Status == "Open" || t.Status == "InProgress");
        var openIncidents = await _db.Incidents.CountAsync(i => i.Status == "Open");

        var monthly = await _db.Trips
            .Where(t => t.CreatedAt >= trendStart)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new
            {
                monthKey = g.Key.Year * 100 + g.Key.Month,
                total = g.Count(),
                delivered = g.Count(t => t.Status == "DeliveryCompleted")
            })
            .OrderBy(x => x.monthKey)
            .ToListAsync();

        var topCompanies = await _db.Companies
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.City,
                c.Status,
                drivers = c.Users.Count(u => u.Role == "Driver"),
                vehicles = c.Vehicles.Count(v => v.IsActive),
                cargo = c.Cargoes.Count(),
                trips = c.Trips.Count(),
                activeTrips = c.Trips.Count(t => ActiveStatuses.Contains(t.Status))
            })
            .OrderByDescending(c => c.trips)
            .ThenByDescending(c => c.vehicles)
            .Take(6)
            .ToListAsync();

        var recentCompanies = await _db.Companies
            .OrderByDescending(c => c.CreatedAt)
            .Take(6)
            .Select(c => new { c.Id, c.Name, c.City, c.Country, c.Status, c.IsVerified, c.CreatedAt })
            .ToListAsync();

        var recentTickets = await _db.SupportTickets
            .Include(t => t.Company)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .Select(t => new
            {
                t.Id, t.Subject, t.Status, t.Priority, t.CreatedAt,
                companyName = t.Company != null ? t.Company.Name : null
            })
            .ToListAsync();

        return Ok(new
        {
            stats = new
            {
                companiesTotal, companiesActive, companiesVerified, companiesNew,
                drivers, managers, admins, blockedUsers,
                vehicles, cargoItems, cargoTons,
                tripsTotal, tripsActive, tripsDelivered, tripsMonth,
                openTickets, openIncidents
            },
            monthly,
            topCompanies,
            recentCompanies,
            recentTickets
        });
    }

    [HttpGet("companies")]
    public async Task<IActionResult> Companies([FromQuery] string? search, [FromQuery] string? status)
    {
        var q = _db.Companies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c =>
                c.Name.Contains(s) ||
                (c.Email != null && c.Email.Contains(s)) ||
                (c.City != null && c.City.Contains(s)) ||
                (c.TaxId != null && c.TaxId.Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            q = q.Where(c => c.Status == status);

        var list = await q
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id, c.Name, c.LegalName, c.Email, c.Phone, c.City, c.Country,
                c.Status, c.IsVerified, c.CreatedAt,
                managers = c.Users.Count(u => u.Role == "Manager"),
                drivers = c.Users.Count(u => u.Role == "Driver"),
                vehicles = c.Vehicles.Count(v => v.IsActive),
                cargo = c.Cargoes.Count(),
                trips = c.Trips.Count(),
                activeTrips = c.Trips.Count(t => ActiveStatuses.Contains(t.Status)),
                lastActivity = c.Trips.Max(t => (DateTime?)t.UpdatedAt)
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("companies/{id:int}")]
    public async Task<IActionResult> Company(int id)
    {
        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (company == null) return NotFound(new { message = "Company not found." });

        var drivers = await _db.Users
            .Where(u => u.CompanyId == id && u.Role == "Driver")
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id, u.FullName, u.Username, u.Email, u.Phone, u.LicenseNumber,
                u.IsActive, u.LastLoginAt, u.InviteToken,
                status = string.IsNullOrEmpty(u.PasswordHash) ? "Invited" : (u.IsActive ? "Active" : "Blocked"),
                trips = u.Trips.Count()
            })
            .ToListAsync();

        var vehicles = await _db.Vehicles
            .Where(v => v.CompanyId == id)
            .OrderByDescending(v => v.IsActive).ThenBy(v => v.RegistrationNumber)
            .Select(v => new { v.Id, v.RegistrationNumber, v.Type, v.FleetName, v.Capacity, v.OwnershipType, v.IsActive })
            .ToListAsync();

        var recentTrips = await _db.Trips
            .Where(t => t.CompanyId == id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(12)
            .Select(t => new
            {
                t.Id, t.TripNumber, t.OriginLocation, t.DestinationLocation, t.Status,
                t.DistanceKm, t.PlannedDepartureDate, t.ActualArrivalDate, t.CreatedAt,
                driverName = t.Driver != null ? t.Driver.FullName : null,
                vehicleReg = t.Vehicle != null ? t.Vehicle.RegistrationNumber : null,
                cargoCount = t.CargoItems.Count,
                weightTons = t.CargoItems.Sum(c => (decimal?)c.WeightTons) ?? 0
            })
            .ToListAsync();

        var cargo = await _db.Cargoes
            .Where(c => c.CompanyId == id)
            .OrderByDescending(c => c.CreatedAt)
            .Take(12)
            .Select(c => new
            {
                c.Id, c.Description, c.Consignor, c.Consignee, c.WeightTons,
                c.VolumeCbm, c.CargoType, c.CreatedAt, c.TripId,
                tripNumber = c.Trip != null ? c.Trip.TripNumber : null
            })
            .ToListAsync();

        var incidents = await _db.Incidents
            .Where(i => i.CompanyId == id)
            .OrderByDescending(i => i.ReportedAt)
            .Take(6)
            .Select(i => new
            {
                i.Id, i.Type, i.Severity, i.Status, i.Location, i.Description,
                i.ReportedAt, i.ResolvedAt,
                reporter = i.Reporter != null ? i.Reporter.FullName : null,
                tripNumber = i.Trip != null ? i.Trip.TripNumber : null
            })
            .ToListAsync();

        var counts = new
        {
            drivers = drivers.Count,
            driversActive = drivers.Count(d => d.status == "Active"),
            vehicles = vehicles.Count(v => v.IsActive),
            cargo = await _db.Cargoes.CountAsync(c => c.CompanyId == id),
            cargoUnassigned = await _db.Cargoes.CountAsync(c => c.CompanyId == id && c.TripId == null),
            cargoTons = await _db.Cargoes.Where(c => c.CompanyId == id).SumAsync(c => (decimal?)c.WeightTons) ?? 0,
            trips = await _db.Trips.CountAsync(t => t.CompanyId == id),
            tripsActive = await _db.Trips.CountAsync(t => t.CompanyId == id && ActiveStatuses.Contains(t.Status)),
            tripsDelivered = await _db.Trips.CountAsync(t => t.CompanyId == id && t.Status == "DeliveryCompleted"),
            tickets = await _db.SupportTickets.CountAsync(t => t.CompanyId == id),
            openTickets = await _db.SupportTickets.CountAsync(t => t.CompanyId == id && (t.Status == "Open" || t.Status == "InProgress")),
            openIncidents = await _db.Incidents.CountAsync(i => i.CompanyId == id && i.Status == "Open")
        };

        var monthly = await _db.Trips
            .Where(t => t.CompanyId == id && t.CreatedAt >= DateTime.UtcNow.AddMonths(-5))
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new
            {
                monthKey = g.Key.Year * 100 + g.Key.Month,
                total = g.Count(),
                delivered = g.Count(t => t.Status == "DeliveryCompleted")
            })
            .OrderBy(x => x.monthKey)
            .ToListAsync();

        return Ok(new
        {
            company = new
            {
                company.Id, company.Name, company.LegalName, company.TaxId, company.Email,
                company.Phone, company.Address, company.City, company.Country, company.Status,
                company.IsVerified, company.Notes, company.CreatedAt
            },
            counts,
            monthly,
            drivers,
            vehicles,
            recentTrips,
            cargo,
            incidents,
            readOnly = true
        });
    }

    [HttpPatch("companies/{id:int}/status")]
    public async Task<IActionResult> SetCompanyStatus(int id, UpdateCompanyStatusDto dto)
    {
        var status = dto.Status?.Trim() ?? "";
        if (status != "Active" && status != "Suspended")
            return BadRequest(new { message = "Status must be Active or Suspended." });

        var company = await _db.Companies.FindAsync(id);
        if (company == null) return NotFound();

        company.Status = status;
        await _db.SaveChangesAsync();
        return Ok(new { company.Id, company.Status });
    }

    [HttpPatch("companies/{id:int}/verify")]
    public async Task<IActionResult> VerifyCompany(int id, VerifyCompanyDto dto)
    {
        var company = await _db.Companies.FindAsync(id);
        if (company == null) return NotFound();
        company.IsVerified = dto.IsVerified;
        await _db.SaveChangesAsync();
        return Ok(new { company.Id, company.IsVerified });
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] string? search, [FromQuery] string? role, [FromQuery] int? companyId)
    {
        var q = _db.Users.AsNoTracking().Include(u => u.Company).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u => u.FullName.Contains(s) || u.Username.Contains(s) ||
                             (u.Email != null && u.Email.Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(role) && role != "All")
            q = q.Where(u => u.Role == role);
        if (companyId.HasValue)
            q = q.Where(u => u.CompanyId == companyId.Value);

        var list = await q
            .OrderBy(u => u.Role).ThenBy(u => u.FullName)
            .Select(u => new
            {
                u.Id, u.FullName, u.Username, u.Email, u.Phone, u.Role, u.IsActive,
                u.LastLoginAt, u.CreatedAt, u.CompanyId,
                companyName = u.Company != null ? u.Company.Name : null,
                companyStatus = u.Company != null ? u.Company.Status : null,
                invitePending = u.InviteToken != null
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("settings/smtp")]
    public async Task<IActionResult> GetSmtp()
    {
        var s = await _db.SmtpSettings.AsNoTracking().FirstOrDefaultAsync();
        if (s == null) return Ok(null);
        return Ok(new SmtpSettingDto(
            s.Host, s.Port, s.EnableSsl, s.Username, s.FromAddress,
            s.FromName, s.ReportsTo, !string.IsNullOrWhiteSpace(s.Password), s.UpdatedAt));
    }

    [HttpPut("settings/smtp")]
    public async Task<IActionResult> UpdateSmtp(UpdateSmtpSettingDto dto)
    {
        var s = await _db.SmtpSettings.FirstOrDefaultAsync();
        if (s == null)
        {
            if (string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { message = "A password is required when configuring email for the first time." });
            s = new SmtpSetting();
            _db.SmtpSettings.Add(s);
        }

        s.Host = dto.Host.Trim();
        s.Port = dto.Port;
        s.EnableSsl = dto.EnableSsl;
        s.Username = dto.Username.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Password)) s.Password = dto.Password.Trim();
        s.FromAddress = dto.FromAddress.Trim();
        s.FromName = dto.FromName.Trim();
        s.ReportsTo = dto.ReportsTo?.Trim();
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Email settings saved." });
    }

    [HttpPost("settings/smtp/test")]
    public async Task<IActionResult> TestSmtp()
    {
        var s = await _db.SmtpSettings.AsNoTracking().FirstOrDefaultAsync();
        if (s == null) return BadRequest(new { message = "Email is not configured." });
        var to = string.IsNullOrWhiteSpace(s.ReportsTo) ? s.FromAddress : s.ReportsTo;
        var (sent, error) = await _email.SendTestAsync(to);
        if (!sent) return StatusCode(502, new { message = "Test email could not be sent.", error });
        return Ok(new { message = $"Test email sent to {to}.", to });
    }
}


