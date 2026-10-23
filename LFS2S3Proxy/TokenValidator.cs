using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace LFS2S3Proxy
{
    internal static class TokenValidator
    {
        internal static bool MatchesUrlToken(string rawUrl, string expectedToken)
        {
            if (string.IsNullOrEmpty(rawUrl) || rawUrl[0] != '/' || string.IsNullOrEmpty(expectedToken))
            {
                return false;
            }

            var separatorIndex = rawUrl.IndexOf('/', 1);
            if (separatorIndex <= 1)
            {
                return false;
            }

            var candidateSpan = rawUrl.AsSpan(1, separatorIndex - 1);
            return CryptographicOperations.FixedTimeEquals(
                MemoryMarshal.AsBytes(expectedToken.AsSpan()),
                MemoryMarshal.AsBytes(candidateSpan));
        }
    }
}
