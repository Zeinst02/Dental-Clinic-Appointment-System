using ClinicAppointmentSystem.Data;
using ClinicAppointmentSystem.DTOs;
using ClinicAppointmentSystem.Models;
using ClinicAppointmentSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicAppointmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ClinicDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly JwtService _jwtService;

    public UsersController(
        ClinicDbContext context,
        PasswordService passwordService,
        JwtService jwtService)
    {
        _context = context;
        _passwordService = passwordService;
        _jwtService = jwtService;
    }

    // POST: api/Users
    // Only Admin can create users
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult CreateUser(CreateUserRequestDto request)
    {
        // Check duplicate email
        if (_context.Users.Any(u => u.Email == request.Email))
        {
            return BadRequest(
                "A user with this email already exists."
            );
        }

        // Check duplicate phone
        if (_context.Users.Any(u => u.Phone == request.Phone))
        {
            return BadRequest(
                "A user with this phone number already exists."
            );
        }

        // Create User
        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Role = request.Role,
            PasswordHash = _passwordService.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.SaveChanges();

        // Return safe response without password hash
        var response = new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };

        return Ok(response);
    }

    // GET: api/Users
    // Only Admin can access all users
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult GetUsers()
    {
        var users = _context.Users
            .Select(user => new UserResponseDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            })
            .ToList();

        return Ok(users);
    }

    // PUT: api/Users/{id}
    // Only Admin can update users
    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public IActionResult UpdateUser(
        int id,
        CreateUserRequestDto request)
    {
        var user = _context.Users.Find(id);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        // Check duplicate email
        if (_context.Users.Any(
            u => u.Email == request.Email && u.Id != id))
        {
            return BadRequest(
                "A user with this email already exists."
            );
        }

        // Check duplicate phone
        if (_context.Users.Any(
            u => u.Phone == request.Phone && u.Id != id))
        {
            return BadRequest(
                "A user with this phone number already exists."
            );
        }

        user.Name = request.Name;
        user.Email = request.Email;
        user.Phone = request.Phone;
        user.Role = request.Role;

        // Update password only if a new password was provided
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash =
                _passwordService.HashPassword(request.Password);
        }

        _context.SaveChanges();

        var response = new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };

        return Ok(response);
    }

    // POST: api/Users/login
    [HttpPost("login")]
    public IActionResult Login(LoginRequestDto request)
    {
        var user = _context.Users
            .FirstOrDefault(u => u.Email == request.Email);

        // Email does not exist
        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        // Check password
        // IMPORTANT:
        // First parameter = stored password hash
        // Second parameter = password entered by user
        if (!_passwordService.VerifyPassword(
            user.PasswordHash,
            request.Password))
        {
            return Unauthorized(new
            {
                message = "Incorrect email or password. Please try again."
            });
        }

        // Generate JWT token
        var token = _jwtService.GenerateToken(user);

        return Ok(new
        {
            id = user.Id,
            name = user.Name,
            email = user.Email,
            phone = user.Phone,
            role = user.Role,
            token = token,
            message = "Login successful."
        });
    }

    // DELETE: api/Users/{id}
    // Only Admin can delete users
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public IActionResult DeleteUser(int id)
    {
        var user = _context.Users.Find(id);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        var hasDoctor = _context.Doctors
            .Any(d => d.UserId == id);

        var hasPatient = _context.Patients
            .Any(p => p.UserId == id);

        if (hasDoctor || hasPatient)
        {
            return BadRequest(
                "Cannot delete a user linked to a doctor or patient."
            );
        }

        _context.Users.Remove(user);
        _context.SaveChanges();

        return Ok("User deleted successfully.");
    }

    // POST: api/Users/telegram/link
    [HttpPost("telegram/link")]
    public IActionResult LinkTelegram(TelegramLinkRequest request)
    {
        var user = _context.Users
            .FirstOrDefault(u =>
                u.Email == request.Email &&
                u.Phone == request.Phone);

        if (user == null)
        {
            return NotFound(
                "No user was found with the provided email and phone number."
            );
        }

        if (user.Role != Role.Patient)
        {
            return BadRequest(
                "Only patient accounts can be linked to Telegram."
            );
        }

        var existingTelegramUser = _context.Users
            .FirstOrDefault(u =>
                u.TelegramChatId == request.TelegramChatId &&
                u.Id != user.Id);

        if (existingTelegramUser != null)
        {
            return BadRequest(
                "This Telegram account is already linked to another user."
            );
        }

        user.TelegramChatId = request.TelegramChatId;

        _context.SaveChanges();

        var patient = _context.Patients
            .FirstOrDefault(p => p.UserId == user.Id);

        if (patient == null)
        {
            return NotFound(
                "The user is not linked to a patient profile."
            );
        }

        return Ok(new
        {
            patientId = patient.Id,
            userId = user.Id,
            name = user.Name,
            message = "Telegram account linked successfully."
        });
    }

}
public class TelegramLinkRequest
{
    public long TelegramChatId { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
}