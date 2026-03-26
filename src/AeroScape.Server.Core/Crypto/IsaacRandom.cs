namespace AeroScape.Server.Core.Crypto;

/// <summary>
/// ISAAC (Indirection, Shift, Accumulate, Add, and Count) PRNG.
/// Used for opcode encryption between client and server in revision 508.
/// </summary>
public sealed class IsaacRandom
{
    private const int Size = 256;
    private const int Mask = 255;

    private int _count;
    private readonly int[] _results = new int[Size];
    private readonly int[] _memory = new int[Size];
    private int _accumulator;
    private int _lastResult;
    private int _counter;

    public IsaacRandom(int[] seed)
    {
        Array.Copy(seed, _results, Math.Min(seed.Length, Size));
        Init();
    }

    public int NextInt()
    {
        if (_count-- == 0)
        {
            Isaac();
            _count = Size - 1;
        }
        return _results[_count];
    }

    /// <summary>
    /// Peeks at the next value without consuming it.
    /// Used to tentatively decrypt an opcode before confirming the full
    /// packet frame is buffered — prevents ISAAC stream desynchronization
    /// on partial reads.
    /// </summary>
    public int PeekNextInt()
    {
        if (_count == 0)
        {
            // Next call to NextInt will trigger Isaac() and set _count = Size - 1,
            // then return _results[Size - 1]. We need to compute that without side effects.
            // Clone state, run Isaac, peek the value.
            var tempResults = (int[])_results.Clone();
            var tempMemory = (int[])_memory.Clone();
            int tempAcc = _accumulator;
            int tempLast = _lastResult;
            int tempCounter = _counter;

            // Run Isaac inline on temp state
            tempLast += ++tempCounter;
            for (int i = 0; i < Size; i++)
            {
                int x = tempMemory[i];
                tempAcc = (i & 3) switch
                {
                    0 => tempAcc ^ (tempAcc << 13),
                    1 => tempAcc ^ (int)((uint)tempAcc >> 6),
                    2 => tempAcc ^ (tempAcc << 2),
                    3 => tempAcc ^ (int)((uint)tempAcc >> 16),
                    _ => tempAcc
                };
                tempAcc += tempMemory[(i + 128) & Mask];
                int y = tempMemory[(int)((uint)x >> 2) & Mask] + tempAcc + tempLast;
                tempMemory[i] = y;
                tempLast = tempMemory[(int)((uint)y >> 10) & Mask] + x;
                tempResults[i] = tempLast;
            }

            return tempResults[Size - 1];
        }

        return _results[_count - 1];
    }

    private void Init()
    {
        Span<int> abcdefgh = stackalloc int[8];
        abcdefgh.Fill(unchecked((int)0x9e3779b9)); // golden ratio

        for (int i = 0; i < 4; i++)
            Mix(abcdefgh);

        for (int i = 0; i < Size; i += 8)
        {
            for (int j = 0; j < 8; j++)
                abcdefgh[j] += _results[i + j];
            Mix(abcdefgh);
            abcdefgh.CopyTo(_memory.AsSpan(i, 8));
        }

        for (int i = 0; i < Size; i += 8)
        {
            for (int j = 0; j < 8; j++)
                abcdefgh[j] += _memory[i + j];
            Mix(abcdefgh);
            abcdefgh.CopyTo(_memory.AsSpan(i, 8));
        }

        Isaac();
        _count = Size;
    }

    private void Isaac()
    {
        _lastResult += ++_counter;

        for (int i = 0; i < Size; i++)
        {
            int x = _memory[i];

            _accumulator = (i & 3) switch
            {
                0 => _accumulator ^ (_accumulator << 13),
                1 => _accumulator ^ (int)((uint)_accumulator >> 6),
                2 => _accumulator ^ (_accumulator << 2),
                3 => _accumulator ^ (int)((uint)_accumulator >> 16),
                _ => _accumulator
            };

            _accumulator += _memory[(i + 128) & Mask];
            int y = _memory[(int)((uint)x >> 2) & Mask] + _accumulator + _lastResult;
            _memory[i] = y;
            _lastResult = _memory[(int)((uint)y >> 10) & Mask] + x;
            _results[i] = _lastResult;
        }
    }

    private static void Mix(Span<int> s)
    {
        s[0] ^= s[1] << 11;  s[3] += s[0]; s[1] += s[2];
        s[1] ^= (int)((uint)s[1] >> 2);  s[4] += s[1]; s[2] += s[3];
        s[2] ^= s[2] << 8;   s[5] += s[2]; s[3] += s[4];
        s[3] ^= (int)((uint)s[3] >> 16); s[6] += s[3]; s[4] += s[5];
        s[4] ^= s[4] << 10;  s[7] += s[4]; s[5] += s[6];
        s[5] ^= (int)((uint)s[5] >> 4);  s[0] += s[5]; s[6] += s[7];
        s[6] ^= s[6] << 8;   s[1] += s[6]; s[7] += s[0];
        s[7] ^= (int)((uint)s[7] >> 9);  s[2] += s[7]; s[0] += s[1];
    }
}
