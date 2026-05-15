using System.Security.Cryptography;
using System.Text;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Security;

internal sealed class TotpService(IClock clock) : ITotpService
{
    private const int timeStepSeconds = 30;
    private const int digits = 6;
    private const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret()
    {
        Span<byte> bytes = stackalloc byte[20];
        RandomNumberGenerator.Fill(bytes);
        return Base32Encode(bytes);
    }

    public string BuildOtpAuthUri(string issuer, string accountName, string secret)
    {
        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        var issuerValue = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{label}?secret={secret}&issuer={issuerValue}&algorithm=SHA1&digits={digits}&period={timeStepSeconds}";
    }

    public TotpVerificationResult Verify(string secret, string code, int allowedTimeStepDrift)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6 || !code.All(char.IsDigit))
        {
            return new TotpVerificationResult(false, 0);
        }

        var secretBytes = Base32Decode(secret);
        var currentStep = ToTimeStep(clock.UtcNow);

        for (var offset = -allowedTimeStepDrift; offset <= allowedTimeStepDrift; offset++)
        {
            var step = currentStep + offset;
            if (step < 0)
            {
                continue;
            }

            var expected = ComputeCode(secretBytes, step);
            if (CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected),
                Encoding.ASCII.GetBytes(code)))
            {
                return new TotpVerificationResult(true, step);
            }
        }

        return new TotpVerificationResult(false, 0);
    }

    private static long ToTimeStep(DateTimeOffset timestamp) =>
        timestamp.ToUnixTimeSeconds() / timeStepSeconds;

    private static string ComputeCode(byte[] secret, long timeStep)
    {
        Span<byte> counter = stackalloc byte[8];
        BitConverter.TryWriteBytes(counter, timeStep);
        if (BitConverter.IsLittleEndian)
        {
            counter.Reverse();
        }

#pragma warning disable CA5350 // TOTP defaults to HMAC-SHA1 for authenticator-app compatibility.
        using var hmac = new HMACSHA1(secret);
#pragma warning restore CA5350
        var hash = hmac.ComputeHash(counter.ToArray());
        var offset = hash[^1] & 0x0f;
        var binary =
            ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        var otp = binary % 1_000_000;
        return otp.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Base32Encode(ReadOnlySpan<byte> bytes)
    {
        var output = new StringBuilder((bytes.Length + 4) / 5 * 8);
        var bitBuffer = 0;
        var bitCount = 0;

        foreach (var b in bytes)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitCount += 8;

            while (bitCount >= 5)
            {
                output.Append(alphabet[(bitBuffer >> (bitCount - 5)) & 0x1f]);
                bitCount -= 5;
            }
        }

        if (bitCount > 0)
        {
            output.Append(alphabet[(bitBuffer << (5 - bitCount)) & 0x1f]);
        }

        return output.ToString();
    }

    private static byte[] Base32Decode(string value)
    {
        var normalized = value.Trim().Replace("=", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        var bytes = new List<byte>();
        var bitBuffer = 0;
        var bitCount = 0;

        foreach (var c in normalized)
        {
            var index = alphabet.IndexOf(c, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new FormatException("Invalid base32 secret.");
            }

            bitBuffer = (bitBuffer << 5) | index;
            bitCount += 5;

            if (bitCount >= 8)
            {
                bytes.Add((byte)((bitBuffer >> (bitCount - 8)) & 0xff));
                bitCount -= 8;
            }
        }

        return bytes.ToArray();
    }
}
