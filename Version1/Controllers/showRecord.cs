using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json.Linq;

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


        [HttpGet]
        [Route("LastRec")]
        public async Task<IActionResult> mainVal(string TempId, string token)
        {
            dynamic res;
            string query = "";
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                var userName = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "unique_name")?.Value;
                using (var _conn = new SqlConnection(_connectionString))
                {
                    var tempTable = "Template_"+TempId;
                    query = @$"select TOP 100 * from {tempTable} where UserName ='{userName}' ORDER BY Id DESC";
                    var result = await _conn.QueryAsync(query);
                    res = new
                    {
                        res = result,
                        state = true
                    };

                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    message = ex.Message,
                    state = false
                };
            }
            return Ok(res);
        }


        [HttpGet]
        [Route("Dash_AllRec")]
        public async Task<IActionResult> allRecord()
        {
            var query = "";
            dynamic result;
            using(var _conn = new SqlConnection(_connectionString))
            {
                query = $@"SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_NAME LIKE 'Template_%'";
                result = await _conn.QueryAsync<string>(query);
                var total = 0; query = null;
                foreach(var data in result)
                {
                    query = $"SELECT COUNT(*) FROM [{data}]";
                    var count = await _conn.ExecuteScalarAsync<int>(query);
                    Console.WriteLine(count);
                    total = total + count;
                }       
                return Ok(total);
            }
        }

        [HttpGet]
        [Route("Dash_ReadyTemp")]
        public async Task<IActionResult> TotalFold()
        {
            var query = "";
            dynamic result;
            using (var _conn = new SqlConnection(_connectionString))
            {
                query = $@"SELECT COUNT(*) AS FilledCount FROM ImgTemplate WHERE imgPath IS NOT NULL AND imgPath != '' AND JsonPath IS NOT NULL AND JsonPath != '';";
                result = await _conn.QueryAsync<int>(query);
                return Ok(result[0]);
            }
        }


        [HttpGet("Dash_Avrage")]
        public async Task<IActionResult> 



    }
}
