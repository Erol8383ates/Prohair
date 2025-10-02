// Models/BusinessSettings.cs
using System;

public class BusinessSettings
{
    public int Id { get; set; } = 1;
    public string TimeZone { get; set; } = "Europe/Brussels";
    public int SlotMinutes { get; set; } = 30;
    public int MinNoticeHours { get; set; } = 2;
    public int MaxSimultaneousBookings { get; set; } = 1;
}

// Models/WeeklyOpenHours.cs
public class WeeklyOpenHours
{
    public int Id { get; set; }
    public DayOfWeek Day { get; set; }          // Monday..Sunday
    public bool IsClosed { get; set; }          // if true, Open/Close ignored
    public TimeOnly? Open { get; set; }         // 09:00
    public TimeOnly? Close { get; set; }        // 19:00
}

// Models/BlackoutDate.cs
public class BlackoutDate
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }          // 2025-12-25
    public string? Reason { get; set; }
}
