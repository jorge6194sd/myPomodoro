using Dapper;
using System.Data.SqlClient;
using WebApplication1.Models;

namespace WebApplication1.Data
{
    public class TimerSessionRepository
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public TimerSessionRepository(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        }

        private SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<int> InsertSessionsAsync(IEnumerable<TimerSession> sessions)
        {
            const string sql = @"INSERT INTO TimerSessions
                                   (StartTime, EndTime, DurationMinutes, SessionType, FocusRating, SessionCategory)
                                   VALUES (@StartTime, @EndTime, @DurationMinutes, @SessionType, @FocusRating, @SessionCategory)";
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(sql, sessions);
        }

        public async Task<IEnumerable<TimerSession>> GetAllAsync()
        {
            const string sql = @"SELECT StartTime, EndTime, DurationMinutes, SessionType, FocusRating, SessionCategory
                                 FROM TimerSessions";
            using var connection = CreateConnection();
            return await connection.QueryAsync<TimerSession>(sql);
        }

        public async Task<TimerSession?> GetByIdAsync(int id)
        {
            const string sql = @"SELECT StartTime, EndTime, DurationMinutes, SessionType, FocusRating, SessionCategory
                                 FROM TimerSessions WHERE Id = @Id";
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<TimerSession>(sql, new { Id = id });
        }

        public async Task<int> UpdateAsync(int id, TimerSession session)
        {
            const string sql = @"UPDATE TimerSessions SET
                                   StartTime = @StartTime,
                                   EndTime = @EndTime,
                                   DurationMinutes = @DurationMinutes,
                                   SessionType = @SessionType,
                                   FocusRating = @FocusRating,
                                   SessionCategory = @SessionCategory
                                 WHERE Id = @Id";
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(sql, new
            {
                Id = id,
                session.StartTime,
                session.EndTime,
                session.DurationMinutes,
                session.SessionType,
                session.FocusRating,
                session.SessionCategory
            });
        }

        public async Task<int> DeleteAsync(int id)
        {
            const string sql = "DELETE FROM TimerSessions WHERE Id = @Id";
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(sql, new { Id = id });
        }
    }
}
