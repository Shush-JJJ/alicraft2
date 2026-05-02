using System.Security.Claims;
using Alicraft2.Data;
using Alicraft2.Models;
using Microsoft.EntityFrameworkCore;

namespace Alicraft2.Services;

public class CurrentUser
{
    private readonly IHttpContextAccessor _ctx;
    private readonly AppDbContext _db;
    private User? _cached;

    public CurrentUser(IHttpContextAccessor ctx, AppDbContext db)
    {
        _ctx = ctx;
        _db = db;
    }

    public int? Id
    {
        get
        {
            var idStr = _ctx.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idStr, out var id) ? id : null;
        }
    }

    public string? Role => _ctx.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);
    public bool IsAuthenticated => Id != null;
    public bool IsAdmin => Role == "Admin";

    public async Task<User?> GetAsync()
    {
        if (_cached != null) return _cached;
        var id = Id;
        if (id == null) return null;
        _cached = await _db.Users.FindAsync(id);
        return _cached;
    }
}
