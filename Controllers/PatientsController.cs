using ClinicAppointmentSystem.Data;
using ClinicAppointmentSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicAppointmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly ClinicDbContext _context;

    public PatientsController(ClinicDbContext context)
    {
        _context = context;
    }

    // GET: api/Patients
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult GetPatients()
    {
        var patients = _context.Patients.ToList();

        return Ok(patients);
    }

    // POST: api/Patients
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult CreatePatient(Patient patient)
    {
        _context.Patients.Add(patient);
        _context.SaveChanges();

        return Ok(patient);
    }
    [Authorize(Roles = "Patient")]
    [HttpGet("me")]
    public IActionResult GetMyPatientProfile()
    {
        var userIdClaim = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier
        )?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var patient = _context.Patients
            .FirstOrDefault(p => p.UserId == userId);

        if (patient == null)
        {
            return NotFound("Patient profile not found.");
        }

        return Ok(new
        {
            patientId = patient.Id,
            userId = patient.UserId
        });
    }
}