using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.DTOs;
public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Role, int UserId, string FullName);
public record CreateUserDto(string FullName, string Username, string Password, string Role, string? Phone, string? LicenseNumber);
public record UpdateUserDto(string FullName, string? Phone, string? LicenseNumber, bool IsActive);
public record CreateVehicleDto(string RegistrationNumber, string? FleetName, string? Type, decimal? Capacity, string? OwnershipType, string? OwnerName, string? OwnerPhone);
public record CreateCargoDto(string Description, string? Consignor, string? Consignee, decimal? WeightTons, decimal? VolumeCbm, string? CargoType, string? SpecialInstructions);
public record CreateTripDto(int? DriverId, int? VehicleId, string OriginLocation, string DestinationLocation, decimal? DistanceKm, DateTime? PlannedDepartureDate, DateTime? ExpectedArrivalDate, string? Remarks, List<int>? CargoIds);
public record UpdateTripStatusDto(string Status, string? Remarks, DateTime? LoadingArrivalTime, DateTime? LoadingEndTime, DateTime? UnloadingArrivalTime, DateTime? UnloadingEndTime);
public record PodReceiveDto(string? PodNumber, string? Remarks, DateTime? LoadingArrivalTime, DateTime? LoadingEndTime, DateTime? UnloadingArrivalTime, DateTime? UnloadingEndTime);
public record UpdateCmrDto(string? CmrNumber);
public record ResetPasswordDto([Required, MinLength(6)] string NewPassword);
public record CreateSupportTicketDto([Required, MaxLength(200)] string Subject, [Required, MaxLength(2000)] string Description, string? Priority);
public record UpdateSupportTicketDto(string Status, string? Priority, int? AssignedToUserId, string? Resolution);
public record SupportTicketDto(
    int Id, string Subject, string Description, string Status, string Priority,
    int CreatedByUserId, string? CreatedByName,
    int? AssignedToUserId, string? AssignedToName,
    string? Resolution, DateTime CreatedAt, DateTime UpdatedAt, DateTime? ResolvedAt
);
