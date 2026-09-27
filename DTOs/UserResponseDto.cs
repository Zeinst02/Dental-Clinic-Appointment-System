using ClinicAppointmentSystem.Models;

namespace ClinicAppointmentSystem.DTOs;

public class UserResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string Email { get; set; }

    public string Phone { get; set; }

    public Role Role { get; set; }

    public DateTime CreatedAt { get; set; }
}