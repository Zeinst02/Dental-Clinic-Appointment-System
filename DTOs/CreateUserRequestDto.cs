using ClinicAppointmentSystem.Models;
using System.ComponentModel.DataAnnotations;

namespace ClinicAppointmentSystem.DTOs;

public class CreateUserRequestDto
{
    [Required]
    public string Name { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string Phone { get; set; }

    [Required]
    public string Password { get; set; }

    public Role Role { get; set; }
}