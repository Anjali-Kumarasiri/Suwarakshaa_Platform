using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Api.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private const int MaxFailures = 5;
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    public AuthController(AppDbContext db, JwtTokenService jwt) { _db = db; _jwt = jwt; }

    /// An account needs an email, a phone number, or both. People without email can use a phone number only.
    public class RegisterRequest
    {
        [StringLength(256)] public string? Email { get; set; }
        [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = "";
        [Required, StringLength(200, MinimumLength = 2)] public string FullName { get; set; } = "";
        [StringLength(30)] public string? Phone { get; set; }
    }
    public class LoginRequest
    {
        public string? Identifier { get; set; }   // email address or phone number
        public string? Email { get; set; }        // older clients send the email here
        [Required] public string Password { get; set; } = "";
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest r, CancellationToken ct)
    {
        if (!r.Password.Any(char.IsLetter) || !r.Password.Any(char.IsDigit))
            return BadRequest(new { error = "Password must contain letters and digits." });

        var email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim().ToLowerInvariant();
        var hasPhone = !string.IsNullOrWhiteSpace(r.Phone);
        var phone = hasPhone ? PhoneNumber.Normalize(r.Phone) : null;
        if (email is null && !hasPhone) return BadRequest(new { error = "Enter an email address, a phone number, or both." });
        if (email is not null && !EmailPattern.IsMatch(email)) return BadRequest(new { error = "Enter a valid email address." });
        if (hasPhone && phone is null) return BadRequest(new { error = "Enter a valid phone number, for example 077 123 4567." });

        if (email is not null && await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict(new { error = "An account with this email already exists." });
        if (phone is not null && await _db.Users.AnyAsync(u => u.Phone == phone, ct))
            return Conflict(new { error = "An account with this phone number already exists." });

        // The Users table needs an email value. A phone-only account gets an internal placeholder (never shown, never mailed).
        var storedEmail = email ?? $"p{phone!.TrimStart('+')}@phone.invalid";
        var role = await _db.Roles.FirstAsync(x => x.Name == "Citizen", ct);
        var user = new User { Email = storedEmail, FullName = r.FullName.Trim(), Phone = phone, PasswordHash = BCrypt.Net.BCrypt.HashPassword(r.Password) };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        _db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId });
        _db.AuditEvents.Add(new AuditEvent { UserId = user.UserId, Action = "REGISTER", EntityType = "User", EntityId = user.UserId.ToString() });
        await _db.SaveChangesAsync(ct);
        return Created("", new { user.UserId, Email = email, Phone = phone });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest r, CancellationToken ct)
    {
        var id = (r.Identifier ?? r.Email ?? "").Trim();
        if (id.Length == 0) return BadRequest(new { error = "Enter your email or phone number." });

        User? user;
        if (id.Contains('@'))
        {
            var email = id.ToLowerInvariant();
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        }
        else
        {
            var phone = PhoneNumber.Normalize(id);
            user = phone is null ? null : await _db.Users.FirstOrDefaultAsync(u => u.Phone == phone || u.Phone == id, ct);
        }
        const string generic = "Invalid email/phone or password.";   // same message for every failure: no account enumeration

        if (user is null || !user.IsActive) return Unauthorized(new { error = generic });
        if (user.LockoutUntil is { } until && until > DateTime.UtcNow)
            return StatusCode(429, new { error = "Account temporarily locked. Try again later." });

        if (!BCrypt.Net.BCrypt.Verify(r.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailures) { user.LockoutUntil = DateTime.UtcNow.AddMinutes(15); user.FailedLoginCount = 0; }
            _db.AuditEvents.Add(new AuditEvent { UserId = user.UserId, Action = "LOGIN_FAILED", EntityType = "User", EntityId = user.UserId.ToString() });
            await _db.SaveChangesAsync(ct);
            return Unauthorized(new { error = generic });
        }

        user.FailedLoginCount = 0; user.LockoutUntil = null;
        var roles = await (from ur in _db.UserRoles join ro in _db.Roles on ur.RoleId equals ro.RoleId
                           where ur.UserId == user.UserId select ro.Name).ToListAsync(ct);
        _db.AuditEvents.Add(new AuditEvent { UserId = user.UserId, Action = "LOGIN", EntityType = "User", EntityId = user.UserId.ToString() });
        await _db.SaveChangesAsync(ct);

        return Ok(new { token = _jwt.Create(user, roles), role = roles.FirstOrDefault(), fullName = user.FullName });
    }
}
