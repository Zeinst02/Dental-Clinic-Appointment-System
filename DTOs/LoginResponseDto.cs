using ClinicAppointmentSystem.Models;

namespace ClinicAppointmentSystem.DTOs;

public class LoginResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string Email { get; set; }

    public Role Role { get; set; }

    public string Message { get; set; }
}