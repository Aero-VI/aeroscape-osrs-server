using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;

namespace AeroScape.LoginServer;

/// <summary>
/// RSA key management for the AeroScape login server.
///
/// On first start, a 1024-bit RSA key pair is generated and saved to disk.
/// On subsequent starts, the persisted key is loaded.
///
/// The client must be patched to use the public modulus printed at startup.
/// Public exponent is always 65537.
/// </summary>
public static class RsaKeys
{
    private const string KeyFile = "server_rsa.xml";

    public static BigInteger Modulus      { get; private set; }
    public static BigInteger PrivateExp   { get; private set; }
    public static BigInteger PublicExp    { get; private set; } = new BigInteger(65537);

    // Keep the native RSA object for direct use
    private static RSA? _rsa;

    /// <summary>
    /// Loads or generates the RSA key pair.
    /// Call once at server startup.
    /// </summary>
    public static void Initialize()
    {
        _rsa = RSA.Create(1024);

        if (File.Exists(KeyFile))
        {
            try
            {
                string xml = File.ReadAllText(KeyFile);
                _rsa.FromXmlString(xml);
                Console.WriteLine($"[RSA] Loaded existing key from {KeyFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RSA] Failed to load key ({ex.Message}), generating new one.");
                _rsa = RSA.Create(1024);
                SaveKey();
            }
        }
        else
        {
            SaveKey();
            Console.WriteLine($"[RSA] Generated new 1024-bit key pair, saved to {KeyFile}");
        }

        var p = _rsa.ExportParameters(includePrivateParameters: true);
        // BigInteger from big-endian unsigned byte array
        Modulus    = new BigInteger(p.Modulus!,    isUnsigned: true, isBigEndian: true);
        PrivateExp = new BigInteger(p.D!,          isUnsigned: true, isBigEndian: true);
        PublicExp  = new BigInteger(p.Exponent!,   isUnsigned: true, isBigEndian: true);

        Console.WriteLine($"[RSA] Modulus (hex, patch into client):");
        Console.WriteLine($"      {Convert.ToHexString(p.Modulus!)}");
        Console.WriteLine($"[RSA] Public exponent: {PublicExp}");
        Console.WriteLine($"[RSA] Modulus (decimal): {Modulus}");
    }

    private static void SaveKey()
    {
        File.WriteAllText(KeyFile, _rsa!.ToXmlString(includePrivateParameters: true));
    }

    /// <summary>
    /// Decrypts an RSA-encrypted block using the server's private key.
    /// Returns the plaintext as a byte array (leading zero stripped if present).
    /// </summary>
    public static byte[] Decrypt(byte[] cipherBytes)
    {
        // OSRS RSA: raw BigInteger modpow (no OAEP/PKCS1 padding)
        var enc = new BigInteger(cipherBytes, isUnsigned: true, isBigEndian: true);
        var dec = BigInteger.ModPow(enc, PrivateExp, Modulus);

        // Export back to big-endian unsigned byte array
        byte[] plaintext = dec.ToByteArray(isUnsigned: true, isBigEndian: true);
        return plaintext;
    }
}
