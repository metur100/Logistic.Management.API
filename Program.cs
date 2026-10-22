using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using LogisticsAPI.Data;
using LogisticsAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// EventLog can throw when the process lacks access to the Windows event source
// (it takes the whole app down while logging an error). Console is enough for IIS
// and for local development.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtKeys = new JwtKeyProvider();
builder.Services.AddSingleton(jwtKeys);
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<RouteService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHttpClient<ImgBBService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("routing", c => c.Timeout = TimeSpan.FromSeconds(12));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        var j = builder.Configuration.GetSection("Jwt");
        opt.SaveToken = true;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = j["Issuer"],
            ValidateAudience = true, ValidAudience = j["Audience"],
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            IssuerSigningKeyResolver = (_, _, _, _) =>
            {
                var key = jwtKeys.Key;
                return string.IsNullOrWhiteSpace(key)
                    ? Array.Empty<SecurityKey>()
                    : new SecurityKey[] { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)) };
            }
        };
        opt.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var idValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(idValue, out var userId))
                {
                    context.Fail("Invalid token.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var user = await db.Users.AsNoTracking()
                    .Include(u => u.Company)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null || !user.IsActive)
                {
                    context.Fail("Account is disabled.");
                    return;
                }

                if (user.Role != "Admin" && (user.Company == null || user.Company.Status != "Active"))
                    context.Fail("Company account is suspended.");
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LogistiTrack API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();
        await jwtKeys.InitializeAsync(db, app.Logger);
        await DbSeeder.SeedAsync(db, builder.Configuration, app.Logger);

        var imgBbKey = builder.Configuration["ImgBB:ApiKey"] ?? Environment.GetEnvironmentVariable("IMGBB_API_KEY");
        if (!string.IsNullOrWhiteSpace(imgBbKey))
        {
            var settings = await db.PlatformSettings.FirstOrDefaultAsync();
            if (settings != null)
            {
                settings.ImgBBApiKey = imgBbKey.Trim();
                await db.SaveChangesAsync();
            }
        }
    }
    catch (Exception ex)
    {
        // Keep the API up so a transient database problem is visible in logs instead
        // of taking the whole site down. Authentication will fail until it is fixed.
        app.Logger.LogError(ex, "Database startup (migration/seed) failed");
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    name = "LogistiTrack API",
    status = "ok",
    time = DateTime.UtcNow
}));
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));
app.MapControllers();

app.Run();
