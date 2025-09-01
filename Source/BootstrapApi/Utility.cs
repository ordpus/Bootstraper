namespace Bootstrap;

public static class Utility {
    private static readonly char[] HexChars = "0123456789abcdef".ToCharArray();
    private static readonly char[] UpperHexChars = "0123456789ABCDEF".ToCharArray();

    public static string ToHexString(this byte[]? bytes, bool upperCase = false) {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length == 0) return string.Empty;
        var chars = upperCase ? UpperHexChars : HexChars;
        var result = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++) {
            var b = bytes[i];
            result[i * 2] = chars[b >> 4];
            result[i * 2 + 1] = chars[b & 0x0F];
        }
        return new string(result);
    }

    public static string ToStringSafeDictionary<TKey, TValue>(this IDictionary<TKey, TValue>? dict) {
        return dict == null ? "null" : $"{{ {string.Join(", ", dict.Select(pair => $"{{ {pair.Key} = {pair.Value} }}"))} }}";
    }

    public static string ToStringSafeDictionary<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue>? dict) {
        return dict == null ? "null" : $"{{ {string.Join(", ", dict.Select(pair => $"{{ {pair.Key} = {pair.Value} }}"))} }}";
    }
}