using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public userAuth(ApplicationDbContext DbContext, JwtAuth Jwt, EncptDcript EncDec)
        {
            _DbContext = DbContext;
            _jwtTokenGen = Jwt;
            _EncptDcript = EncDec;
        }

        // Add API --  
        [HttpPost("SignUp")]
        public IActionResult Add(string name, string email, string pwd, string cont, string role)
        {
            dynamic res;
            try
            {
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pwd) || string.IsNullOrEmpty(cont) || string.IsNullOrEmpty(role))
                {
                    return BadRequest("Please Fill Every Information");
                }
                EmpModel newEmp = new EmpModel
                {
                    EmpName = name,
                    EmpEmail = email,
                    password = pwd,
                    contact = cont,
                    role = role
                };
                _DbContext.empModels.Add(newEmp);
                _DbContext.SaveChanges();
                res = new
                {
                    message = "User Created Successfully",
                    status = true,
                    data = newEmp
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
            // Get all data from EmpClass
            var empList = _DbContext.empModels.ToList();
            if (empList == null)
            {
                BadRequest("No Data Found");
            }
            else if (empList.Any())
            {
                //var fs = empList.First();
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
                var empid = _DbContext.empModels.Find(id);
                var empVal = empid;
                if (empid == null)
                {
                    BadRequest("Record is not avilable");
                }
                _DbContext.empModels.Remove(empid);
                _DbContext.SaveChanges();
                return Ok(empVal + "Has Been Removed");
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
            token = _jwtTokenGen.GenerateJwtToken(ReturnDetails);
            return Ok(new
            {
                message = $"Login Success",
                token = token,
            });
        }
    }
}
