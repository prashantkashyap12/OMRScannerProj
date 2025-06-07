using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace SQCScanner.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class showRecord : ControllerBase
    {

        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        public showRecord(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("dbc")!;
        }

        [HttpGet]
        [Route("getRec")]
        public async Task <IActionResult> Index(int Id, string direction, int tempId)
        {
            dynamic res = null;
            string query;
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    if (direction == "up")
                    {
                        query = $"SELECT TOP 200 * FROM Template_{tempId} where id <= {Id} ORDER BY Id";
                        var resw = await connection.QueryAsync(query);
                        res = new
                        {
                            state = true,
                            massege = "Record Found",
                            data = resw
                        };
                    }
                    else if (direction == "down")
                    {
                        query = $"SELECT TOP 200 * FROM Template_{tempId} where id >= {Id} ORDER BY Id";
                        var resw = await connection.QueryAsync(query);
                        res = new
                        {
                            state = true,
                            massege = "Record Found",
                            data = resw
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    massege = "Error Occured",
                    error = ex.Message
                };
            }
            return Ok(res);
        }
    }
}
