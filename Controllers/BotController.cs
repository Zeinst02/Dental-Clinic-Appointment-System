using ClinicAppointmentSystem.Data;
using ClinicAppointmentSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointmentSystem.Controllers;

public record BotBookingRequest(long TelegramChatId, int DoctorId, DateTime StartTime);
public record BotLinkRequest(long TelegramChatId, string Phone);
public record BotCancelRequest(long TelegramChatId);

// Endpoints consumed only by the n8n bot (service account with role "Bot").
[ApiController]
[Route("api/bot")]
[Authorize(Roles = "Bot")]
public class BotController : ControllerBase
{
    private const int SlotMinutes = 30;
    private readonly ClinicDbContext _context;

    public BotController(ClinicDbContext context)
    {
        _context = context;
    }

    // GET api/bot/patients/123456789
    [HttpGet("patients/{telegramChatId:long}")]
    public async Task<IActionResult> GetPatient(long telegramChatId)
    {
        var patient = await _context.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.User.TelegramChatId == telegramChatId);

        if (patient == null)
            return NotFound(new { error = "patient_not_found" });

        return Ok(new { patientId = patient.Id, name = patient.User.Name });
    }

    // POST api/bot/link
    // Links a Telegram chat to a patient the clinic ALREADY registered (matched by phone number).
    // Unknown numbers are rejected: only patients added by the admin can use the bot.
    [HttpPost("link")]
    public async Task<IActionResult> LinkPatient(BotLinkRequest request)
    {
        var alreadyLinked = await _context.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.User.TelegramChatId == request.TelegramChatId);

        if (alreadyLinked != null)
            return Ok(new { patientId = alreadyLinked.Id, name = alreadyLinked.User.Name });

        var wanted = NormalizePhone(request.Phone);
        if (wanted.Length < 7)
            return BadRequest(new { error = "invalid_phone" });

        // Small clinic: matching in memory lets us normalize formats (0933..., +963933..., spaces).
        var patients = await _context.Patients.Include(p => p.User).ToListAsync();
        var matches = patients.Where(p => NormalizePhone(p.User.Phone) == wanted).ToList();

        if (matches.Count == 0)
            return NotFound(new { error = "not_registered" });

        if (matches.Count > 1)
            return Conflict(new { error = "ambiguous_phone" });

        var patient = matches[0];

        if (patient.User.TelegramChatId != null)
            return Conflict(new { error = "linked_to_another_account" });

        patient.User.TelegramChatId = request.TelegramChatId;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "linked_to_another_account" });
        }

        return Ok(new { patientId = patient.Id, name = patient.User.Name });
    }

    // Digits only, compared by the last 9 digits so "0933 123 456" and "+963933123456" match.
    private static string NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        return digits.Length > 9 ? digits[^9..] : digits;
    }

    // GET api/bot/doctors
    [HttpGet("doctors")]
    public async Task<IActionResult> GetDoctors()
    {
        var doctors = await _context.Doctors
            .Include(d => d.User)
            .Select(d => new { doctorId = d.Id, name = d.User.Name, specialty = d.Specialty })
            .ToListAsync();

        return Ok(doctors);
    }

    // GET api/bot/availability?doctorId=4&date=2026-10-05
    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability(int doctorId, DateOnly date)
    {
        if (!await _context.Doctors.AnyAsync(d => d.Id == doctorId))
            return NotFound(new { error = "doctor_not_found" });

        var (windows, booked) = await LoadScheduleAsync(doctorId, date, date);
        var slots = ComputeSlots(windows, booked, date, DateTime.Now);

        return Ok(new { doctorId, date = date.ToString("yyyy-MM-dd"), slots });
    }

    // GET api/bot/doctors/4/available-days?count=5&horizonDays=30
    // Returns the next days (yyyy-MM-dd) on which the doctor still has a free slot.
    [HttpGet("doctors/{doctorId:int}/available-days")]
    public async Task<IActionResult> GetAvailableDays(int doctorId, int count = 5, int horizonDays = 30)
    {
        if (!await _context.Doctors.AnyAsync(d => d.Id == doctorId))
            return NotFound(new { error = "doctor_not_found" });

        count = Math.Clamp(count, 1, 10);
        horizonDays = Math.Clamp(horizonDays, 1, 60);

        var now = DateTime.Now;
        var from = DateOnly.FromDateTime(now);
        var to = from.AddDays(horizonDays - 1);

        var (windows, booked) = await LoadScheduleAsync(doctorId, from, to);

        var days = new List<string>();
        for (var date = from; date <= to && days.Count < count; date = date.AddDays(1))
        {
            if (ComputeSlots(windows, booked, date, now).Count > 0)
                days.Add(date.ToString("yyyy-MM-dd"));
        }

        return Ok(new { doctorId, days });
    }

    // Loads the doctor's weekly working hours and the booked appointments within [from, to].
    private async Task<(List<DoctorAvailability> Windows, List<(DateTime Start, DateTime End)> Booked)>
        LoadScheduleAsync(int doctorId, DateOnly from, DateOnly to)
    {
        var rangeStart = from.ToDateTime(TimeOnly.MinValue);
        var rangeEnd = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var windows = await _context.DoctorAvailabilities
            .Where(a => a.DoctorId == doctorId)
            .ToListAsync();

        var booked = await _context.Appointments
            .Where(a => a.DoctorId == doctorId
                        && a.Status != AppointmentStatus.Cancelled
                        && a.StartTime >= rangeStart && a.StartTime < rangeEnd)
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        return (windows, booked.Select(b => (b.StartTime, b.EndTime)).ToList());
    }

    // Free 30-minute slots ("HH:mm") for one date, skipping the past and booked times.
    private static List<string> ComputeSlots(
        List<DoctorAvailability> windows,
        List<(DateTime Start, DateTime End)> booked,
        DateOnly date,
        DateTime now)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var slots = new List<string>();

        foreach (var w in windows.Where(w => w.DayOfWeek == date.DayOfWeek))
        {
            for (var t = dayStart + w.StartTime;
                 t.AddMinutes(SlotMinutes) <= dayStart + w.EndTime;
                 t = t.AddMinutes(SlotMinutes))
            {
                var end = t.AddMinutes(SlotMinutes);
                if (t <= now) continue;
                if (booked.Any(b => t < b.End && end > b.Start)) continue;
                slots.Add(t.ToString("HH:mm"));
            }
        }

        return slots;
    }

    // POST api/bot/appointments
    [HttpPost("appointments")]
    public async Task<IActionResult> Book(BotBookingRequest request)
    {
        var patient = await _context.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.User.TelegramChatId == request.TelegramChatId);

        if (patient == null)
            return NotFound(new { error = "patient_not_found" });

        if (!await _context.Doctors.AnyAsync(d => d.Id == request.DoctorId))
            return NotFound(new { error = "doctor_not_found" });

        var start = request.StartTime;

        if (start <= DateTime.Now)
            return BadRequest(new { error = "in_the_past", message = "The appointment must be in the future." });

        if (start.Minute % SlotMinutes != 0 || start.Second != 0)
            return BadRequest(new { error = "invalid_slot", message = $"Appointments start on a {SlotMinutes}-minute grid (e.g. 10:00, 10:30)." });

        var end = start.AddMinutes(SlotMinutes);

        var withinHours = await _context.DoctorAvailabilities.AnyAsync(a =>
            a.DoctorId == request.DoctorId &&
            a.DayOfWeek == start.DayOfWeek &&
            start.TimeOfDay >= a.StartTime &&
            end.TimeOfDay <= a.EndTime);

        if (!withinHours)
            return BadRequest(new { error = "outside_working_hours", message = "The requested time is outside the doctor's working hours." });

        var dayStart = start.Date;
        var dayEnd = dayStart.AddDays(1);

        var hasSameDay = await _context.Appointments.AnyAsync(a =>
            a.PatientId == patient.Id &&
            a.Status != AppointmentStatus.Cancelled &&
            a.StartTime >= dayStart && a.StartTime < dayEnd);

        if (hasSameDay)
            return BadRequest(new { error = "already_booked_that_day", message = "You already have an appointment on this day." });

        var conflict = await _context.Appointments.AnyAsync(a =>
            a.DoctorId == request.DoctorId &&
            a.Status != AppointmentStatus.Cancelled &&
            start < a.EndTime && end > a.StartTime);

        if (conflict)
            return Conflict(new { error = "slot_taken", message = "The doctor is already booked at this time." });

        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = request.DoctorId,
            StartTime = start,
            EndTime = end,
            Status = AppointmentStatus.Confirmed
        };

        _context.Appointments.Add(appointment);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Second line of defence: unique index hit by a concurrent request.
            return Conflict(new { error = "slot_taken", message = "The doctor is already booked at this time." });
        }

        return Ok(new
        {
            appointmentId = appointment.Id,
            doctorId = appointment.DoctorId,
            startTime = appointment.StartTime,
            endTime = appointment.EndTime,
            status = appointment.Status.ToString()
        });
    }

    // GET api/bot/appointments/123456789
    // Upcoming (not cancelled, not completed) appointments of the patient linked to this Telegram chat.
    [HttpGet("appointments/{telegramChatId:long}")]
    public async Task<IActionResult> GetMyAppointments(long telegramChatId)
    {
        var now = DateTime.Now;

        var appointments = await _context.Appointments
            .Where(a => a.Patient.User.TelegramChatId == telegramChatId
                        && a.Status != AppointmentStatus.Cancelled
                        && a.Status != AppointmentStatus.Completed
                        && a.StartTime > now)
            .OrderBy(a => a.StartTime)
            .Select(a => new
            {
                appointmentId = a.Id,
                doctorName = a.Doctor.User.Name,
                specialty = a.Doctor.Specialty,
                startTime = a.StartTime
            })
            .ToListAsync();

        return Ok(new { appointments });
    }

    // PUT api/bot/appointments/15/cancel   body: { "telegramChatId": 123456789 }
    [HttpPut("appointments/{id:int}/cancel")]
    public async Task<IActionResult> CancelAppointment(int id, BotCancelRequest request)
    {
        var appointment = await _context.Appointments
            .Include(a => a.Patient)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        // Same answer for "does not exist" and "belongs to someone else":
        // never reveal another patient's appointments.
        if (appointment == null || appointment.Patient?.User?.TelegramChatId != request.TelegramChatId)
            return NotFound(new { error = "appointment_not_found" });

        if (appointment.Status == AppointmentStatus.Cancelled)
            return BadRequest(new { error = "already_cancelled" });

        if (appointment.Status == AppointmentStatus.Completed || appointment.StartTime <= DateTime.Now)
            return BadRequest(new { error = "cannot_cancel_past" });

        appointment.Status = AppointmentStatus.Cancelled;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            appointmentId = appointment.Id,
            startTime = appointment.StartTime,
            status = appointment.Status.ToString()
        });
    }
}