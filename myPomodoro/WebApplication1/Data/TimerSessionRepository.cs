using Dapper;
using System.Data.SqlClient;
using WebApplication1.Models;

namespace WebApplication1.Data
{
    public class TimerSessionRepository
    {
        private readonly string _connectionString;

        public TimerSessionRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task InsertSessionsAsync(IEnumerable<TimerSession> sessions)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("Connection string not configured.");
            }

            using var connection = new SqlConnection(_connectionString);
            const string sql = @"INSERT INTO TimerSessions (StartTime, EndTime, DurationMinutes, SessionType, FocusRating, SessionCategory)
                                 VALUES (@StartTime, @EndTime, @DurationMinutes, @SessionType, @FocusRating, @SessionCategory)";
            await connection.OpenAsync();
            await connection.ExecuteAsync(sql, sessions);
        }

        public async Task<IEnumerable<TimerSession>> GetAllAsync()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("Connection string not configured.");
            }

            using var connection = new SqlConnection(_connectionString);
            const string sql = "SELECT StartTime, EndTime, DurationMinutes, SessionType, FocusRating, SessionCategory FROM TimerSessions";
            await connection.OpenAsync();
            return await connection.QueryAsync<TimerSession>(sql);
        }
    }
}
