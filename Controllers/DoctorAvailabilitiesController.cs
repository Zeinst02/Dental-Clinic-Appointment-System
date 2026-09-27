using ClinicAppointmentSystem.Data;
using ClinicAppointmentSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClinicAppointmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DoctorAvailabilitiesController : ControllerBase
{
    private readonly ClinicDbContext _context;

    public DoctorAvailabilitiesController(ClinicDbContext context)
    {
        _context = context;
    }

    // GET: api/DoctorAvailabilities
    // Admin only - full schedule table, any doctor
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult GetAvailabilities()
    {
        var availabilities = _context.DoctorAvailabilities.ToList();

        return Ok(availabilities);
    }

    // POST: api/DoctorAvailabilities
    // Admin only - can set availability for any doctor
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult CreateAvailability(DoctorAvailability availability)
    {
        var doctor = _context.Doctors.Find(availability.DoctorId);

        if (doctor == null)
        {
            return NotFound("Doctor not found.");
        }

        if (availability.StartTime >= availability.EndTime)
        {
            return BadRequest(
                "Start time must be earlier than end time.");
        }

        var hasOverlap = _context.DoctorAvailabilities
            .Any(a =>
                a.DoctorId == availability.DoctorId &&
                a.DayOfWeek == availability.DayOfWeek &&
                availability.StartTime < a.EndTime &&
                availability.EndTime > a.StartTime
            );

        if (hasOverlap)
        {
            return BadRequest(
                "This availability overlaps with an existing schedule.");
        }

        _context.DoctorAvailabilities.Add(availability);
        _context.SaveChanges();

        return Ok(availability);
    }

    // GET: api/DoctorAvailabilities/me
    // Doctor - own availability only
    [Authorize(Roles = "Doctor")]
    [HttpGet("me")]
    public IActionResult GetMyAvailabilities()
    {
        var doctor = GetCurrentDoctor();

        if (doctor == null)
        {
            return NotFound("Doctor profile not found.");
        }

        var availabilities = _context.DoctorAvailabilities
            .Where(a => a.DoctorId == doctor.Id)
            .ToList();

        return Ok(availabilities);
    }

    // POST: api/DoctorAvailabilities/me
    // Doctor - add a slot to own schedule (DoctorId is taken from the
    // logged-in doctor, never from the request body)
    [Authorize(Roles = "Doctor")]
    [HttpPost("me")]
    public IActionResult CreateMyAvailability(DoctorAvailability availability)
    {
        var doctor = GetCurrentDoctor();

        if (doctor == null)
        {
            return NotFound("Doctor profile not found.");
        }

        availability.DoctorId = doctor.Id;

        if (availability.StartTime >= availability.EndTime)
        {
            return BadRequest(
                "Start time must be earlier than end time.");
        }

        var hasOverlap = _context.DoctorAvailabilities
            .Any(a =>
                a.DoctorId == doctor.Id &&
                a.DayOfWeek == availability.DayOfWeek &&
                availability.StartTime < a.EndTime &&
                availability.EndTime > a.StartTime
            );

        if (hasOverlap)
        {
            return BadRequest(
                "This availability overlaps with an existing schedule.");
        }

        _context.DoctorAvailabilities.Add(availability);
        _context.SaveChanges();

        return Ok(availability);
    }

    // DELETE: api/DoctorAvailabilities/5
    // Doctor can delete own slot, Admin can delete any
    [Authorize(Roles = "Doctor,Admin")]
    [HttpDelete("{id}")]
    public IActionResult DeleteAvailability(int id)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        var availability = _context.DoctorAvailabilities.Find(id);

        if (availability == null)
        {
            return NotFound("Availability slot not found.");
        }

        if (role == Role.Doctor.ToString())
        {
            var doctor = GetCurrentDoctor();

            if (doctor == null || availability.DoctorId != doctor.Id)
            {
                return Forbid();
            }
        }

        _context.DoctorAvailabilities.Remove(availability);
        _context.SaveChanges();

        return Ok(new { message = "Availability slot removed." });
    }

    // Resolve the logged-in user's Doctor record
    private Doctor? GetCurrentDoctor()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return _context.Doctors.FirstOrDefault(d => d.UserId == userId);
    }
}