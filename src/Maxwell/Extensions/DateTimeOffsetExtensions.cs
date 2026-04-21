namespace Maxwell;

public static class DateTimeOffsetExtensions
{
    extension(DateTimeOffset source)
    {
        public DateTimeOffset RemoveMilliseconds()=> source.AddTicks(-(source.Ticks % TimeSpan.TicksPerSecond));
    }
}