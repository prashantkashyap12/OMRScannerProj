using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using Version1.Modal;

namespace SQCScanner.Services
{
    public class RecordDBClass
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;


        public RecordDBClass(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("dbc")!;
        }
        public async Task<List<string>> TableCreation(string imagePath, string templatePath)
        {

            var fieldNames = new List<string>(); // Move it outside the using block so it's accessible at the end

            // String JSON TEXT
            var templateJson = File.ReadAllText(templatePath);
            // return to parsal from jsonTemp.
            var template = JObject.Parse(templateJson);

            // is Table exist or not into DB. table Id <Img + Json + tempName >
            var ImageName = Path.GetFileName(imagePath);
            var templateName = Path.GetFileName(templatePath);
            var tableName = template["name"]?.ToString() ?? "UnknownTable";
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                string checkTableSql = @" SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
                var exists = await connection.QueryFirstOrDefaultAsync<int?>(checkTableSql, new { TableName = tableName });
                bool tableExists = exists.HasValue;
                // Extract the field names from JSON
                if (tableExists)
                {
                    try
                    {
                        // Get the list of existing columns in the table
                        var existingColumns = await connection.QueryAsync<string>(@"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName", new { TableName = tableName });
                        var existingColumnNames = existingColumns.ToList();

                        
                        foreach (var field in template["fields"]!)
                        {
                            string fieldName = field["fieldName"]!.ToString();
                            fieldNames.Add(fieldName);
                        }
                        // Loop through the fields and add new columns that do not exist
                        var alterCommands = fieldNames.Where(fieldName => !existingColumnNames.Contains(fieldName)).Select(name => $"ALTER TABLE [{tableName}] ADD [{name}] VARCHAR(255);");
                        if (alterCommands.Any())
                        {
                            var fullSql = string.Join(Environment.NewLine, alterCommands);
                            await connection.ExecuteAsync(fullSql);
                            Console.WriteLine("New fields added successfully.");
                        }
                        else
                        {
                            Console.WriteLine("No new fields to add. All fields already exist.");
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"Error creating table '{ex.Message}'");
                    }
                }
                else
                {
                    try
                    {
                        
                        foreach (var field in template["fields"]!)
                        {
                            string fieldName = field["fieldName"]!.ToString();
                            fieldNames.Add(fieldName);
                        } 
                        // Build CREATE TABLE query
                        var columnsSql = string.Join(",\n", fieldNames.Select(name => $"[{name}] VARCHAR(255)"));
                        var createTableSql = $"CREATE TABLE [{tableName}] (\n[Id] INT IDENTITY(1,1) PRIMARY KEY,\n{columnsSql}\n);";
                        await connection.ExecuteAsync(createTableSql);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error creating table '{tableName}': {ex.Message}");
                    }
                }
            }
            return fieldNames;
        }
    }
}