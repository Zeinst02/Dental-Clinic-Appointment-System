namespace ClinicAppointmentSystem.DTOs;

public class AppointmentDto
{
    public int Id { get; set; }

    public int PatientId { get; set; }
    public string PatientName { get; set; }

    public string PatientEmail { get; set; }
    public string PatientPhone { get; set; }

    public int DoctorId { get; set; }
    public string DoctorName { get; set; }

    public string DoctorSpecialty { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public string Status { get; set; }
}