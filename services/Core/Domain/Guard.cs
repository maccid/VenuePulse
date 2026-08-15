namespace VenuePulse.Core.Domain;

public static class Guard
{
    public static string AgainstNullOrWhiteSpace(string? value, string paramName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(message, paramName);

        return value;
    }

    public static int AgainstNonPositive(int value, string paramName, string message)
    {
        if (value <= 0)
            throw new ArgumentException(message, paramName);

        return value;
    }

    public static decimal AgainstNonPositive(decimal value, string paramName, string message)
    {
        if (value <= 0)
            throw new ArgumentException(message, paramName);

        return value;
    }
}
