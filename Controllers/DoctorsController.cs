using ClinicAppointmentSystem.Data;
using ClinicAppointmentSystem.DTOs;
using ClinicAppointmentSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClinicAppointmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoctorsController : ControllerBase
{
    private readonly ClinicDbContext _context;

    public DoctorsController(ClinicDbContext context)
    {
        _context = context;
    }

    // GET: api/Doctors
    [Authorize]
    [HttpGet]
    public IActionResult GetDoctors()
    {
        var doctors = _context.Doctors
            .Select(d => new DoctorDto
            {
                Id = d.Id,
                Name = d.User.Name,
                Email = d.User.Email,
                Phone = d.User.Phone,
                Specialty = d.Specialty
            })
            .ToList();

        return Ok(doctors);
    }
    // GET: api/Doctors/me
    [Authorize(Roles = "Doctor")]
    [HttpGet("me")]
    public IActionResult GetMyDoctorProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized();
        }

        if (!int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized();
        }

        var doctor = _context.Doctors
            .Where(d => d.UserId == userId)
            .Select(d => new DoctorDto
            {
                Id = d.Id,
                Name = d.User.Name,
                Email = d.User.Email,
                Phone = d.User.Phone,
                Specialty = d.Specialty
            })
            .FirstOrDefault();

        if (doctor == null)
        {
            return NotFound("Doctor profile not found.");
        }

        return Ok(doctor);
    }

    // POST: api/Doctors
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult CreateDoctor(CreateDoctorDto request)
    {
        // Check if user exists
        var user = _context.Users.Find(request.UserId);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        // Check if user is already a doctor
        if (user.Role != Role.Doctor)
        {
            return BadRequest("The selected user does not have the Doctor role.");
        }

        // Check if this user already has a doctor profile
        var existingDoctor = _context.Doctors
            .FirstOrDefault(d => d.UserId == request.UserId);

        if (existingDoctor != null)
        {
            return BadRequest("This user already has a doctor profile.");
        }

        var doctor = new Doctor
        {
            UserId = request.UserId,
            Specialty = request.Specialty
        };

        _context.Doctors.Add(doctor);
        _context.SaveChanges();

        return Ok(doctor);
    }
}