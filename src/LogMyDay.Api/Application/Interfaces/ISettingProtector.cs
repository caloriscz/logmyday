namespace LogMyDay.Api.Application.Interfaces;

/// <summary>
/// Service for encrypting and decrypting sensitive setting values (e.g., API keys).
/// </summary>
public interface ISettingProtector
{
    /// <summary>
    /// Encrypts a plain-text value.
    /// </summary>
    /// <param name="plainText">The plain-text value to encrypt.</param>
    /// <returns>The encrypted value.</returns>
    string Protect(string plainText);

    /// <summary>
    /// Decrypts an encrypted value.
    /// </summary>
    /// <param name="protectedText">The encrypted value to decrypt.</param>
    /// <returns>The plain-text value.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Thrown if the value cannot be decrypted (e.g., key has rotated, or value was not encrypted).
    /// </exception>
    string Unprotect(string protectedText);
}
