using LogMyDay.Api.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace LogMyDay.Api.Application.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive setting values using ASP.NET Core Data Protection API.
/// </summary>
public sealed class SettingProtector : ISettingProtector
{
    private readonly IDataProtector _protector;

    public SettingProtector(IDataProtectionProvider dataProtectionProvider)
    {
        // Create a protector with a specific purpose for settings encryption
        // This ensures encryption keys are isolated from other uses of Data Protection
        _protector = dataProtectionProvider.CreateProtector("LogMyDay.Settings.SensitiveValues");
    }

    public string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return plainText;
        }

        return _protector.Protect(plainText);
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return protectedText;
        }

        return _protector.Unprotect(protectedText);
    }
}
