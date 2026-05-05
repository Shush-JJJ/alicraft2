using System.ComponentModel.DataAnnotations;

namespace Alicraft2.Models;

// Shared validation rules so the same regex / message is used everywhere.
public static class ValidationPatterns
{
    // Password: minimum 8 chars, at least one uppercase letter, one digit, and one special character.
    public const string Password      = @"^(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$";
    public const string PasswordError = "Password must be at least 8 characters and include an uppercase letter, a number, and a special character.";

    // Philippine mobile number: 09XXXXXXXXX, +639XXXXXXXXX, or 639XXXXXXXXX (digits only).
    public const string Phone         = @"^(09|\+639|639)\d{9}$";
    public const string PhoneError    = "Enter a valid Philippine mobile number (e.g. 09171234567 or +639171234567).";
}

public class RegisterVm
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "Password is required.")]
    [RegularExpression(ValidationPatterns.Password, ErrorMessage = ValidationPatterns.PasswordError)]
    public string Password { get; set; } = string.Empty;
    [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneError)]
    public string Phone { get; set; } = string.Empty;
    [Required] public string Province { get; set; } = string.Empty;
    [Required] public string City { get; set; } = string.Empty;
    public string? Barangay { get; set; }
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? SecurityQuestion { get; set; }
    public string? SecurityAnswer { get; set; }
}

public class LoginVm
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    public bool Remember { get; set; }
}

public class ForgotPasswordVm
{
    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email.")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordVm
{
    [Required] public string Token { get; set; } = string.Empty;
    [Required(ErrorMessage = "New password is required.")]
    [RegularExpression(ValidationPatterns.Password, ErrorMessage = ValidationPatterns.PasswordError)]
    public string NewPassword { get; set; } = string.Empty;
    [Required(ErrorMessage = "Please confirm your new password."), Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class ProfileVm
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneError)]
    public string Phone { get; set; } = string.Empty;
    public string? Province { get; set; }
    public string? City { get; set; }
    public string? Barangay { get; set; }
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public IFormFile? Avatar { get; set; }
}

public class ChangePasswordVm
{
    [Required(ErrorMessage = "Current password is required.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [RegularExpression(ValidationPatterns.Password, ErrorMessage = ValidationPatterns.PasswordError)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your new password."), Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class CheckoutVm
{
    [Required] public string ShippingName { get; set; } = string.Empty;
    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneError)]
    public string ShippingPhone { get; set; } = string.Empty;
    [Required] public string ShippingProvince { get; set; } = string.Empty;
    [Required] public string ShippingCity { get; set; } = string.Empty;
    [Required] public string ShippingBarangay { get; set; } = string.Empty;
    [Required] public string ShippingStreet { get; set; } = string.Empty;
    [Required] public string ShippingPostalCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    [Required] public string PaymentMethod { get; set; } = "GCash"; // GCash | PayMaya | COD
    public string? PaymentReference { get; set; }
    public IFormFile? PaymentProof { get; set; }
}

public class ProductFormVm
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [Required] public string Description { get; set; } = string.Empty;
    [Required] public string Category { get; set; } = "Frame";
    [Range(0, 1_000_000)] public decimal Price { get; set; }
    [Range(0, 100_000)] public int Stock { get; set; }
    public bool IsActive { get; set; } = true;
    public IFormFile? Image { get; set; }
    public string? ExistingImagePath { get; set; }
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
