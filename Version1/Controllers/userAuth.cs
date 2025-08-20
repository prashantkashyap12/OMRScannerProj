using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Azure.Core;
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
using Version1.Modal;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;


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

        [HttpGet("DeviceLogOut")]
        public async Task<IActionResult> allDeviceLogOut()
        {
            dynamic res;
            var qurry = "";
            var expiredToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
            if (string.IsNullOrWhiteSpace(expiredToken))
            {
                return Unauthorized(new { message = "No token provided" });
            }
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(expiredToken);

            var empId = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
            var role = jwtToken.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
            var empName = jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;
            var empEmail = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var emp = new EmpModel
            {
                EmpId = Convert.ToInt32(empId),
                EmpName = empName,
                EmpEmail = empEmail,
                role = role
            };
            var jwtAuth = _jwtTokenGen.GenerateJwtToken(emp);


            using (var _conn = new SqlConnection(_connectionString))
            {
                var TokenMain = jwtAuth;
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken2 = tokenHandler.ReadJwtToken(jwtAuth);
                var EmpId2 = jwtToken2.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
                var role2 = jwtToken2.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
                var expiryTimeUtc1 = jwtToken.ValidFrom.ToLocalTime();
                var expiryTimeUtc2 = jwtToken.ValidTo;
                var expiryTimeLocal2 = expiryTimeUtc2.ToLocalTime();
                qurry = $@"update LoginTokenRec set Token='{TokenMain}',Expiry='{expiryTimeLocal2}',Role='{role2}', isLoggedIn='0'  where EmpId = {EmpId2}";
                var result = await _conn.ExecuteAsync(qurry);
                res = new
                {
                    state = true,
                    message = $"Logout From all Devices",
                    token = TokenMain,
                };
            }

            return Ok(res);
        }

        [HttpGet("RefreshToken")]
        public async Task<IActionResult> refresh()
        {
            dynamic res;
            var expiredToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
            if (string.IsNullOrWhiteSpace(expiredToken))
            {
                return Unauthorized(new { message = "No token provided" });
            }
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(expiredToken);

            //// Claims se data nikalo
            var empId = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
            var role = jwtToken.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
            var empName = jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;
            var empEmail = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var emp = new EmpModel
            {
                EmpId = Convert.ToInt32(empId),
                EmpName = empName,
                EmpEmail = empEmail,
                role = role
            };
            var jwtAuth = _jwtTokenGen.GenerateJwtToken(emp);
            res = new
            {
                state = true,
                token= jwtAuth
            };
            return Ok(res);
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
                bool isEmt = false;
                StringBuilder sb = new StringBuilder();
                using (var _conn = new SqlConnection(_connectionString))
                {
                    if (string.IsNullOrEmpty(name))
                    {
                        sb.Append("Name, ");
                        isEmt = true;
                    }
                    if (string.IsNullOrEmpty(email))
                    {
                        sb.Append("Email, ");
                        isEmt = true;
                    }
                    if (string.IsNullOrEmpty(pwd))
                    {
                        sb.Append("Password, ");
                        isEmt = true;
                    }
                    if (string.IsNullOrEmpty(cont))
                    {
                        sb.Append("Contact, ");
                        isEmt = true;
                    }
                    if (string.IsNullOrEmpty(role))
                    {
                        sb.Append("Role, ");
                        isEmt = true;
                    }
                    if(isEmt == true)
                    {
                        res = new
                        {
                            state = false,
                            Message = @$"These value are Empty {sb}"
                        };
                    }
                    else
                    {
                        var emailExist = _conn.ExecuteScalarAsync<int?>($@"select COUNT(1) from empModels where EmpEmail = '{email}'");
                        if (emailExist.Result != 0) {
                            res = new
                            {
                                state = false,
                                Message = @$"Email Id is Already Exist {email}"
                            };
                        }
                        else {
                            query = $@"select MAX(EmpId) from empModels";
                            var result = _conn.ExecuteScalarAsync<int?>(query);
                            var empId = result == null ? 1001 : result.Result + 1;
                            var query2 = @$"insert into empModels ([EmpName],[EmpEmail],[password],[contact],[role]) values('{name}', '{email}','{pwd}', '{cont}', '{role}'); 
                            insert into LoginTokenRec (EmpId) values ('{empId}')";
                            var result2 = await _conn.ExecuteAsync(query2);
                            res = new
                            {
                                state = true,
                                Message = @$"User Created Successfully {email}"
                            };
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                res = new
                {
                    Message = ex.Message,
                    status = false,
                };
            }
            return Ok(res);
        }

        // Retireve API --  All Record
        [HttpGet]
        [Route("GetList")]
        public async Task<IActionResult> GetList()
        {
            dynamic res;
            try {
                var empList = _DbContext.empModels.ToList();
                if (!empList.Any())
                {
                    res = new
                    {
                        message = @$"No Record",
                        state = false
                    };
                }
                else 
                {
                    res = new
                    {
                        result = empList,
                        state = true
                    };
                }
            }
            catch(Exception ex)
            {
                res = new
                {
                    message = ex.Message,
                    state = false
                };
            }
            return Ok(res);
        }

        // Delete API  -- All Record
        [HttpDelete]
        [Route("DeleteEmp")]
        public IActionResult delete(int id)
        {
            dynamic res;
            try
            {
                using(var _conn = new SqlConnection(_connectionString))
                {
                    var empid = _DbContext.empModels.Find(id);
                    var empVal = empid;
                    if (empid == null)
                    {
                        res = new
                        {
                            state = false,
                            message = "Record is not avilable"
                        };
                    }
                    else
                    {
                        _DbContext.empModels.Remove(empid);
                        _DbContext.SaveChanges();
                        var query = $@"delete LoginTokenRec where where EmpId = {empid}";
                        var ress = _conn.ExecuteAsync(query);
                        res = new
                        {
                            state = true,
                            result = empVal
                        };
                    }
                    
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message
                };
            }
            return Ok(res);
        }

        // Update API  -- Update User
        [HttpPut]
        [Route("Update")]
        public IActionResult update(int id, string name, string email, string pwd, string cont, string role)
        {
            //var hashingPwd = _empService.ComputeSha256Hash(pwd);
            dynamic res;
            try
            {
                var idmain = _DbContext.empModels.Find(id);
                if (idmain == null)
                {
                    res = new
                    {
                        state = false,
                        message = "Record not Find"
                    };
                }
                else
                {
                    idmain.EmpName = name;
                    idmain.EmpEmail = email;
                    idmain.password = pwd;
                    idmain.contact = cont;
                    idmain.role = role;
                    _DbContext.SaveChanges();
                    res = new
                    {
                        state = true,
                        message = @$"Record saved {email}"
                    };
                }
                
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message
                };
            }
            return Ok(res);
        }

        // Check does Login oR not
        [HttpGet]
        [Route("LoginForm")]
        public async Task<IActionResult> get(string uname, string pwd)
        {
            dynamic res;
            bool isVerify = true;
            try
            { 
                var token = string.Empty;
                //checked validation
                if (string.IsNullOrWhiteSpace(uname) || string.IsNullOrWhiteSpace(pwd))
                {
                    res = new
                    {
                        state = false,
                        message = "Please Fill Details"
                    };
                }

                var ReturnDetails = _DbContext.empModels.FirstOrDefault(x => x.EmpEmail == uname);
                if (ReturnDetails == null)
                {
                    res = new
                    {
                        state = false,
                        message = "Email not found.",
                    };
                }
                else
                {

                    if (ReturnDetails.password != pwd)

                    {
                        res = new
                        {
                            state = false,
                            message = $@"Password not Match with {uname}"
                        };
                    }
                    else
                    {
                        token = _jwtTokenGen.GenerateJwtToken(ReturnDetails);   
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
                            qurry = $@"update LoginTokenRec set Token='{TokenMain}',Expiry='{expiryTimeLocal2}',Role='{role}', isLoggedIn=1 where EmpId = {EmpId}";
                            var result = await _conn.ExecuteAsync(qurry);
                        }
                        res = new
                        {
                            state = true,
                            message = $"Login Success",
                            token = token,
                        };
                    }
                }
            }
            catch(Exception ex) {
                res = new
                {
                    state = false,
                    message = ex.Message
                };
            }

            bool currentState = res.state;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }
    
    
    }
}
