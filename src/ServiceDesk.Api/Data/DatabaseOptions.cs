namespace ServiceDesk.Api.Data;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>"SqlServer" (migrations) or "Sqlite" (zero-setup local runs and tests).</summary>
    public string Provider { get; set; } = "Sqlite";

    /// <summary>Insert demo users and tickets when the database is empty.</summary>
    public bool Seed { get; set; } = true;

    public bool UseSqlServer => Provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);
}
