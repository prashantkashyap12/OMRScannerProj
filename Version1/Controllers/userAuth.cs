using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SQCScanner.Modal;
using SQCScanner.Services;
using Version1.Data;


namespace SQCScanner.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class userAuth : ControllerBase
    {
        private readonly ApplicationDbContext _DbContext;
        private readonly JwtAuth _jwtTokenGen;
        private readonly EncptDcript _EncptDcript;
        private readonly IConfiguration _conn;
        private readonly string _connectionString;

        public userAuth(ApplicationDbContext DbContext, JwtAuth Jwt, EncptDcript EncDec, IConfiguration conn)
        {
            _DbContext = DbContext;
            _jwtTokenGen = Jwt;
            _EncptDcript = EncDec;
            _conn = conn;
            _connectionString = conn.GetConnectionString("dbc");
        }

        // Add API --  
        [HttpPost("SignUp")]
        public async Task<IActionResult> Add(string name, string email, string pwd, string cont, string role)
        {
            dynamic res;
            try
            {
                dynamic resp;
                string query;
                using (var _conn = new SqlConnection(_connectionString))
                {
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pwd) || string.IsNullOrEmpty(cont) || string.IsNullOrEmpty(role))
                    {
                        res = new
                        {
                            state = false,
                            Message = "Please Fill Every Information"
                        };
                    }
                    query = $@"select MAX(EmpId) from empModels";
                    var result = _conn.ExecuteScalarAsync<int?>(query);
                    var empId = result == null ? 1001 : result.Result + 1;
                    var query2 = @$"insert into empModels ([EmpName],[EmpEmail],[password],[contact],[role]) values(@name, @Email, @Pwd, @Cont, @Role); 
                    insert into LoginTokenRec (EmpId) values (@Empid)";
                    var result2 = await _conn.ExecuteAsync(query2, new {
                        Name = name,
                        Email = email,
                        Pwd = pwd,
                        Cont = cont,
                        Role = role,
                        Empid = empId
                    });
                }
                res = new
                {
                    message = "User Created Successfully",
                    status = true,
                };
            }
            catch(Exception ex)
            {
                res = new
                {
                    message = ex.Message,
                    status = false,
                };
            }
            return Ok(res);
        }

        // Retireve API --  All Record
        [HttpGet]
        [Route("GetList")]
        public IActionResult GetList()
        {
            var empList = _DbContext.empModels.ToList();
            if (empList == null)
            {
                BadRequest("No Data Found");
            }
            else if (empList.Any())
            {
                var fs = empList;
                return Ok(fs);
            }
            return Ok(empList);
        }

        // Delete API  -- All Record
        [HttpDelete]
        [Route("DeleteEmp")]
        public IActionResult delete(int id)
        {
            try
            {
                using(var _conn = new SqlConnection(_connectionString))
                {
                    var empid = _DbContext.empModels.Find(id);
                    var empVal = empid;
                    if (empid == null)
                    {
                        BadRequest("Record is not avilable");
                    }
                    _DbContext.empModels.Remove(empid);
                    _DbContext.SaveChanges();
                    var query = $@"delete LoginTokenRec where where EmpId = {empid}";
                    var ress = _conn.ExecuteAsync(query);
                    return Ok(empVal + "Has Been Removed");
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Update API  -- Update User
        [HttpPut]
        [Route("Update")]
        public IActionResult update(int id, string name, string email, string pwd, string cont, string role)
        {
            //var hashingPwd = _empService.ComputeSha256Hash(pwd);
            try
            {
                var idmain = _DbContext.empModels.Find(id);
                if (idmain == null)
                {
                    BadRequest("Record not Find");
                }
                idmain.EmpName = name;
                idmain.EmpEmail = email;
                idmain.password = pwd;
                idmain.contact = cont;
                idmain.role = role;
                _DbContext.SaveChanges();
            }
            catch (Exception ex)
            {
                BadRequest(ex.Message);
            }
            return Ok("Emp Update");
        }

        // Check does Login oR not
        [HttpGet]
        [Route("LoginForm")]
        public async Task<IActionResult> get(string uname, string pwd)
        {
            dynamic res;

            try
            { 
                var token = string.Empty;
                //checked validation
                if (string.IsNullOrWhiteSpace(uname) || string.IsNullOrWhiteSpace(pwd))
                {
                    return BadRequest("Please Fill Every Information");
                }
                //string decrypted = _EncptDcript.Decrypt(pwd);

                var ReturnDetails = _DbContext.empModels.FirstOrDefault(x => x.EmpEmail == uname && x.password == pwd);

                if (ReturnDetails == null || ReturnDetails.password != pwd)
                {
                    return Unauthorized("Unauthorized User");
                }

                token = _jwtTokenGen.GenerateJwtToken(ReturnDetails);   // Db

                // save/update data when token gen. which token empId will be 
                string qurry = null;
                using (var _conn = new SqlConnection(_connectionString))
                {
                    _conn.Open();
                    var TokenMain = token;
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var jwtToken = tokenHandler.ReadJwtToken(token);

                    var EmpId = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
                    var role = jwtToken.Claims.FirstOrDefault(c => c.Type == "role")?.Value;

                    var expiryTimeUtc1 = jwtToken.ValidFrom.ToLocalTime();
                    var expiryTimeUtc2 = jwtToken.ValidTo;
                    var expiryTimeLocal2 = expiryTimeUtc2.ToLocalTime();
                    qurry = $@"update LoginTokenRec set Token='{TokenMain}',Expiry='{expiryTimeLocal2}',Role='{role}' where EmpId = {EmpId}";
                    var result = await _conn.ExecuteAsync(qurry);
                }
                res = new
                {
                    message = $"Login Success",
                    token = token,
                };
            }
            catch(Exception ex) {
                res = new
                {
                    massage = ex.Message,
                    message = $"Login Success",
                };
            }
            return Ok(res);
        }
    }
}
