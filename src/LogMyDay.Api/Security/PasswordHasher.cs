using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace LogMyDay.Api.Security;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public sealed class Argon2IdPasswordHasher : IPasswordHasher
{
    private const int SaltSize = 32; // 256 bits
    private const int HashSize = 32; // 256 bits
    private const int Iterations = 3;
    private const int MemorySize = 65536; // 64 MB
    private static readonly int DegreeOfParallelism = Environment.ProcessorCount;

    public string Hash(string password)
    {
        // Generate a cryptographically random salt
        var salt = new byte[SaltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Hash the password
        var hash = HashPassword(password, salt);

        // Combine algorithm identifier, parameters, salt, and hash
        // Format: argon2id$v=19$m=65536,t=3,p=4$<base64-salt>$<base64-hash>
        var saltBase64 = Convert.ToBase64String(salt);
        var hashBase64 = Convert.ToBase64String(hash);

        return $"argon2id$v=19$m={MemorySize},t={Iterations},p={DegreeOfParallelism}${saltBase64}${hashBase64}";
    }

    public bool Verify(string password, string hash)
    {
        try
        {
            // Parse the hash string
            var parts = hash.Split('$');
            if (parts.Length != 5 || parts[0] != "argon2id" || parts[1] != "v=19")
            {
                return false;
            }

            // Parse parameters
            var paramParts = parts[2].Split(',');
            var memorySize = int.Parse(paramParts[0].Split('=')[1]);
            var iterations = int.Parse(paramParts[1].Split('=')[1]);
            var parallelism = int.Parse(paramParts[2].Split('=')[1]);

            // Extract salt and stored hash
            var salt = Convert.FromBase64String(parts[3]);
            var storedHash = Convert.FromBase64String(parts[4]);

            // Hash the provided password with the same parameters
            var computedHash = HashPassword(password, salt, memorySize, iterations, parallelism);

            // Compare hashes using constant-time comparison
            return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] HashPassword(string password, byte[] salt, int memorySize = MemorySize, int iterations = Iterations, int parallelism = -1)
    {
        if (parallelism == -1)
        {
            parallelism = DegreeOfParallelism;
        }

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.DegreeOfParallelism = parallelism;
        argon2.Iterations = iterations;
        argon2.MemorySize = memorySize;

        return argon2.GetBytes(HashSize);
    }
}
