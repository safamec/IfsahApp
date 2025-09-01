using IfsahApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace IfsahApp.Controllers;

[Route("api/adusers")]
public class AdUsersController : Controller
{
    private readonly IAdUserService _ad;

    public AdUsersController(IAdUserService ad)
    {
        _ad = ad;
    }

    // GET /api/adusers/jdoe
    [HttpGet("{userName}")]
    public async Task<IActionResult> GetByUserName(string userName)
    {
        var user = await _ad.GetUserByUserNameAsync(userName);
        if (user is null) return NotFound(new { message = $"User '{userName}' not found." });
        return Ok(user);
    }

    // GET /api/adusers/department/IT
    [HttpGet("department/{department}")]
    public async Task<IActionResult> GetByDepartment(string department)
    {
        var users = await _ad.GetUsersByDepartmentAsync(department);
        return Ok(users);
    }
}
