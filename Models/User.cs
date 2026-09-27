using System.ComponentModel.DataAnnotations;

namespace ClinicAppointmentSystem.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string Phone { get; set; }

    [Required]
    public string PasswordHash { get; set; }

    public Role Role { get; set; }

    public DateTime CreatedAt { get; set; }
}