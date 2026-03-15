using System;

namespace AeroScape.LoginServer;

/// <summary>
/// XTEA (eXtended Tiny Encryption Algorithm) block cipher.
/// Used to decrypt the non-RSA portion of the OSRS login packet.
/// Standard 64-round (32 full rounds) XTEA.
/// </summary>
public static class Xtea
{
    private const uint Delta = 0x9E3779B9;
    private const uint InitialSum = 0xC6EF3720; // Delta * 32

    /// <summary>
    /// Decrypts a byte array in-place using XTEA with the supplied 4-int key.
    /// Any trailing bytes that don't form a complete 8-byte block are left unchanged.
    /// </summary>
    /// <param name="data">The data buffer to decrypt (modified in-place).</param>
    /// <param name="offset">Start offset within the buffer.</param>
    /// <param name="length">Number of bytes to process (will be rounded down to nearest 8).</param>
    /// <param name="key">4-element int[] key derived from the ISAAC seed integers.</param>
    public static void Decrypt(byte[] data, int offset, int length, int[] key)
    {
        if (key == null || key.Length < 4)
            throw new ArgumentException("XTEA key must have 4 elements.", nameof(key));

        uint k0 = (uint)key[0];
        uint k1 = (uint)key[1];
        uint k2 = (uint)key[2];
        uint k3 = (uint)key[3];

        int blocks = length / 8;
        for (int i = 0; i < blocks; i++)
        {
            int pos = offset + i * 8;
            uint v0 = ReadBE32(data, pos);
            uint v1 = ReadBE32(data, pos + 4);

            uint sum = InitialSum;
            for (int r = 0; r < 32; r++)
            {
                v1 -= ((v0 << 4 ^ v0 >> 5) + v0) ^ (sum + ((sum >> 11 & 3) switch
                {
                    0 => k0, 1 => k1, 2 => k2, _ => k3
                }));
                sum -= Delta;
                v0 -= ((v1 << 4 ^ v1 >> 5) + v1) ^ (sum + ((sum & 3) switch
                {
                    0 => k0, 1 => k1, 2 => k2, _ => k3
                }));
            }

            WriteBE32(data, pos,     v0);
            WriteBE32(data, pos + 4, v1);
        }
    }

    private static uint ReadBE32(byte[] buf, int offset) =>
        ((uint)buf[offset]     << 24) |
        ((uint)buf[offset + 1] << 16) |
        ((uint)buf[offset + 2] <<  8) |
         (uint)buf[offset + 3];

    private static void WriteBE32(byte[] buf, int offset, uint value)
    {
        buf[offset]     = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >>  8);
        buf[offset + 3] = (byte)(value);
    }
}
