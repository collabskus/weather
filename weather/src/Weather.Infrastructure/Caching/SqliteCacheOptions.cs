namespace Weather.Infrastructure.Caching;

/// <summary>Configuration for the SQLite cache, bound from the <c>"Cache"</c> section.</summary>
public sealed class SqliteCacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// ADO.NET connection string for the SQLite cache database. A plain file
    /// path works well, e.g. <c>Data Source=weather-cache.db</c>. Tests point
    /// this at a throwaway temp file.
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=weather-cache.db";
}
