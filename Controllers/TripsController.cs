using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Models;
using LogisticsAPI.Services;

namespace LogisticsAPI.Controllers;

[Route("api/[controller]")]
[Authorize(Roles = "Manager,Driver")]
public class TripsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly RouteService _routes;
    private readonly ImgBBService _imgBB;

    private static readonly string[] ActiveStatuses =
    {
        "Assigned", "CargoLoading", "LoadingComplete", "InTransit",
        "NearDestination", "Unloading", "UnloadingComplete"
    };

    private static readonly Dictionary<string, string[]> AllowedTransitions = new()
    {
        ["Assigned"] = new[] { "CargoLoading", "Cancelled" },
        ["CargoLoading"] = new[] { "LoadingComplete", "Cancelled" },
        ["LoadingComplete"] = new[] { "InTransit", "Cancelled" },
        ["InTransit"] = new[] { "NearDestination", "Unloading", "Cancelled" },
        ["NearDestination"] = new[] { "Unloading", "Cancelled" },
        ["Unloading"] = new[] { "UnloadingComplete", "Cancelled" },
        ["UnloadingComplete"] = new[] { "DeliveryCompleted", "Cancelled" },
        ["DeliveryCompleted"] = Array.Empty<string>(),
        ["Cancelled"] = Array.Empty<string>()
    };

    public TripsController(AppDbContext db, RouteService routes, ImgBBService imgBB)
    {
        _db = db;
        _routes = routes;
        _imgBB = imgBB;
    }

    private int Cid => CurrentCompanyId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] int? driverId, [FromQuery] string? search)
    {
        var q = _db.Trips
            .AsNoTracking()
            .Include(t => t.Driver)
            .Include(t => t.Vehicle)
            .Include(t => t.CargoItems)
            .Where(t => t.CompanyId == Cid);

        if (IsDriver) q = q.Where(t => t.DriverId == CurrentUserId);
        else if (driverId.HasValue) q = q.Where(t => t.DriverId == driverId);

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            q = q.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(t => t.TripNumber.Contains(s) || t.OriginLocation.Contains(s) || t.DestinationLocation.Contains(s));
        }

        var list = await q.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var trip = await _db.Trips
            .AsNoTracking()
            .Include(t => t.Driver)
            .Include(t => t.Vehicle)
            .Include(t => t.CargoItems)
            .Include(t => t.StatusHistory)
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == Cid);

        if (trip == null) return NotFound(new { message = "Transport not found." });
        if (IsDriver && trip.DriverId != CurrentUserId) return Forbid();
        return Ok(trip);
    }

    [HttpPost]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Create(CreateTripDto dto)
    {
        if (dto.CargoIds == null || dto.CargoIds.Count == 0)
            return BadRequest(new { message = "Select at least one cargo item for the transport." });

        if (string.IsNullOrWhiteSpace(dto.OriginLocation) || string.IsNullOrWhiteSpace(dto.DestinationLocation))
            return BadRequest(new { message = "Origin and destination are required." });

        if (!string.IsNullOrWhiteSpace(dto.OriginLocation) &&
            string.Equals(dto.OriginLocation.Trim(), dto.DestinationLocation?.Trim(), StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Origin and destination must be different." });

        var driver = dto.DriverId.HasValue
            ? await _db.Users.FirstOrDefaultAsync(u => u.Id == dto.DriverId && u.CompanyId == Cid && u.Role == "Driver" && u.IsActive)
            : null;
        if (dto.DriverId.HasValue && driver == null)
            return BadRequest(new { message = "The selected driver is not an active driver in your company." });

        var vehicle = dto.VehicleId.HasValue
            ? await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == dto.VehicleId && v.CompanyId == Cid && v.IsActive)
            : null;
        if (dto.VehicleId.HasValue && vehicle == null)
            return BadRequest(new { message = "The selected vehicle is not an active vehicle in your company." });

        var cargoes = await _db.Cargoes
            .Where(c => dto.CargoIds.Contains(c.Id) && c.CompanyId == Cid)
            .ToListAsync();
        if (cargoes.Count != dto.CargoIds.Distinct().Count())
            return BadRequest(new { message = "One or more cargo items do not belong to your company." });
        if (cargoes.Any(c => c.TripId.HasValue))
            return BadRequest(new { message = "One or more cargo items are already assigned to another transport." });

        var trip = new Trip
        {
            CompanyId = Cid,
            TripNumber = await NextTripNumberAsync(),
            DriverId = driver?.Id,
            VehicleId = vehicle?.Id,
            OriginLocation = dto.OriginLocation!.Trim(),
            DestinationLocation = dto.DestinationLocation!.Trim(),
            DistanceKm = dto.DistanceKm,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
            PlannedDepartureDate = dto.PlannedDepartureDate,
            ExpectedArrivalDate = dto.ExpectedArrivalDate,
            Remarks = dto.Remarks?.Trim(),
            Status = "Assigned",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (!trip.DistanceKm.HasValue || trip.DistanceKm <= 0)
        {
            var route = await _routes.CalculateAsync(trip.OriginLocation, trip.DestinationLocation);
            if (route != null)
            {
                trip.DistanceKm = route.DistanceKm;
                trip.EstimatedDurationMinutes ??= route.DurationMinutes;
            }
        }

        if (!trip.ExpectedArrivalDate.HasValue && trip.PlannedDepartureDate.HasValue && trip.EstimatedDurationMinutes.HasValue)
            trip.ExpectedArrivalDate = trip.PlannedDepartureDate.Value.AddMinutes(trip.EstimatedDurationMinutes.Value);

        _db.Trips.Add(trip);
        await _db.SaveChangesAsync();

        foreach (var c in cargoes) c.TripId = trip.Id;
        _db.TripStatusHistories.Add(new TripStatusHistory
        {
            TripId = trip.Id,
            Status = "Assigned",
            Remarks = "Transport created",
            ChangedByUserId = CurrentUserId
        });
        await _db.SaveChangesAsync();

        var created = await _db.Trips
            .AsNoTracking()
            .Include(t => t.Driver).Include(t => t.Vehicle).Include(t => t.CargoItems)
            .FirstAsync(t => t.Id == trip.Id);

        return CreatedAtAction(nameof(Get), new { id = trip.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Update(int id, CreateTripDto dto)
    {
        if (dto.CargoIds == null || dto.CargoIds.Count == 0)
            return BadRequest(new { message = "Select at least one cargo item for the transport." });

        var trip = await _db.Trips.Include(t => t.CargoItems)
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == Cid);
        if (trip == null) return NotFound();

        if (dto.DriverId.HasValue &&
            !await _db.Users.AnyAsync(u => u.Id == dto.DriverId && u.CompanyId == Cid && u.Role == "Driver" && u.IsActive))
            return BadRequest(new { message = "The selected driver is not an active driver in your company." });

        if (dto.VehicleId.HasValue &&
            !await _db.Vehicles.AnyAsync(v => v.Id == dto.VehicleId && v.CompanyId == Cid && v.IsActive))
            return BadRequest(new { message = "The selected vehicle is not an active vehicle in your company." });

        var cargoes = await _db.Cargoes
            .Where(c => dto.CargoIds.Contains(c.Id) && c.CompanyId == Cid)
            .ToListAsync();
        if (cargoes.Count != dto.CargoIds.Distinct().Count())
            return BadRequest(new { message = "One or more cargo items do not belong to your company." });
        if (cargoes.Any(c => c.TripId.HasValue && c.TripId != trip.Id))
            return BadRequest(new { message = "One or more cargo items are already assigned to another transport." });

        var originChanged = !string.Equals(trip.OriginLocation, dto.OriginLocation.Trim(), StringComparison.OrdinalIgnoreCase)
                         || !string.Equals(trip.DestinationLocation, dto.DestinationLocation.Trim(), StringComparison.OrdinalIgnoreCase);

        trip.DriverId = dto.DriverId;
        trip.VehicleId = dto.VehicleId;
        trip.OriginLocation = dto.OriginLocation.Trim();
        trip.DestinationLocation = dto.DestinationLocation.Trim();
        trip.PlannedDepartureDate = dto.PlannedDepartureDate;
        trip.ExpectedArrivalDate = dto.ExpectedArrivalDate;
        trip.EstimatedDurationMinutes = dto.EstimatedDurationMinutes;
        trip.Remarks = dto.Remarks?.Trim();
        trip.UpdatedAt = DateTime.UtcNow;

        if (dto.DistanceKm.HasValue && dto.DistanceKm > 0)
            trip.DistanceKm = dto.DistanceKm;
        else if (originChanged)
        {
            trip.DistanceKm = null;
            var route = await _routes.CalculateAsync(trip.OriginLocation, trip.DestinationLocation);
            if (route != null)
            {
                trip.DistanceKm = route.DistanceKm;
                trip.EstimatedDurationMinutes ??= route.DurationMinutes;
            }
        }

        if (!trip.ExpectedArrivalDate.HasValue && trip.PlannedDepartureDate.HasValue && trip.EstimatedDurationMinutes.HasValue)
            trip.ExpectedArrivalDate = trip.PlannedDepartureDate.Value.AddMinutes(trip.EstimatedDurationMinutes.Value);

        foreach (var old in trip.CargoItems) old.TripId = null;
        foreach (var c in cargoes) c.TripId = trip.Id;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var trip = await _db.Trips.Include(t => t.CargoItems)
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == Cid);
        if (trip == null) return NotFound();

        if (trip.Status == "InTransit" || trip.Status == "NearDestination" || trip.Status == "Unloading")
            return BadRequest(new { message = "A transport in progress cannot be deleted. Cancel it instead." });

        foreach (var c in trip.CargoItems) c.TripId = null;
        trip.Status = "Cancelled";
        trip.UpdatedAt = DateTime.UtcNow;
        _db.TripStatusHistories.Add(new TripStatusHistory
        {
            TripId = trip.Id,
            Status = "Cancelled",
            Remarks = "Transport cancelled by management",
            ChangedByUserId = CurrentUserId
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateTripStatusDto dto)
    {
        var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == Cid);
        if (trip == null) return NotFound(new { message = "Transport not found." });
        if (IsDriver && trip.DriverId != CurrentUserId) return Forbid();

        var status = dto.Status?.Trim() ?? "";
        if (!AllowedTransitions.ContainsKey(status))
            return BadRequest(new { message = "Invalid transport status." });

        if (IsDriver && status == "Cancelled")
            return StatusCode(403, new { message = "Only management can cancel a transport." });

        if (status != "Cancelled" && !AllowedTransitions[trip.Status].Contains(status))
            return BadRequest(new { message = $"A transport cannot move from {trip.Status} to {status}." });

        if (trip.Status == "DeliveryCompleted" || trip.Status == "Cancelled")
            return BadRequest(new { message = "This transport is already closed." });

        var now = DateTime.UtcNow;
        trip.Status = status;
        trip.UpdatedAt = now;

        switch (status)
        {
            case "CargoLoading":
                trip.LoadingArrivalTime ??= dto.LoadingArrivalTime ?? now;
                break;
            case "LoadingComplete":
                trip.LoadingEndTime ??= dto.LoadingEndTime ?? now;
                break;
            case "InTransit":
                trip.ActualDepartureDate ??= now;
                if (!trip.ExpectedArrivalDate.HasValue && trip.EstimatedDurationMinutes.HasValue)
                    trip.ExpectedArrivalDate = now.AddMinutes(trip.EstimatedDurationMinutes.Value);
                break;
            case "Unloading":
                trip.UnloadingArrivalTime ??= dto.UnloadingArrivalTime ?? now;
                break;
            case "UnloadingComplete":
                trip.UnloadingEndTime ??= dto.UnloadingEndTime ?? now;
                break;
            case "DeliveryCompleted":
                trip.ActualArrivalDate ??= now;
                break;
        }

        if (dto.LoadingArrivalTime.HasValue) trip.LoadingArrivalTime = dto.LoadingArrivalTime;
        if (dto.LoadingEndTime.HasValue) trip.LoadingEndTime = dto.LoadingEndTime;
        if (dto.UnloadingArrivalTime.HasValue) trip.UnloadingArrivalTime = dto.UnloadingArrivalTime;
        if (dto.UnloadingEndTime.HasValue) trip.UnloadingEndTime = dto.UnloadingEndTime;

        _db.TripStatusHistories.Add(new TripStatusHistory
        {
            TripId = trip.Id,
            Status = status,
            Remarks = dto.Remarks?.Trim(),
            ChangedAt = now,
            ChangedByUserId = CurrentUserId
        });

        await _db.SaveChangesAsync();
        return Ok(new { trip.Id, trip.Status, trip.DistanceKm, trip.ExpectedArrivalDate });
    }

    /// <summary>Driver/manager starts the route and gets a Google Maps navigation link.</summary>
    [HttpPost("{id:int}/start")]
    public async Task<IActionResult> StartRoute(int id)
    {
        var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == Cid);
        if (trip == null) return NotFound(new { message = "Transport not found." });
        if (IsDriver && trip.DriverId != CurrentUserId) return Forbid();
        if (trip.Status == "DeliveryCompleted" || trip.Status == "Cancelled")
            return BadRequest(new { message = "This transport is already closed." });

        var now = DateTime.UtcNow;
        if (trip.Status != "InTransit" && trip.Status != "NearDestination" && trip.Status != "Unloading" && trip.Status != "UnloadingComplete")
        {
            trip.Status = "InTransit";
            trip.ActualDepartureDate ??= now;
            trip.UpdatedAt = now;
            if (!trip.ExpectedArrivalDate.HasValue && trip.EstimatedDurationMinutes.HasValue)
                trip.ExpectedArrivalDate = now.AddMinutes(trip.EstimatedDurationMinutes.Value);

            _db.TripStatusHistories.Add(new TripStatusHistory
            {
                TripId = trip.Id,
                Status = "InTransit",
                Remarks = "Route started",
                ChangedAt = now,
                ChangedByUserId = CurrentUserId
            });
            await _db.SaveChangesAsync();
        }

        var mapsUrl = MapsUrl(trip.OriginLocation, trip.DestinationLocation);
        return Ok(new { trip.Id, trip.Status, mapsUrl, trip.DistanceKm, trip.ExpectedArrivalDate });
    }

    [HttpPut("{id:int}/pod")]
    public async Task<IActionResult> ReceivePod(int id, PodReceiveDto dto)
    {
        var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == Cid);
        if (trip == null) return NotFound();
        if (IsDriver && trip.DriverId != CurrentUserId) return Forbid();

        trip.PodNumber = dto.PodNumber?.Trim();
        trip.PodReceived = !string.IsNullOrWhiteSpace(dto.PodNumber);
        trip.Remarks = dto.Remarks?.Trim();
        if (dto.LoadingArrivalTime.HasValue) trip.LoadingArrivalTime = dto.LoadingArrivalTime;
        if (dto.LoadingEndTime.HasValue) trip.LoadingEndTime = dto.LoadingEndTime;
        if (dto.UnloadingArrivalTime.HasValue) trip.UnloadingArrivalTime = dto.UnloadingArrivalTime;
        if (dto.UnloadingEndTime.HasValue) trip.UnloadingEndTime = dto.UnloadingEndTime;
        trip.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("my-trips")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> MyTrips([FromQuery] string? status)
    {
        var q = _db.Trips
            .AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.CargoItems)
            .Where(t => t.CompanyId == Cid && t.DriverId == CurrentUserId);

        if (!string.IsNullOrWhiteSpace(status) && status != "All") q = q.Where(t => t.Status == status);

        var trips = await q.OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.TripNumber,
                t.OriginLocation,
                t.DestinationLocation,
                t.Status,
                t.DistanceKm,
                t.PlannedDepartureDate,
                t.ExpectedArrivalDate,
                t.Remarks,
                VehicleRegistration = t.Vehicle != null ? t.Vehicle.RegistrationNumber : null,
                CargoCount = t.CargoItems.Count,
                TotalWeightTons = t.CargoItems.Sum(c => (decimal?)c.WeightTons) ?? 0
            }).ToListAsync();

        return Ok(trips);
    }

    [HttpGet("my-stats")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> MyStats()
    {
        var trips = await _db.Trips
            .Where(t => t.CompanyId == Cid && t.DriverId == CurrentUserId)
            .Select(t => new { t.Status, t.DistanceKm })
            .ToListAsync();

        return Ok(new
        {
            TotalTrips = trips.Count,
            CompletedTrips = trips.Count(t => t.Status == "DeliveryCompleted"),
            InProgressTrips = trips.Count(t => ActiveStatuses.Contains(t.Status)),
            TotalDistanceKm = trips.Sum(t => t.DistanceKm ?? 0)
        });
    }

    [HttpGet("my-active")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> MyActiveTrip()
    {
        var trip = await _db.Trips
            .AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.CargoItems)
            .Where(t => t.CompanyId == Cid && t.DriverId == CurrentUserId && ActiveStatuses.Contains(t.Status))
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.OriginLocation,
                t.DestinationLocation,
                t.Status,
                t.DistanceKm,
                t.PlannedDepartureDate,
                t.ExpectedArrivalDate,
                VehicleRegistration = t.Vehicle != null ? t.Vehicle.RegistrationNumber : null,
                CargoCount = t.CargoItems.Count,
                TotalWeightTons = t.CargoItems.Sum(c => (decimal?)c.WeightTons) ?? 0
            })
            .FirstOrDefaultAsync();

        return Ok(trip);
    }

    [HttpPatch("{id:int}/cmr")]
    public async Task<IActionResult> UpdateCmr(int id, [FromBody] UpdateCmrDto dto)
    {
        var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == Cid);
        if (trip == null) return NotFound();
        if (IsDriver && trip.DriverId != CurrentUserId) return Forbid();

        trip.CmrNumber = dto.CmrNumber?.Trim();
        trip.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string resource = "vehicle")
    {
        var rangeFrom = from ?? DateTime.UtcNow.Date;
        var rangeTo = to ?? DateTime.UtcNow.Date.AddDays(14);

        var q = _db.Trips
            .AsNoTracking()
            .Include(t => t.Driver)
            .Include(t => t.Vehicle)
            .Where(t => t.CompanyId == Cid);

        if (IsDriver) q = q.Where(t => t.DriverId == CurrentUserId);

        q = q.Where(t =>
            (t.PlannedDepartureDate ?? t.CreatedAt) <= rangeTo &&
            (t.ExpectedArrivalDate ?? t.PlannedDepartureDate ?? t.CreatedAt) >= rangeFrom);

        var trips = await q.ToListAsync();

        var result = resource == "driver"
            ? trips.Where(t => t.DriverId.HasValue).Select(t => new
            {
                resourceId = t.DriverId,
                resourceLabel = t.Driver?.FullName,
                tripId = t.Id,
                tripNumber = t.TripNumber,
                start = t.PlannedDepartureDate ?? t.CreatedAt,
                end = t.ExpectedArrivalDate ?? t.PlannedDepartureDate ?? t.CreatedAt,
                originLocation = t.OriginLocation,
                destinationLocation = t.DestinationLocation,
                status = t.Status
            })
            : trips.Where(t => t.VehicleId.HasValue).Select(t => new
            {
                resourceId = t.VehicleId,
                resourceLabel = t.Vehicle?.RegistrationNumber,
                tripId = t.Id,
                tripNumber = t.TripNumber,
                start = t.PlannedDepartureDate ?? t.CreatedAt,
                end = t.ExpectedArrivalDate ?? t.PlannedDepartureDate ?? t.CreatedAt,
                originLocation = t.OriginLocation,
                destinationLocation = t.DestinationLocation,
                status = t.Status
            });

        return Ok(result);
    }

    [HttpPost("{id:int}/documents/{type}")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadDocument(int id, string type, IFormFile? file)
    {
        var kind = NormalizeDocumentType(type);
        if (kind == null)
            return BadRequest(new { message = "Document type must be cmr or delivery-note." });

        var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == Cid);
        if (trip == null) return NotFound(new { message = "Transport not found." });
        if (IsDriver && trip.DriverId != CurrentUserId) return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Select an image to upload." });
        if (file.Length > 8 * 1024 * 1024)
            return BadRequest(new { message = "Image must be smaller than 8 MB." });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { message = "Use a JPG, PNG or WebP image." });

        try
        {
            await using var stream = file.OpenReadStream();
            var upload = await _imgBB.UploadAsync(stream, file.FileName, file.ContentType);
            if (kind == "cmr")
            {
                trip.CmrImageUrl = upload.Url;
                trip.CmrImageThumbUrl = upload.ThumbUrl;
                trip.CmrImageDeleteUrl = upload.DeleteUrl;
            }
            else
            {
                trip.DeliveryNoteImageUrl = upload.Url;
                trip.DeliveryNoteImageThumbUrl = upload.ThumbUrl;
                trip.DeliveryNoteImageDeleteUrl = upload.DeleteUrl;
            }
            trip.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { type = kind, url = upload.Url, thumbUrl = upload.ThumbUrl });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { message = ex.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { message = "Image hosting is currently unavailable." });
        }
    }

    [HttpDelete("{id:int}/documents/{type}")]
    public async Task<IActionResult> RemoveDocument(int id, string type)
    {
        var kind = NormalizeDocumentType(type);
        if (kind == null)
            return BadRequest(new { message = "Document type must be cmr or delivery-note." });

        var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == Cid);
        if (trip == null) return NotFound(new { message = "Transport not found." });
        if (IsDriver && trip.DriverId != CurrentUserId) return Forbid();

        if (kind == "cmr")
        {
            trip.CmrImageUrl = null;
            trip.CmrImageThumbUrl = null;
            trip.CmrImageDeleteUrl = null;
        }
        else
        {
            trip.DeliveryNoteImageUrl = null;
            trip.DeliveryNoteImageThumbUrl = null;
            trip.DeliveryNoteImageDeleteUrl = null;
        }
        trip.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { removed = true });
    }

    private static string? NormalizeDocumentType(string type)
    {
        return type.Trim().ToLowerInvariant() switch
        {
            "cmr" => "cmr",
            "delivery-note" or "deliverynote" or "delivery_note" => "delivery-note",
            _ => null
        };
    }

    private async Task<string> NextTripNumberAsync()
    {
        for (var i = 0; i < 10; i++)
        {
            var number = $"TRP-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}";
            if (!await _db.Trips.AnyAsync(t => t.TripNumber == number)) return number;
        }
        return $"TRP-{DateTime.UtcNow:yyMMddHHmmss}";
    }

    private static string MapsUrl(string origin, string destination)
    {
        return "https://www.google.com/maps/dir/?api=1" +
               $"&origin={Uri.EscapeDataString(origin)}" +
               $"&destination={Uri.EscapeDataString(destination)}" +
               "&travelmode=driving";
    }
}

