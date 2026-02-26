# Logistics API - ASP.NET Core 8

## Setup
1. Update `appsettings.json` with your SQL Server connection string
2. Run migrations:
   ```
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
3. Run the API:
   ```
   dotnet run
   ```
4. Swagger UI: http://localhost:5000/swagger

## Default Admin Login
- Username: `admin`
- Password: `Admin@123`

## Roles
- **Admin**: Full access
- **Manager**: Manage trips, cargo, vehicles, users
- **Driver**: View own trips, update status, request fuel

## Trip Statuses
Assigned → CargoLoading → LoadingComplete → InTransit → NearDestination → Unloading → DeliveryCompleted
