namespace Backend.Utils;

/// <summary>
/// Base64url (RFC 4648 §5) — the padding-free, URL-safe alphabet every part of a
/// JWT is encoded with: header, payload, signature, and the x/y coordinates of a
/// JWKS key.
/// </summary>
public static class Base64Url
{
    public static byte[] Decode(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }

    public static string Encode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
