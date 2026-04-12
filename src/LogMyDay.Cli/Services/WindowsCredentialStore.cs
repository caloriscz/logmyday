using System.Runtime.InteropServices;
using System.Text;

namespace LogMyDay.Cli.Services;

public class WindowsCredentialStore : ICredentialStore
{
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const string Prefix = "lmd:";
    private const int ErrorNotFound = 1168;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredReadW(string target, uint type, int flags, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWriteW(ref CREDENTIAL userCredential, int flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDeleteW(string target, uint type, int flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredEnumerateW(string? filter, int flags, out int count, out IntPtr credentialArrayPtr);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    public void Save(string alias, Uri server, string username, string password)
    {
        var target = $"{Prefix}{alias}";
        var userName = $"{server}|{username}";
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var passwordHandle = Marshal.AllocHGlobal(passwordBytes.Length);
        try
        {
            Marshal.Copy(passwordBytes, 0, passwordHandle, passwordBytes.Length);

            var cred = new CREDENTIAL
            {
                Flags = 0,
                Type = CredTypeGeneric,
                TargetName = target,
                UserName = userName,
                CredentialBlob = passwordHandle,
                CredentialBlobSize = (uint)passwordBytes.Length,
                Persist = CredPersistLocalMachine,
                AttributeCount = 0,
                Attributes = IntPtr.Zero
            };

            if (!CredWriteW(ref cred, 0))
            {
                throw new InvalidOperationException(
                    $"Failed to save credential for alias '{alias}' (Win32 error {Marshal.GetLastWin32Error()}).");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(passwordHandle);
        }
    }

    public StoredCredential? Load(string alias)
    {
        var target = $"{Prefix}{alias}";

        if (!CredReadW(target, CredTypeGeneric, 0, out var credPtr))
        {
            return null;
        }

        try
        {
            return ParseCredential(credPtr);
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    public void Delete(string alias)
    {
        var target = $"{Prefix}{alias}";

        if (!CredDeleteW(target, CredTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();

            if (error != ErrorNotFound)
            {
                throw new InvalidOperationException(
                    $"Failed to delete credential for alias '{alias}' (Win32 error {error}).");
            }
        }
    }

    public IReadOnlyList<StoredCredential> LoadAll()
    {
        if (!CredEnumerateW($"{Prefix}*", 0, out var count, out var arrayPtr))
        {
            var error = Marshal.GetLastWin32Error();

            if (error == ErrorNotFound)
            {
                return [];
            }

            throw new InvalidOperationException($"Failed to enumerate credentials (Win32 error {error}).");
        }

        try
        {
            var result = new List<StoredCredential>(count);

            for (var i = 0; i < count; i++)
            {
                var credPtr = Marshal.ReadIntPtr(arrayPtr, i * IntPtr.Size);
                var cred = ParseCredential(credPtr);

                if (cred is not null)
                {
                    result.Add(cred);
                }
            }

            return result;
        }
        finally
        {
            CredFree(arrayPtr);
        }
    }

    private static StoredCredential? ParseCredential(IntPtr credPtr)
    {
        var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);

        if (cred.UserName is null || !cred.TargetName.StartsWith(Prefix))
        {
            return null;
        }

        var alias = cred.TargetName[Prefix.Length..];

        // UserName is encoded as "{server}|{username}"
        var separatorIdx = cred.UserName.IndexOf('|');

        if (separatorIdx < 0)
        {
            return null;
        }

        var server = cred.UserName[..separatorIdx];
        var username = cred.UserName[(separatorIdx + 1)..];

        if (!Uri.TryCreate(server, UriKind.Absolute, out var serverUri))
        {
            return null;
        }

        var password = string.Empty;

        if (cred.CredentialBlob != IntPtr.Zero && cred.CredentialBlobSize > 0)
        {
            var passwordBytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, passwordBytes, 0, (int)cred.CredentialBlobSize);
            password = Encoding.UTF8.GetString(passwordBytes);
        }

        return new StoredCredential(alias, serverUri, username, password);
    }
}
