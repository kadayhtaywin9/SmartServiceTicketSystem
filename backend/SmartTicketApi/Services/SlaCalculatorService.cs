namespace SmartTicketApi.Services;

public class SlaCalculatorService
{
    private readonly int _workStartHour;
    private readonly int _workEndHour;

    public SlaCalculatorService(IConfiguration config)
    {
        _workStartHour = config.GetValue<int>("SlaBusinessHours:StartHour", 9);
        _workEndHour = config.GetValue<int>("SlaBusinessHours:EndHour", 18);
    }

    public DateTime CalculateDueDate(DateTime startUtc, int minutesToAdd, bool businessHoursOnly)
    {
        if (!businessHoursOnly)
            return startUtc.AddMinutes(minutesToAdd);

        DateTime current = NormalizeToBusinessHours(startUtc);
        int remainingMinutes = minutesToAdd;

        while (remainingMinutes > 0)
        {
            DateTime endOfWorkDay = current.Date.AddHours(_workEndHour);
            int availableToday = (int)(endOfWorkDay - current).TotalMinutes;

            if (remainingMinutes <= availableToday)
            {
                current = current.AddMinutes(remainingMinutes);
                remainingMinutes = 0;
            }
            else
            {
                remainingMinutes -= availableToday;
                current = MoveToNextBusinessMorning(current);
            }
        }

        return current;
    }

    private DateTime NormalizeToBusinessHours(DateTime dt)
    {
        while (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday)
            dt = dt.Date.AddDays(1).AddHours(_workStartHour);

        if (dt.Hour < _workStartHour)
            return dt.Date.AddHours(_workStartHour);

        if (dt.Hour >= _workEndHour)
            return MoveToNextBusinessMorning(dt);

        return dt;
    }

    private DateTime MoveToNextBusinessMorning(DateTime dt)
    {
        DateTime next = dt.Date.AddDays(1).AddHours(_workStartHour);
        while (next.DayOfWeek == DayOfWeek.Saturday || next.DayOfWeek == DayOfWeek.Sunday)
            next = next.AddDays(1);
        return next;
    }
}
