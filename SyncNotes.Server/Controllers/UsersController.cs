using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SyncNotes.Server.Data;
using SyncNotes.Shared.Models;
using SyncNotes.Shared.Models.ViewModels;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserResponseViewmodel>> Register(UserRegisterViewmodel dto)
    {
        if (await _context.Users.AnyAsync(u => u.Name == dto.Name))
            return BadRequest("Username already taken.");

        var user = new User
        {
            Name = dto.Name,
            Email=dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new UserResponseViewmodel { Id = user.Id, Name = user.Name };
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserResponseViewmodel>> Login(UserLoginViewmodel dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("Ubejd_Kurtishi_131040_Veron_Idrizi_130922_SEEU_Shkup_2025")); // move this to config
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "SyncNotes",
            audience: "SyncNotes",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            token = tokenString,
            user = new UserResponseViewmodel { Id = user.Id, Name = user.Name }
        });
    }
}