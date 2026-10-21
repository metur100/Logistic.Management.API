using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.DTOs;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string Token,
    string Role,
    int UserId,
    string FullName,
    int? CompanyId,
    string? CompanyName,
    string? Email
);

public record MeResponse(
    int Id,
    string FullName,
    string Username,
    string? Email,
    string Role,
    string? Phone,
    string? LicenseNumber,
    int? CompanyId,
    string? CompanyName,
    string? CompanyStatus
);

public record ChangePasswordDto([Required] string CurrentPassword, [Required, MinLength(8)] string NewPassword);

public record RegisterCompanyDto(
    [Required, MaxLength(160)] string CompanyName,
    [Required, MaxLength(160)] string FullName,
    [Required, EmailAddress, MaxLength(160)] string Email,
    [Required, MaxLength(80)] string Username,
    [Required, MinLength(8), MaxLength(100)] string Password,
    [MaxLength(40)] string? Phone,
    [MaxLength(160)] string? LegalName,
    [MaxLength(40)] string? TaxId,
    [MaxLength(240)] string? Address,
    [MaxLength(80)] string? City,
    [MaxLength(80)] string? Country
);

public record UpdateCompanyProfileDto(
    [Required, MaxLength(160)] string Name,
    [MaxLength(160)] string? LegalName,
    [MaxLength(40)] string? TaxId,
    [EmailAddress, MaxLength(160)] string? Email,
    [MaxLength(40)] string? Phone,
    [MaxLength(240)] string? Address,
    [MaxLength(80)] string? City,
    [MaxLength(80)] string? Country
);

public record InviteDriverDto(
    [Required, MaxLength(160)] string FullName,
    [Required, EmailAddress, MaxLength(160)] string Email,
    [Required, MaxLength(80)] string Username,
    [MaxLength(40)] string? Phone,
    [MaxLength(60)] string? LicenseNumber
);

public record AcceptInviteDto([Required, MinLength(8), MaxLength(100)] string NewPassword);

public record CreateUserDto(string FullName, string Username, string Password, string Role, string? Phone, string? LicenseNumber, string? Email);
public record UpdateUserDto(string FullName, string? Phone, string? LicenseNumber, bool IsActive, string? Email);
public record ResetPasswordDto([Required, MinLength(8)] string NewPassword);

public record CreateVehicleDto(
    [Required, MaxLength(40)] string RegistrationNumber,
    [MaxLength(100)] string? FleetName,
    [MaxLength(60)] string? Type,
    decimal? Capacity,
    [MaxLength(40)] string? OwnershipType,
    [MaxLength(120)] string? OwnerName,
    [MaxLength(40)] string? OwnerPhone
);

public record CreateCargoDto(
    [Required, MaxLength(200)] string Description,
    [MaxLength(160)] string? Consignor,
    [MaxLength(160)] string? Consignee,
    decimal? WeightTons,
    decimal? VolumeCbm,
    [MaxLength(60)] string? CargoType,
    [MaxLength(1000)] string? SpecialInstructions
);

public record CreateTripDto(
    int? DriverId,
    int? VehicleId,
    [Required, MaxLength(200)] string OriginLocation,
    [Required, MaxLength(200)] string DestinationLocation,
    decimal? DistanceKm,
    int? EstimatedDurationMinutes,
    DateTime? PlannedDepartureDate,
    DateTime? ExpectedArrivalDate,
    [MaxLength(2000)] string? Remarks,
    List<int>? CargoIds
);

public record UpdateTripStatusDto(
    [Required] string Status,
    [MaxLength(2000)] string? Remarks,
    DateTime? LoadingArrivalTime,
    DateTime? LoadingEndTime,
    DateTime? UnloadingArrivalTime,
    DateTime? UnloadingEndTime
);

public record PodReceiveDto(
    [MaxLength(60)] string? PodNumber,
    [MaxLength(2000)] string? Remarks,
    DateTime? LoadingArrivalTime,
    DateTime? LoadingEndTime,
    DateTime? UnloadingArrivalTime,
    DateTime? UnloadingEndTime
);

public record UpdateCmrDto([MaxLength(60)] string? CmrNumber);

public record CreateSupportTicketDto(
    [Required, MaxLength(200)] string Subject,
    [Required, MaxLength(2000)] string Description,
    [MaxLength(20)] string? Priority
);

public record UpdateSupportTicketDto(
    [Required, MaxLength(20)] string Status,
    [MaxLength(20)] string? Priority,
    int? AssignedToUserId,
    [MaxLength(2000)] string? Resolution
);

public record SupportTicketDto(
    int Id, string Subject, string Description, string Status, string Priority,
    int CompanyId, string? CompanyName,
    int CreatedByUserId, string? CreatedByName,
    int? AssignedToUserId, string? AssignedToName,
    string? Resolution, DateTime CreatedAt, DateTime UpdatedAt, DateTime? ResolvedAt
);

public record UpdateCompanyStatusDto([Required, MaxLength(20)] string Status);
public record VerifyCompanyDto(bool IsVerified);

public record SmtpSettingDto(
    string Host, int Port, bool EnableSsl, string Username, string FromAddress,
    string FromName, string? ReportsTo, bool HasPassword, DateTime UpdatedAt
);

public record UpdateSmtpSettingDto(
    [Required, MaxLength(160)] string Host,
    [Range(1, 65535)] int Port,
    bool EnableSsl,
    [Required, MaxLength(160)] string Username,
    // Leave empty to keep the existing password.
    string? Password,
    [Required, EmailAddress, MaxLength(160)] string FromAddress,
    [Required, MaxLength(120)] string FromName,
    [EmailAddress, MaxLength(160)] string? ReportsTo
);

public record RouteDistanceDto(
    string Origin,
    string Destination,
    decimal DistanceKm,
    int DurationMinutes,
    string OriginLabel,
    string DestinationLabel,
    double OriginLat,
    double OriginLon,
    double DestinationLat,
    double DestinationLon,
    string Source
);
