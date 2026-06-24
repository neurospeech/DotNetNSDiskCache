namespace NeuroSpeech.NSDiskCache;

internal static class StringHelpers
{

    private const long K = 1000;
    private const long M = K * K;
    private const long B = M * K;

    private const long KB = 1024;
    private const long MB = KB * KB;
    private const long GB = MB * KB;
    private const long TB = GB * KB;
    private const long PB = TB * KB;

    private static string ToCompactFixed(double n, int decimals)
    {
        if (decimals > 0)
        {
            if (n > 0)
            {
                return Math.Round(n, decimals).ToString("F" + decimals);
            }
        }
        return n.ToString("F0");
    }

    public static string ToKMBString(this long n, int decimals = 0, string def = "")
    {
        if (n == 0)
        {
            return def + "";
        }

        if (n >= B)
        {
            return ToCompactFixed(n / B, decimals) + "B";
        }
        if (n >= M)
        {
            return ToCompactFixed(n / M, decimals) + "M";
        }
        if (n >= K)
        {
            return ToCompactFixed(n / K, decimals) + "K";
        }

        return ToCompactFixed(n, 0);
    }

    public static string ToKMBString(this double n, int decimals = 0, string def = "")
    {
        if (n == 0)
        {
            return def + "";
        }

        if (n >= B)
        {
            return ToCompactFixed(n / B, decimals) + "B";
        }
        if (n >= M)
        {
            return ToCompactFixed(n / M, decimals) + "M";
        }
        if (n >= K)
        {
            return ToCompactFixed(n / K, decimals) + "K";
        }

        return ToCompactFixed(n, 0);
    }
}
