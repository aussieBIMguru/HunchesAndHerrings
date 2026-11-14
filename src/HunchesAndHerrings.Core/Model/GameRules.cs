namespace HunchesAndHerrings.Core.Model;

/// <summary>Fixed costs and clock conventions.</summary>
public static class GameRules
{
    public const int StepMinutes = 5;
    public const int StairsMinutes = 10;
    public const int QuestionMinutes = 10;
    public const int SearchMinutes = 20;
    public const int WaitMinutes = 30;
    public const int SergeantSearchMinutes = 40;
    public const int SergeantInterviewMinutes = 50;

    /// <summary>The investigation begins at 8am.</summary>
    public const int DayStartHour = 8;

    /// <summary>Hours covered by today's schedules (8am to 5pm starts).</summary>
    public const int DayHours = 10;

    public const int DayStartMinute = DayStartHour * 60;
}

/// <summary>Helpers for the three time slots of the murder night.</summary>
public static class NightSlots
{
    public const int Count = 3;

    public static string Spoken(int slot) => slot switch
    {
        0 => "nine o'clock",
        1 => "ten o'clock",
        _ => "eleven o'clock",
    };

    public static string Short(int slot) => slot switch
    {
        0 => "9 pm",
        1 => "10 pm",
        _ => "11 pm",
    };

    public static int Hour24(int slot) => 21 + slot;
}

/// <summary>Formats minutes-since-midnight as a period clock.</summary>
public static class Clock
{
    public static string Format(int minuteOfDay)
    {
        int h = minuteOfDay / 60 % 24;
        int m = minuteOfDay % 60;
        string suffix = h < 12 ? "am" : "pm";
        int h12 = h % 12 == 0 ? 12 : h % 12;
        return $"{h12}:{m:00} {suffix}";
    }

    public static string Hour(int hour24)
    {
        string suffix = hour24 < 12 ? "am" : "pm";
        int h12 = hour24 % 12 == 0 ? 12 : hour24 % 12;
        return $"{h12} {suffix}";
    }

    public static string HourWords(int hour24)
    {
        string[] words = { "twelve", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven" };
        return words[hour24 % 12];
    }
}
