namespace NeuroSpeech.NSDiskCache;

internal static class DateTimeHelpers
{
    public static long ToMilliseconds(this DateTime dateTime)
    {
        return dateTime.Ticks / TimeSpan.TicksPerMillisecond;
    }

}
