namespace ClinicAppointmentSystem.Models;

public class Patient
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }
}