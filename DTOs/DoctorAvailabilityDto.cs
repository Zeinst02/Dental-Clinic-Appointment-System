namespace ClinicAppointmentSystem.DTOs;

public class DoctorAvailabilityDto
{
    public int Id { get; set; }

    public int DoctorId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }
}