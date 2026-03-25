namespace AeroScape.Server.Core.Util;

/// <summary>
/// RuneScape chat text encoding/decoding (Huffman-like compression).
/// Used in 508 protocol for public and private chat messages.
/// </summary>
public static class ChatEncoder
{
    private static readonly char[] ValidChars =
    [
        ' ', 'e', 't', 'a', 'o', 'i', 'h', 'n', 's', 'r',
        'd', 'l', 'u', 'm', 'w', 'c', 'y', 'f', 'g', 'p',
        'b', 'v', 'k', 'x', 'j', 'q', 'z', '0', '1', '2',
        '3', '4', '5', '6', '7', '8', '9', ' ', '!', '?',
        '.', ',', ':', ';', '(', ')', '-', '&', '*', '\\',
        '\'', '@', '#', '+', '=', '\u00A3', '$', '%', '"', '[', ']'
    ];

    /// <summary>
    /// Unpacks chat text from the 508 packed format.
    /// </summary>
    public static string Unpack(ReadOnlySpan<byte> data, int textLength)
    {
        var sb = new System.Text.StringBuilder(textLength);
        int dataIndex = 0;
        int bitOffset = 0;

        for (int i = 0; i < textLength; i++)
        {
            if (dataIndex >= data.Length) break;

            int charIndex = data[dataIndex] >> (7 - bitOffset) & 1;
            bitOffset++;

            if (bitOffset >= 8)
            {
                bitOffset = 0;
                dataIndex++;
            }

            if (charIndex == 0)
            {
                // 7-bit encoding
                int value = 0;
                for (int bit = 0; bit < 6; bit++)
                {
                    if (dataIndex >= data.Length) break;
                    value = (value << 1) | ((data[dataIndex] >> (7 - bitOffset)) & 1);
                    bitOffset++;
                    if (bitOffset >= 8)
                    {
                        bitOffset = 0;
                        dataIndex++;
                    }
                }
                if (value < ValidChars.Length)
                    sb.Append(ValidChars[value]);
            }
            else
            {
                // Extended character
                int value = 0;
                for (int bit = 0; bit < 12; bit++)
                {
                    if (dataIndex >= data.Length) break;
                    value = (value << 1) | ((data[dataIndex] >> (7 - bitOffset)) & 1);
                    bitOffset++;
                    if (bitOffset >= 8)
                    {
                        bitOffset = 0;
                        dataIndex++;
                    }
                }
                sb.Append((char)value);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Simple fallback: treats packed text as ASCII bytes.
    /// Many 508 clients use simple byte-per-char encoding.
    /// </summary>
    public static string UnpackSimple(ReadOnlySpan<byte> data)
    {
        var sb = new System.Text.StringBuilder(data.Length);
        foreach (byte b in data)
        {
            if (b >= 32 && b < 127)
                sb.Append((char)b);
        }
        return sb.ToString();
    }
}
