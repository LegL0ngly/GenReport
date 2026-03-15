namespace GenReport.DB.Domain.Enums
{
    /// <summary>
    /// Enum representing the database provider (driver) used for connections.
    /// </summary>
    public enum DbProvider
    {
        /// <summary>NpgSql provider for PostgreSQL.</summary>
        NpgSql = 1,
        /// <summary>System.Data.SqlClient provider for SQL Server.</summary>
        SqlClient = 2,
        /// <summary>MySqlConnector provider for MySQL.</summary>
        MySqlConnector = 3,
        /// <summary>Oracle provider for Oracle Database.</summary>
        Oracle = 4,
        /// <summary>SQLite provider for SQLite.</summary>
        SQLite = 5
    }
}
