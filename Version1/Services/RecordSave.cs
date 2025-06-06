using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;
using TesseractOCR.Renderers;
using Version1.Modal;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SQCScanner.Services
{
    public class RecordSave
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly string IWebHostEnvironment;
        public RecordSave(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("dbc")!;
        }


        // Save Record into DB 
        public async Task<Dictionary<string, string>> RecordSaveVal(OmrResult respose, int templateId, string userName)
        {
            Dictionary<string, string> records = new Dictionary<string, string>();
            var Query = "";
            dynamic res;
            dynamic result;
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // check Table exist or not
                    string tableName = $"Template_{templateId}";
                    string checkTableSql = @"SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
                    var exists = await connection.QueryFirstOrDefaultAsync<int?>(checkTableSql, new { TableName = tableName });
                    bool tableExists = exists.HasValue;

                    // Insert record into Distnory. 
                    records.Add("LiveTime", DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt"));
                    records.Add("UserName", userName.ToString());
                    var fields2 = respose.FieldResults.ToDictionary(fr => fr.Key, dl => dl.Value);
                    foreach (var kvp in fields2)
                    {
                        records.Add(kvp.Key, kvp.Value);
                    }

                    if (tableExists)
                    {
                        // Before save some record we will. (check FileName) 
                        var isFileCheck = respose.FieldResults.Where(x => x.Key == "FileName").ToList();
                        var isFileCheck2 = isFileCheck[0].Value;
                        var imgReName = Path.GetFileName(isFileCheck2);   //
                        
                        // Does Record is Exist or Not.
                        var RecordExis = $"Select * from {tableName} where FileName = '{isFileCheck2}'";
                        var isRes = connection.Query(RecordExis).ToList();

                        if (isRes.Count >= 1)  // Record Update   _ Problem Will be = jab koi record(column ki rows) pahle se add ho gya hai or us record ko mene apne main scan response se hata diya hai wo update k case me hatega ni pahle waha record db se save rahega.
                        {
                            var updateQuery = $@"UPDATE [{tableName}] SET {string.Join(", ", records.Select(kvp => $"[{kvp.Key}] = '{kvp.Value}'"))} WHERE [FileName] = '{isFileCheck2}'";
                            result = await connection.ExecuteAsync(updateQuery);
                        
                            //var setFile = $"update {tableName} SET [FileName]='{imgReName}'";
                            //connection.Query(setFile);
                            res = new
                            {
                                res = records,
                                status = true
                            };
                        }
                        else    // Record Fresh
                        {
                            var insertQuery = $@"INSERT INTO {tableName} ({string.Join(", ", records.Select(a => $"[{a.Key}]"))}) VALUES ({string.Join(", ", records.Select(c => "'" + c.Value + "'"))})";
                            result = connection.Query(insertQuery);

                            //var setFile = $"update {tableName} SET [FileName]='{imgReName}' ";
                            //result = connection.Query(setFile);
                            res = new
                            {
                                res = records,
                                status = true
                            };
                        }
                    }
                    else
                    {
                        res = new
                        {
                            res = "Table is not Available",
                            status = false
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    res = ex.Message,
                    status = false
                };
            }

            return records;
        }



    }
}
