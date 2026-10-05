using ClinicAppointmentSystem.Data;
using ClinicAppointmentSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;
// using ClinicAppointmentSystem.Data;   // <- adjust to the namespace of ClinicDbContext

namespace ClinicAppointmentSystem.Services;

public class BookingService : IBookingService
{
    private const int SlotMinutes = 30;   // assumption: change if your appointments last longer

    private readonly ClinicDbContext _db;

    public BookingService(ClinicDbContext db) => _db = db;

    public Task<bool> IsLinkedAsync(long chatId) =>
        _db.Set<Patient>().AnyAsync(p => p.User!.TelegramChatId == chatId);

    public async Task<bool> LinkByPhoneAsync(long chatId, string phone)
    {
        var incoming = Digits(phone);
        if (incoming.Length < 7) return false;

        var patients = await _db.Set<Patient>().Include(p => p.User).ToListAsync();
        var match = patients.FirstOrDefault(p => p.User != null && SamePhone(Digits(p.User.Phone), incoming));
        if (match?.User is null) return false;

        // Refuse if this account is already linked to a different Telegram chat
        if (match.User.TelegramChatId is not null && match.User.TelegramChatId != chatId) return false;

        match.User.TelegramChatId = chatId;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<(int Id, string Name)>> GetDoctorsAsync()
    {
        var doctors = await _db.Set<Doctor>().Include(d => d.User).ToListAsync();
        return doctors.Select(d => (d.Id, $"{d.User?.Name} - {d.Specialty}")).ToList();
    }

    public async Task<List<TimeOnly>> GetFreeSlotsAsync(int doctorId, DateOnly date)
    {
        var day = date.DayOfWeek;
        var windows = await _db.Set<DoctorAvailability>()
            .Where(a => a.DoctorId == doctorId && a.DayOfWeek == day)
            .ToListAsync();

        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var booked = await _db.Set<Appointment>()
            .Where(a => a.DoctorId == doctorId && a.Status != AppointmentStatus.Cancelled
                        && a.StartTime >= dayStart && a.StartTime < dayEnd)
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        var now = DateTime.Now;
        var slots = new List<TimeOnly>();
        foreach (var w in windows)
        {
            for (var t = w.StartTime; t + TimeSpan.FromMinutes(SlotMinutes) <= w.EndTime; t += TimeSpan.FromMinutes(SlotMinutes))
            {
                var start = dayStart + t;
                var end = start.AddMinutes(SlotMinutes);
                if (start <= now) continue;
                if (booked.Any(b => b.StartTime < end && b.EndTime > start)) continue;
                slots.Add(TimeOnly.FromTimeSpan(t));
            }
        }
        return slots.Distinct().OrderBy(s => s).ToList();
    }

    public async Task<bool> BookAsync(long chatId, int doctorId, DateOnly date, TimeOnly time)
    {
        var patient = await _db.Set<Patient>().Include(p => p.User)
            .FirstOrDefaultAsync(p => p.User!.TelegramChatId == chatId);
        if (patient is null) return false;

        var start = date.ToDateTime(time);
        var end = start.AddMinutes(SlotMinutes);
        if (start <= DateTime.Now) return false;

        // The slot must fall inside the doctor's working hours
        var tod = time.ToTimeSpan();
        var inWindow = await _db.Set<DoctorAvailability>().AnyAsync(a =>
            a.DoctorId == doctorId && a.DayOfWeek == date.DayOfWeek
            && a.StartTime <= tod && a.EndTime >= tod + TimeSpan.FromMinutes(SlotMinutes));
        if (!inWindow) return false;

        // Serializable transaction to prevent double booking under concurrent requests
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var conflict = await _db.Set<Appointment>().AnyAsync(a =>
            a.Status != AppointmentStatus.Cancelled && a.StartTime < end && a.EndTime > start
            && (a.DoctorId == doctorId || a.PatientId == patient.Id));
        if (conflict) return false;

        _db.Set<Appointment>().Add(new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctorId,
            StartTime = start,
            EndTime = end,
            Status = AppointmentStatus.Confirmed
        });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return true;
    }

    private static string Digits(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    // Compare the last 9 digits so "+963 9xx", "09xx" and "9xx" all match
    private static bool SamePhone(string a, string b)
    {
        var n = Math.Min(9, Math.Min(a.Length, b.Length));
        return n >= 7 && a[^n..] == b[^n..];
    }
}
