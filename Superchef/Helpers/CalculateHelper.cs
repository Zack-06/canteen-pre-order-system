namespace Superchef.Helpers;

public class CalculateHelper
{
    public static decimal CalculatePercentageChange(decimal previous, decimal current)
    {
        if (previous == 0)
        {
            return current > 0 ? 1m : 0m;
        }

        return (current - previous) / previous;
    }
}