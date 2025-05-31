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
        public RecordSave(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("dbc")!;
        }
        public async Task<List<string>> RecordSaveVal(OmrResult respose, int templateId)
        {
            var saveRec = new List<string>();
            //var Query = "";
            //dynamic res;
            //try
            //{
            //    using (var connection = new SqlConnection(_connectionString))
            //    {
            //        connection.Open();
            //        string tableName = $"Template_{templateId}";
            //        string checkTableSql = @"SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
            //        var exists = await connection.QueryFirstOrDefaultAsync<int?>(checkTableSql, new { TableName = tableName });
            //        bool tableExists = exists.HasValue;
            //        var fields = respose.FieldResults.Select(fr => fr.Value).Distinct().ToList();
            //        saveRec.AddRange(fields);
            //        if (tableExists)
            //        {
            //            Query = @$"select * from {tableName}";
            //            var result = connection.Query(Query).ToList();

            //            Query = @$"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}';";
            //            var result2 = connection.Query(Query).ToList();
            //            var columns = result2.Select(c => c.COLUMN_NAME).ToList();
            //            var insertQuery = $@"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ('asdf', 'fdsa' , 'zxcv', {string.Join(", ", fields.Select(c => "'"+c+"'"))})";
            //            connection.Query(insertQuery);
            //            res = new
            //            {
            //                res = result,
            //                status = true
            //            };
            //        }
            //        else
            //        {
            //            res = new
            //            {
            //                res = "Table is not Available",
            //                status = false
            //            };
            //        }
            //    }
            //}catch(Exception ex)
            //{
            //    res = new
            //    {
            //        res = ex.Message,
            //        status = false
            //    };
            //}

            return saveRec;
        }
    }
}
