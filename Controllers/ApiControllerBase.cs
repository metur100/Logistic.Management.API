namespace LogisticsAPI.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected int? CurrentCompanyId =>
        int.TryParse(User.FindFirstValue("companyId"), out var id) ? id : null;

    protected bool IsAdmin => User.IsInRole("Admin");
    protected bool IsManager => User.IsInRole("Manager");
    protected bool IsDriver => User.IsInRole("Driver");
}
