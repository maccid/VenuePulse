namespace VenuePulse.Core.Domain;

/// <summary>
/// Guard clauses para invariantes de constructor/entrada repetidas en los 3 dominios
/// (`Ticket`, `Event`, `Zone` ya tenían el mismo `if`/`throw` copiado tres veces) — ver ADR 0011.
/// Lanza <see cref="ArgumentException"/>, igual que el código que sustituye: son invariantes de
/// entrada al agregado, no fallos de negocio esperables (para eso ya existe <see cref="Result"/>,
/// ver el ADR sobre por qué no se mezclan).
/// </summary>
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
