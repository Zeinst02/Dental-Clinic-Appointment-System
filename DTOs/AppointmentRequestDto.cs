namespace ClinicAppointmentSystem.DTOs;

public class AppointmentRequestDto
{
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateTime StartTime { get; set; }
}