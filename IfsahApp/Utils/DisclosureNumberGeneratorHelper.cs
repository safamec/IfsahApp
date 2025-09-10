using System.Security.Cryptography;

namespace IfsahApp.Utils;

public static class DisclosureNumberGeneratorHelper
{
    private static readonly char[] chars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

    public static string Generate(int length = 8)
    {
        var token = new char[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] data = new byte[length];
            rng.GetBytes(data);

            for (int i = 0; i < length; i++)
            {
                token[i] = chars[data[i] % chars.Length];
            }
        }

        return $"DISC-{new string(token)}";
    }

    
    
}