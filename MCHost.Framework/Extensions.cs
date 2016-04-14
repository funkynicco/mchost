using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public static class Extensions
{
    public static string ToUsernameHash(this string username)
    {
        using (var hash = SHA1.Create())
        {
            var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(username));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("X2"));
            return sb.ToString();
        }
    }

    public static string ToPasswordHash(this string text, string username)
    {
        using (var hash = SHA512.Create())
        {
            var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(username.ToUsernameHash() + text));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("X2"));
            return sb.ToString();
        }
    }
}