// IfsahApp.Utils/Security/EmailTokenHelper.cs
using System.Security.Cryptography;
using System.Text;

namespace IfsahApp.Utils.Security;

public static class EmailTokenHelper
{
    public static string GenerateToken(int bytes = 32)
    {
        var data = RandomNumberGenerator.GetBytes(bytes);
        // URL-safe Base64 (no padding)
        return Convert.ToBase64String(data)
                      .TrimEnd('=')
                      .Replace('+','-')
                      .Replace('/','_');
    }

    public static string Sha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
