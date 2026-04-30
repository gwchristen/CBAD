using System.Data.OleDb;
using CBAD.Reporting;

namespace CBAD.Data;

internal static class DatabaseManager
{
    private const string DefaultDbName = "CBAD_Records.accdb";

    public static string GetDefaultDbPath() =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultDbName);

    public static string GetConnectionString(string path) =>
        $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={path};Persist Security Info=False;";

    public static void GenerateDatabase(string filePath)
    {
        if (File.Exists(filePath))
            throw new InvalidOperationException("A database already exists at the target location.");

        try
        {
            Type? type = Type.GetTypeFromProgID("ADOX.Catalog");
            if (type == null)
            {
                throw new InvalidOperationException(
                    "The ADOX.Catalog COM object was not found. Please ensure the Microsoft Access Database Engine (ACE) is installed.");
            }

            dynamic catalog = Activator.CreateInstance(type)!;
            catalog.Create(GetConnectionString(filePath));

            // Initialize the schema
            using var connection = new OleDbConnection(GetConnectionString(filePath));
            connection.Open();
            using var command = connection.CreateCommand();

            command.CommandText = @"
                CREATE TABLE TestRecords (
                    Id AUTOINCREMENT PRIMARY KEY,
                    Station VARCHAR(255),
                    WorkOrder VARCHAR(255),
                    BatterySerial VARCHAR(255),
                    VisualPass BIT,
                    DateCaptured DATETIME,
                    FinalStatus VARCHAR(255),
                    ProcessCode VARCHAR(255),
                    TargetCapacity VARCHAR(255),
                    FinalVoltage VARCHAR(255),
                    FinalCurrent VARCHAR(255),
                    FinalHealth VARCHAR(255),
                    Resistance VARCHAR(255),
                    Notes MEMO,
                    ChartImageBase64 MEMO
                )";
            command.ExecuteNonQuery();
        }
        catch (Exception)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
            throw;
        }
    }

    public static void BackupDatabase(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("No database file exists to backup. Please generate one first.");

        string directory = Path.GetDirectoryName(sourcePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        string backupName = $"CBAD_Records_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.accdb";
        string backupPath = Path.Combine(directory, backupName);

        File.Copy(sourcePath, backupPath, overwrite: true);
    }

    public static void ArchiveDatabase(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("No database file exists to archive. Please generate one first.");

        string directory = Path.GetDirectoryName(sourcePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        string archiveName = $"CBAD_Records_Archive_{DateTime.Now:yyyyMMdd_HHmmss}.accdb";
        string archivePath = Path.Combine(directory, archiveName);

        File.Move(sourcePath, archivePath);

        // Generate a fresh empty database in place of the archived one
        GenerateDatabase(sourcePath);
    }

    public static void SaveRecord(string dbPath, ReportData data)
    {
        if (!File.Exists(dbPath))
        {
            throw new FileNotFoundException("The database file does not exist. Please go to Records > Generate Database first.");
        }

        using var connection = new OleDbConnection(GetConnectionString(dbPath));
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO TestRecords (
                Station, WorkOrder, BatterySerial, VisualPass, 
                DateCaptured, FinalStatus, ProcessCode, TargetCapacity, 
                FinalVoltage, FinalCurrent, FinalHealth, Resistance, 
                Notes, ChartImageBase64
            ) VALUES (
                @Station, @WorkOrder, @BatterySerial, @VisualPass, 
                @DateCaptured, @FinalStatus, @ProcessCode, @TargetCapacity, 
                @FinalVoltage, @FinalCurrent, @FinalHealth, @Resistance, 
                @Notes, @ChartImageBase64
            )";

        command.Parameters.AddWithValue("@Station", data.Station ?? string.Empty);
        command.Parameters.AddWithValue("@WorkOrder", data.WorkOrder ?? string.Empty);
        command.Parameters.AddWithValue("@BatterySerial", data.BatterySerial ?? string.Empty);
        command.Parameters.AddWithValue("@VisualPass", data.PassedVisualInspection);
        command.Parameters.AddWithValue("@DateCaptured", string.IsNullOrEmpty(data.Date) ? DateTime.Now : DateTime.Parse(data.Date).ToLocalTime());
        command.Parameters.AddWithValue("@FinalStatus", data.FinalStatus ?? string.Empty);
        command.Parameters.AddWithValue("@ProcessCode", data.ProcessCode ?? string.Empty);
        command.Parameters.AddWithValue("@TargetCapacity", data.TargetCapacity ?? string.Empty);
        command.Parameters.AddWithValue("@FinalVoltage", data.FinalVoltage ?? string.Empty);
        command.Parameters.AddWithValue("@FinalCurrent", data.FinalCurrent ?? string.Empty);
        command.Parameters.AddWithValue("@FinalHealth", data.FinalHealth ?? string.Empty);
        command.Parameters.AddWithValue("@Resistance", data.Resistance ?? string.Empty);
        command.Parameters.AddWithValue("@Notes", data.Notes ?? string.Empty);
        command.Parameters.AddWithValue("@ChartImageBase64", data.ChartImageBase64 ?? string.Empty);

        command.ExecuteNonQuery();
    }
}