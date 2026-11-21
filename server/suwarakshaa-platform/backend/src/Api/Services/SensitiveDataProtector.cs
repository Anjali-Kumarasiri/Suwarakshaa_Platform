using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
namespace Api.Services;

/// Encrypts sensitive personal data (medical notes, home address) before it is stored in the database.
/// Keys are kept by ASP.NET Data Protection (persisted to a folder/volume so data survives restarts).
public class SensitiveDataProtector
{
    private readonly IDataProtector _p;
    public SensitiveDataProtector(IDataProtectionProvider provider) => _p = provider.CreateProtector("Households.v1");

    public byte[]? Protect(string? plain) => string.IsNullOrWhiteSpace(plain) ? null : Encoding.UTF8.GetBytes(_p.Protect(plain));

    public string? Unprotect(byte[]? data)
    {
        if (data is null || data.Length == 0) return null;
        try { return _p.Unprotect(Encoding.UTF8.GetString(data)); }
        catch (CryptographicException) { return null; }      // wrong/lost key: never crash, never leak
    }
}
