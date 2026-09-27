using Microsoft.AspNetCore.Identity;

namespace ClinicAppointmentSystem.Services;

public class PasswordService
{
    private readonly PasswordHasher<object> _hasher = new();

    public string HashPassword(string password)
    {
        return _hasher.HashPassword(null!, password);
    }

    public bool VerifyPassword(
        string hashedPassword,
        string password)
    {
        var result = _hasher.VerifyHashedPassword(
            null!,
            hashedPassword,
            password
        );

        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}