namespace Corteza.Infrastructure.Persistence.Configurations;

internal static class EnumCheck
{
    /// <summary>"[Status] IN ('Received', 'Preparing', ...)" para un CHECK constraint.</summary>
    public static string In<TEnum>(string column) where TEnum : struct, Enum =>
        $"[{column}] IN ({string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"))})";
}
