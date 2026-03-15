using System;

namespace AeroScape.LoginServer;

/// <summary>
/// ISAAC (Indirection, Shift, Accumulate, Add, and Count) pseudo-random number generator.
/// Used for OSRS game packet opcode masking.
/// Reference: Bob Jenkins, 1996. https://burtleburtle.net/bob/rand/isaacafa.html
/// </summary>
public sealed class IsaacCipher
{
    private const int Size = 256;
    private const int GoldenRatio = unchecked((int)0x9E3779B9);

    private readonly int[] _results = new int[Size];
    private readonly int[] _mem = new int[Size];
    private int _count;
    private int _a, _b, _c;

    /// <summary>
    /// Creates and seeds a new ISAAC cipher instance.
    /// </summary>
    /// <param name="seed">The integer seed array. Only the first 256 elements are used.</param>
    public IsaacCipher(int[] seed)
    {
        _a = _b = _c = 0;

        int[] initArr = new int[8];
        Array.Fill(initArr, GoldenRatio);

        // Mix 4 times with golden ratio
        for (int i = 0; i < 4; i++)
            Mix(initArr);

        // Fill memory with seed data
        for (int i = 0; i < Size; i += 8)
        {
            for (int j = 0; j < 8; j++)
                initArr[j] += (i + j < seed.Length) ? seed[i + j] : 0;
            Mix(initArr);
            Array.Copy(initArr, 0, _mem, i, 8);
        }

        Generate();
        _count = Size;
    }

    /// <summary>Returns the next pseudo-random integer from the cipher stream.</summary>
    public int NextInt()
    {
        if (_count-- == 0)
        {
            Generate();
            _count = Size - 1;
        }
        return _results[_count];
    }

    private void Generate()
    {
        _c++;
        _b += _c;

        for (int i = 0; i < Size; i++)
        {
            int x = _mem[i];
            switch (i & 3)
            {
                case 0: _a ^= _a << 13;  break;
                case 1: _a ^= _a >>> 6;  break;
                case 2: _a ^= _a << 2;   break;
                case 3: _a ^= _a >>> 16; break;
            }
            _a += _mem[(i + 128) & 0xFF];
            int y = _mem[i] = _mem[(x >>> 2) & 0xFF] + _a + _b;
            _results[i] = _b = _mem[(y >>> 10) & 0xFF] + x;
        }
    }

    private static void Mix(int[] arr)
    {
        arr[0] ^= arr[1] << 11;  arr[3] += arr[0]; arr[1] += arr[2];
        arr[1] ^= arr[2] >>> 2;  arr[4] += arr[1]; arr[2] += arr[3];
        arr[2] ^= arr[3] << 8;   arr[5] += arr[2]; arr[3] += arr[4];
        arr[3] ^= arr[4] >>> 16; arr[6] += arr[3]; arr[4] += arr[5];
        arr[4] ^= arr[5] << 10;  arr[7] += arr[4]; arr[5] += arr[6];
        arr[5] ^= arr[6] >>> 4;  arr[0] += arr[5]; arr[6] += arr[7];
        arr[6] ^= arr[7] << 8;   arr[1] += arr[6]; arr[7] += arr[0];
        arr[7] ^= arr[0] >>> 9;  arr[2] += arr[7]; arr[0] += arr[1];
    }
}
