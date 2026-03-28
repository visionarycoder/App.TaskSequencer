using ConsoleApp.Ifx.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp.Services;

/// <summary>
/// Service for calculating occurrence times from recurrence patterns.
/// Handles generating scheduled times for recurring intake events.
/// </summary>
public class RecurrenceCalculationService
{
    /// <summary>
    /// Calculates all occurrence times within a given time window based on the recurrence pattern.
    /// </summary>
    /// <param name="pattern">The recurrence pattern to expand</param>
    /// <param name="intakeTime">The time of day for each occurrence (e.g., "11:30:00")</param>
    /// <param name="startDateTime">Start of the time window</param>
    /// <param name="endDateTime">End of the time window</param>
    /// <returns>List of calculated occurrence DateTimes</returns>
    public IEnumerable<DateTime> CalculateOccurrences(
        RecurrencePattern pattern,
        string intakeTime,
        DateTime startDateTime,
        DateTime endDateTime)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(intakeTime);

        if (!pattern.IsValid())
            throw new ArgumentException("Recurrence pattern is invalid.", nameof(pattern));

        if (endDateTime <= startDateTime)
            throw new ArgumentException("End time must be after start time.");

        if (!TimeSpan.TryParse(intakeTime, out var timeOfDay))
            throw new ArgumentException($"Invalid time format: {intakeTime}. Expected HH:mm:ss");

        return pattern.Frequency switch
        {
            RecurrenceFrequency.None => Enumerable.Empty<DateTime>(),
            RecurrenceFrequency.Minutely => CalculateMinutelyOccurrences(pattern, timeOfDay, startDateTime, endDateTime),
            RecurrenceFrequency.Hourly => CalculateHourlyOccurrences(pattern, timeOfDay, startDateTime, endDateTime),
            RecurrenceFrequency.Daily => CalculateDailyOccurrences(pattern, timeOfDay, startDateTime, endDateTime),
            RecurrenceFrequency.Weekly => CalculateWeeklyOccurrences(pattern, timeOfDay, startDateTime, endDateTime),
            RecurrenceFrequency.Monthly => CalculateMonthlyOccurrences(pattern, timeOfDay, startDateTime, endDateTime),
            RecurrenceFrequency.Yearly => CalculateYearlyOccurrences(pattern, timeOfDay, startDateTime, endDateTime),
            _ => throw new NotSupportedException($"Recurrence frequency not supported: {pattern.Frequency}")
        };
    }

    private IEnumerable<DateTime> CalculateMinutelyOccurrences(
        RecurrencePattern pattern,
        TimeSpan timeOfDay,
        DateTime startDateTime,
        DateTime endDateTime)
    {
        var occurrences = new List<DateTime>();
        var current = startDateTime.Date.Add(timeOfDay);

        if (current < startDateTime)
            current = current.AddMinutes(pattern.Interval);

        int count = 0;
        while (current <= endDateTime && (!pattern.MaxOccurrences.HasValue || count < pattern.MaxOccurrences))
        {
            if (!pattern.EndDate.HasValue || current <= pattern.EndDate)
            {
                occurrences.Add(current);
                count++;
            }

            current = current.AddMinutes(pattern.Interval);
        }

        return occurrences;
    }

    private IEnumerable<DateTime> CalculateHourlyOccurrences(
        RecurrencePattern pattern,
        TimeSpan timeOfDay,
        DateTime startDateTime,
        DateTime endDateTime)
    {
        var occurrences = new List<DateTime>();
        var current = startDateTime.Date.Add(timeOfDay);

        if (current < startDateTime)
            current = current.AddHours(pattern.Interval);

        int count = 0;
        while (current <= endDateTime && (!pattern.MaxOccurrences.HasValue || count < pattern.MaxOccurrences))
        {
            if (!pattern.EndDate.HasValue || current <= pattern.EndDate)
            {
                occurrences.Add(current);
                count++;
            }

            current = current.AddHours(pattern.Interval);
        }

        return occurrences;
    }

    private IEnumerable<DateTime> CalculateDailyOccurrences(
        RecurrencePattern pattern,
        TimeSpan timeOfDay,
        DateTime startDateTime,
        DateTime endDateTime)
    {
        var occurrences = new List<DateTime>();
        var current = startDateTime.Date.Add(timeOfDay);

        if (current < startDateTime)
            current = current.AddDays(pattern.Interval);

        int count = 0;
        while (current <= endDateTime && (!pattern.MaxOccurrences.HasValue || count < pattern.MaxOccurrences))
        {
            if (!pattern.EndDate.HasValue || current <= pattern.EndDate)
            {
                occurrences.Add(current);
                count++;
            }

            current = current.AddDays(pattern.Interval);
        }

        return occurrences;
    }

    private IEnumerable<DateTime> CalculateWeeklyOccurrences(
        RecurrencePattern pattern,
        TimeSpan timeOfDay,
        DateTime startDateTime,
        DateTime endDateTime)
    {
        var occurrences = new List<DateTime>();
        var current = startDateTime.Date;

        int count = 0;
        while (current <= endDateTime && (!pattern.MaxOccurrences.HasValue || count < pattern.MaxOccurrences))
        {
            var dayOfWeek = (int)current.DayOfWeek;
            if (dayOfWeek == 0) dayOfWeek = 7; // Convert Sunday from 0 to 7

            if (pattern.WeeklyDays.Contains(dayOfWeek))
            {
                var occurrence = current.Add(timeOfDay);
                if (occurrence >= startDateTime && (!pattern.EndDate.HasValue || occurrence <= pattern.EndDate))
                {
                    occurrences.Add(occurrence);
                    count++;
                }
            }

            current = current.AddDays(1);
        }

        return occurrences;
    }

    private IEnumerable<DateTime> CalculateMonthlyOccurrences(
        RecurrencePattern pattern,
        TimeSpan timeOfDay,
        DateTime startDateTime,
        DateTime endDateTime)
    {
        var monthlyDays = pattern.GetAllMonthlyDays();
        
        if (monthlyDays.Count == 0)
            throw new InvalidOperationException("Monthly recurrence requires at least one MonthlyDay to be set.");

        var occurrences = new List<DateTime>();
        var current = startDateTime.Date;

        int count = 0;
        while (current.Year < endDateTime.Year || (current.Year == endDateTime.Year && current.Month <= endDateTime.Month))
        {
            if (pattern.MaxOccurrences.HasValue && count >= pattern.MaxOccurrences)
                break;

            // Generate occurrence for each specified day in this month
            foreach (var day in monthlyDays)
            {
                if (pattern.MaxOccurrences.HasValue && count >= pattern.MaxOccurrences)
                    break;

                DateTime occurrence;
                if (day == -1)
                {
                    // Last day of month
                    occurrence = new DateTime(current.Year, current.Month, 1).AddMonths(1).AddDays(-1).Add(timeOfDay);
                }
                else
                {
                    // Specific day (handle month-end, e.g., Feb 31 -> Feb 28/29)
                    var dayOfMonth = Math.Min(day, DateTime.DaysInMonth(current.Year, current.Month));
                    occurrence = new DateTime(current.Year, current.Month, dayOfMonth).Add(timeOfDay);
                }

                if (occurrence >= startDateTime && occurrence <= endDateTime && (!pattern.EndDate.HasValue || occurrence <= pattern.EndDate))
                {
                    occurrences.Add(occurrence);
                    count++;
                }
            }

            current = current.AddMonths(pattern.Interval);
        }

        return occurrences;
    }

    private IEnumerable<DateTime> CalculateYearlyOccurrences(
        RecurrencePattern pattern,
        TimeSpan timeOfDay,
        DateTime startDateTime,
        DateTime endDateTime)
    {
        var occurrences = new List<DateTime>();
        var current = startDateTime.Date;

        int count = 0;
        while (current.Year <= endDateTime.Year && (!pattern.MaxOccurrences.HasValue || count < pattern.MaxOccurrences))
        {
            var occurrence = current.Add(timeOfDay);

            if (occurrence >= startDateTime && occurrence <= endDateTime && (!pattern.EndDate.HasValue || occurrence <= pattern.EndDate))
            {
                occurrences.Add(occurrence);
                count++;
            }

            current = current.AddYears(pattern.Interval);
        }

        return occurrences;
    }
}
