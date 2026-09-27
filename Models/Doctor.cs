namespace ClinicAppointmentSystem.Models;

public class Doctor
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Specialty { get; set; }

    public User? User { get; set; }
}