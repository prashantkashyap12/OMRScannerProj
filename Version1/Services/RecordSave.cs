using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Syncfusion.EJ2.Linq;
using TesseractOCR.Renderers;
using Version1.Modal;
using static System.Runtime.InteropServices.JavaScript.JSType;
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
                    records.Add("Status", respose.Success.ToString());
                    var fields2 = respose.FieldResults.ToDictionary(fr => fr.Key, dl => dl.Value);
                    if (respose.Success)
                    {
                        records.Add("Report", ""); // true k case me auto add ni aata.
                        foreach (var kvp in fields2)
                        {
                            records.Add(kvp.Key, kvp.Value);
                        }
                        foreach (var rec in records)
                        {
                            if (rec.Key == "Report")
                            {
                                records[rec.Key] = "N/A";
                            }
                        }
                    }
                    else
                    {
                        foreach(var filds in fields2)
                        {
                            records.Add(filds.Key, filds.Value);
                        }
                        // save 
                        Query = @$"select column_name from INFORMATION_SCHEMA.columns where table_name = @TableName";
                        var col = (await connection.QueryAsync<string>(Query, new {TableName = tableName})).Select(a=>a).ToHashSet();
                        foreach (var kvp in col)
                        {
                            if (kvp=="Id" || kvp== "LiveTime" || kvp == "UserName" || kvp == "FileName" || kvp == "Status" || kvp == "Report" )
                            {
                                continue;
                            }
                            else 
                            {
                                  records.Add(kvp, "error");
                            }
                        }
                    }

                    if (tableExists)
                    {
                        // Before save some record we will. (check FileName) 
                        var isFileCheck = respose.FieldResults.Where(x => x.Key == "FileName").ToList();
                        var isFileCheck2 = isFileCheck[0].Value;
                        var imgReName = Path.GetFileName(isFileCheck2);  // Extract fileName only to save futurely
                        // Does Record is Exist or Not.
                        var RecordExis = $"Select * from {tableName} where FileName = '{isFileCheck2}'";
                        var isRes = connection.Query(RecordExis).ToList();
                        
                        if (isRes.Count >= 1)  // Record Update _ Problem Will be = jab koi record(column ki rows) pahle se add ho gya hai or us record ko mene apne main scan response se hata diya hai wo update k case me hatega ni pahle waha record db se save rahega.
                        {
                            var updateQuery = $@"UPDATE [{tableName}] SET {string.Join(", ", records.Select(kvp => $"[{kvp.Key}] = '{kvp.Value}'"))} WHERE [FileName] = '{isFileCheck2}'";
                            result = await connection.ExecuteAsync(updateQuery);
                            res = new
                            {
                                res = records,
                                status = true
                            };
                        }
                        else
                        {
                            var insertQuery = $@"INSERT INTO {tableName} ({string.Join(", ", records.Select(a => $"[{a.Key}]"))}) VALUES ({string.Join(", ", records.Select(c => "'" + c.Value + "'"))})";
                            result = connection.Query(insertQuery);
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
