using ClinicAppointmentSystem.Data;
using ClinicAppointmentSystem.Models;
using ClinicAppointmentSystem.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ClinicAppointmentSystem.DTOs;


namespace ClinicAppointmentSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly ClinicDbContext _context;

    public AppointmentsController(ClinicDbContext context)
    {
        _context = context;
    }

    // GET: api/Appointments
    // Admin -> all appointments
    // Doctor -> own appointments
    // Patient -> own appointments
    [HttpGet]
    public IActionResult GetAppointments()
    {
        var userId = GetCurrentUserId();
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (userId == null)
        {
            return Unauthorized();
        }

        var query = _context.Appointments
            .Include(a => a.Patient)
            .ThenInclude(p => p.User)
            .Include(a => a.Doctor)
            .ThenInclude(d => d.User)
            .AsQueryable();

        if (role == Role.Patient.ToString())
        {
            query = query.Where(a =>
                a.Patient.UserId == userId.Value);
        }
        else if (role == Role.Doctor.ToString())
        {
            query = query.Where(a =>
                a.Doctor.UserId == userId.Value);
        }
        else if (role != Role.Admin.ToString())
        {
            return Forbid();
        }

        var appointments = query
            .Select(a => new AppointmentDto
            {
                Id = a.Id,

                PatientId = a.PatientId,
                PatientName = a.Patient.User.Name,
                PatientEmail = a.Patient.User.Email,
                PatientPhone = a.Patient.User.Phone,

                DoctorId = a.DoctorId,
                DoctorName = a.Doctor.User.Name,
                DoctorSpecialty = a.Doctor.Specialty,

                StartTime = a.StartTime,
                EndTime = a.EndTime,

                Status = a.Status.ToString()
            })
            .ToList();

        return Ok(appointments);
    }

    // POST: api/Appointments/check
    // Patient or Admin can check an appointment
    [Authorize(Roles = "Patient,Admin")]
    [HttpPost("check")]
    public IActionResult CheckAppointment(AppointmentRequestDto request)
    {
        var userId = GetCurrentUserId();
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (userId == null)
        {
            return Unauthorized();
        }

        // Patient can only book for himself
        if (role == Role.Patient.ToString())
        {
            var patient = _context.Patients
                .FirstOrDefault(p => p.UserId == userId.Value);

            if (patient == null)
            {
                return NotFound("Patient profile not found.");
            }

            if (request.PatientId != patient.Id)
            {
                return Forbid();
            }
        }

        // Check if patient exists
        var requestedPatient = _context.Patients
            .Find(request.PatientId);

        if (requestedPatient == null)
        {
            return NotFound("Patient not found.");
        }

        // Check if doctor exists
        var doctor = _context.Doctors
            .Find(request.DoctorId);

        if (doctor == null)
        {
            return NotFound("Doctor not found.");
        }

        // Check if appointment is in the past
        if (request.StartTime <= DateTime.Now)
        {
            return BadRequest(
                "The appointment date and time must be in the future.");
        }

        // Appointment duration is always 30 minutes
        var endTime = request.StartTime.AddMinutes(30);

        // Check doctor's working hours
        var dayOfWeek = request.StartTime.DayOfWeek;
        var startTime = request.StartTime.TimeOfDay;
        var appointmentEndTime = endTime.TimeOfDay;

        var isAvailable = _context.DoctorAvailabilities.Any(a =>
            a.DoctorId == request.DoctorId &&
            a.DayOfWeek == dayOfWeek &&
            startTime >= a.StartTime &&
            appointmentEndTime <= a.EndTime
        );

        if (!isAvailable)
        {
            return BadRequest(
                "The requested time is outside the doctor's working hours."
            );
        }

        // Check for overlapping appointments
        var hasConflict = _context.Appointments.Any(a =>
            a.DoctorId == request.DoctorId &&
            a.Status != AppointmentStatus.Cancelled &&
            request.StartTime < a.EndTime &&
            endTime > a.StartTime
        );

        if (hasConflict)
        {
            return BadRequest(
                "The doctor is already booked at this time."
            );
        }

      

        // Appointment is available
        return Ok(new
        {
            message =
                "The requested time is available. Would you like to confirm this appointment?",

            patientId = request.PatientId,
            doctorId = request.DoctorId,
            startTime = request.StartTime,
            endTime = endTime
        });
    }

    // POST: api/Appointments/confirm
    // Patient or Admin can confirm an appointment
    // POST: api/Appointments/confirm
    // Patient or Admin can confirm an appointment
    [Authorize(Roles = "Patient,Admin")]
    [HttpPost("confirm")]
    public IActionResult ConfirmAppointment(AppointmentRequestDto request)
    {
        var userId = GetCurrentUserId();
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (userId == null)
        {
            return Unauthorized();
        }

        // Patient can only confirm for himself
        if (role == Role.Patient.ToString())
        {
            var patient = _context.Patients
                .FirstOrDefault(p => p.UserId == userId.Value);

            if (patient == null)
            {
                return NotFound("Patient profile not found.");
            }

            if (request.PatientId != patient.Id)
            {
                return Forbid();
            }
        }

        // Check if patient exists
        var patientExists = _context.Patients
            .Find(request.PatientId);

        if (patientExists == null)
        {
            return NotFound("Patient not found.");
        }

        // Check if doctor exists
        var doctor = _context.Doctors
            .Find(request.DoctorId);

        if (doctor == null)
        {
            return NotFound("Doctor not found.");
        }

        // Check if appointment is in the past
        if (request.StartTime <= DateTime.Now)
        {
            return BadRequest(
                "The appointment date and time must be in the future.");
        }

        // Appointment duration is always 30 minutes
        var endTime = request.StartTime.AddMinutes(30);

        // Check doctor's working hours
        var dayOfWeek = request.StartTime.DayOfWeek;
        var startTime = request.StartTime.TimeOfDay;
        var appointmentEndTime = endTime.TimeOfDay;

        var isAvailable = _context.DoctorAvailabilities.Any(a =>
            a.DoctorId == request.DoctorId &&
            a.DayOfWeek == dayOfWeek &&
            startTime >= a.StartTime &&
            appointmentEndTime <= a.EndTime
        );

        if (!isAvailable)
        {
            return BadRequest(
                "The requested time is outside the doctor's working hours."
            );
        }

        // Check for overlapping appointments
        var hasConflict = _context.Appointments.Any(a =>
            a.DoctorId == request.DoctorId &&
            a.Status != AppointmentStatus.Cancelled &&
            request.StartTime < a.EndTime &&
            endTime > a.StartTime
        );

        if (hasConflict)
        {
            return BadRequest(
                "The doctor is already booked at this time."
            );
        }

        // Check if patient already has another appointment on same day
        var appointmentDate = request.StartTime.Date;

        var hasAppointmentSameDay = _context.Appointments.Any(a =>
            a.PatientId == request.PatientId &&
            a.StartTime.Date == appointmentDate &&
            a.Status != AppointmentStatus.Cancelled
        );

        if (hasAppointmentSameDay)
        {
            return BadRequest(
                "You already have another appointment on this day."
            );
        }

        // Create the appointment on the server
        var appointment = new Appointment
        {
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            StartTime = request.StartTime,
            EndTime = endTime,
            Status = AppointmentStatus.Confirmed
        };

        _context.Appointments.Add(appointment);
        _context.SaveChanges();

        return Ok(new
        {
            message = "Your appointment has been confirmed.",
            appointmentId = appointment.Id,
            patientId = appointment.PatientId,
            doctorId = appointment.DoctorId,
            startTime = appointment.StartTime,
            endTime = appointment.EndTime,
            status = appointment.Status.ToString()
        });
    }

    // PUT: api/Appointments/cancel/4
    // Patient can cancel own appointment
    // Admin can cancel any appointment
    [Authorize(Roles = "Patient,Admin")]
    [HttpPut("cancel/{id}")]
    public IActionResult CancelAppointment(int id)
    {
        var userId = GetCurrentUserId();
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (userId == null)
        {
            return Unauthorized();
        }

        var appointment = _context.Appointments
            .Include(a => a.Patient)
            .FirstOrDefault(a => a.Id == id);

        if (appointment == null)
        {
            return NotFound("Appointment not found.");
        }

        // Patient can only cancel own appointment
        if (role == Role.Patient.ToString())
        {
            if (appointment.Patient.UserId != userId.Value)
            {
                return Forbid();
            }
        }

        // Check if already cancelled
        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            return BadRequest(
                "This appointment is already cancelled.");
        }

        // Cancel appointment
        appointment.Status = AppointmentStatus.Cancelled;

        _context.SaveChanges();

        return Ok(new
        {
            message = "The appointment has been cancelled.",
            appointmentId = appointment.Id,
            status = appointment.Status
        });
    }

    // PUT: api/Appointments/complete/1
    // Doctor can complete own appointment
    // Admin can complete any appointment
    [Authorize(Roles = "Doctor,Admin")]
    [HttpPut("complete/{id}")]
    public IActionResult CompleteAppointment(int id)
    {
        var userId = GetCurrentUserId();
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (userId == null)
        {
            return Unauthorized();
        }

        var appointment = _context.Appointments
            .Include(a => a.Doctor)
            .FirstOrDefault(a => a.Id == id);

        if (appointment == null)
        {
            return NotFound("Appointment not found.");
        }

        // Doctor can only complete own appointments
        if (role == Role.Doctor.ToString())
        {
            if (appointment.Doctor.UserId != userId.Value)
            {
                return Forbid();
            }
        }

        // Check if appointment is cancelled
        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            return BadRequest(
                "A cancelled appointment cannot be completed.");
        }

        // Check if already completed
        if (appointment.Status == AppointmentStatus.Completed)
        {
            return BadRequest(
                "This appointment is already completed.");
        }

        // Complete appointment
        appointment.Status = AppointmentStatus.Completed;

        _context.SaveChanges();

        return Ok(new
        {
            message = "The appointment has been completed.",
            appointmentId = appointment.Id,
            status = appointment.Status
        });
    }

    // Get current logged-in user's ID
    private int? GetCurrentUserId()
    {
        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public IActionResult DeleteAppointment(int id)
    {
        var appointment = _context.Appointments
            .FirstOrDefault(a => a.Id == id);

        if (appointment == null)
        {
            return NotFound("Appointment not found.");
        }

        if (appointment.Status != AppointmentStatus.Completed &&
            appointment.Status != AppointmentStatus.Cancelled)
        {
            return BadRequest(
                "Only completed or cancelled appointments can be deleted."
            );
        }

        _context.Appointments.Remove(appointment);
        _context.SaveChanges();

        return Ok("Appointment deleted successfully.");
    }
}