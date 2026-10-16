using InteractiveAI.Backend.Config;
using InteractiveAI.Backend.Data;
using InteractiveAI.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{

    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "InteractiveAI Backend",
        Version = "v1"
    });
});

builder.Services.Configure<OllamaOptions>(o =>
{
    builder.Configuration.GetSection("Ollama").Bind(o);

    var envKey = Environment.GetEnvironmentVariable("OLLAMA_API_KEY");
    if (!string.IsNullOrWhiteSpace(envKey)) o.ApiKey = envKey;
});

var ollamaUrl = builder.Configuration["Ollama:BaseUrl"] ?? "https://ollama.com";
var timeoutSec = int.TryParse(builder.Configuration["Ollama:TimeoutSeconds"], out var t) ? t : 120;

builder.Services.AddHttpClient<IOllamaService, OllamaService>(c =>
{
    c.BaseAddress = new Uri(ollamaUrl);
    c.Timeout = TimeSpan.FromSeconds(timeoutSec);
});

builder.Services.Configure<VisionOptions>(builder.Configuration.GetSection("Vision"));
builder.Services.AddSingleton<IVisionService, Yolo11VisionService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("master");
builder.Services.Configure<MasterOptions>(builder.Configuration.GetSection("Master"));
builder.Services.Configure<WoOptions>(builder.Configuration.GetSection("Wo"));
builder.Services.Configure<VoiceOptions>(builder.Configuration.GetSection("Voice"));
builder.Services.Configure<SttOptions>(builder.Configuration.GetSection("Voice:Stt"));
builder.Services.AddSingleton<HushDenoiseService>();
builder.Services.AddSingleton<WhisperSttService>();

builder.Services.Configure<VoiceOptions>(o =>
{
    var elevenKey = Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY");
    if (!string.IsNullOrWhiteSpace(elevenKey)) o.ElevenLabs.ApiKey = elevenKey;
});
var voiceProvider = builder.Configuration["Voice:Provider"] ?? "ElevenLabs";
switch (voiceProvider.ToLowerInvariant())
{
    case "vits":
        builder.Services.AddSingleton<IVoiceService, VitsTtsService>();
        break;
    case "local":
        builder.Services.AddSingleton<IVoiceService, MmsTtsService>();
        break;
    case "elevenlabs":
        builder.Services.AddSingleton<ElevenLabsTtsService>();

        builder.Services.AddSingleton<MmsTtsService>();
        builder.Services.AddSingleton<VitsTtsService>();
        builder.Services.AddSingleton<IVoiceService, FallbackVoiceService>();
        break;
    default:
        builder.Services.AddSingleton<IVoiceService, EdgeTtsService>();
        break;
}

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));
var dbProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var useMysql = dbProvider.Equals("Mysql", StringComparison.OrdinalIgnoreCase);
if (useMysql)
{

    var mysqlConn = Environment.GetEnvironmentVariable("MYSQL_CONNECTION")
        ?? builder.Configuration.GetConnectionString("Mysql")
        ?? "server=127.0.0.1;port=3306;database=interactiveai;user=root;password=12345678;SslMode=none;CharSet=utf8mb4";
    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseMySql(mysqlConn, new MySqlServerVersion(new Version(5, 7, 44))));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=chat.db"));
}

builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IWoService, WoService>();
builder.Services.AddScoped<IMasterService, MasterService>();


var mkaConn = Environment.GetEnvironmentVariable("MKAPOOL_CONNECTION")
    ?? builder.Configuration.GetConnectionString("MkaPool")
    ?? "";
builder.Services.AddDbContext<MkaPoolContext>(o =>
{
    if (string.IsNullOrWhiteSpace(mkaConn))
        throw new InvalidOperationException(
            "Koneksi MkaPool belum dikonfigurasi (MkaPool:ConnectionString / MKAPOOL_CONNECTION).");
    o.UseMySQL(mkaConn);
});


var kacsConn = Environment.GetEnvironmentVariable("KACSGPS_CONNECTION")
    ?? builder.Configuration.GetConnectionString("Kagt")
    ?? builder.Configuration["KacsGps:ConnectionString"]
    ?? "";
builder.Services.Configure<KacsGpsOptions>(o => o.ConnectionString = kacsConn);
builder.Services.AddDbContext<KacsGpsContext>(o =>
{
    o.UseMySQL(string.IsNullOrWhiteSpace(kacsConn)
        ? "server=127.0.0.1;port=3306;database=kacs_gps_placeholder;uid=root;pwd=;CharSet=utf8mb4"
        : kacsConn);
});
builder.Services.AddScoped<IQrKimoService, QrKimoService>();

builder.Services.AddCors(o => o.AddPolicy("frontend", p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("X-Voice-Id")));

var app = builder.Build();


{
    static string Part(string conn, string key)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            conn ?? "", @"(?i)\b" + key + @"\s*=\s*([^;]+)");
        return m.Success ? m.Groups[1].Value.Trim() : "?";
    }
    app.Logger.LogInformation("KacsGps -> server={Server} database={Db}",
        Part(kacsConn, "server"), Part(kacsConn, "database"));
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (useMysql) InitMysql(db);
    else InitSqlite(db);
}


static bool TableExists(AppDbContext db, string table, bool useMysql)
{
    var conn = db.Database.GetDbConnection();
    var wasOpen = conn.State == ConnectionState.Open;
    if (!wasOpen) conn.Open();
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = useMysql
            ? "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '" + table + "'"
            : "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = \"" + table + "\"";
        using var reader = cmd.ExecuteReader();
        return reader.Read();
    }
    finally { if (!wasOpen) conn.Close(); }
}

static HashSet<string> ExistingColumns(AppDbContext db, string table, bool useMysql)
{
    var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var conn = db.Database.GetDbConnection();
    var wasOpen = conn.State == ConnectionState.Open;
    if (!wasOpen) conn.Open();
    try
    {
        using var cmd = conn.CreateCommand();

        cmd.CommandText = useMysql
            ? "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '" + table + "'"
            : "PRAGMA table_info(\"" + table + "\")";
        using var reader = cmd.ExecuteReader();
        var idx = useMysql ? 0 : 1;
        while (reader.Read()) cols.Add(reader.GetString(idx));
    }
    finally { if (!wasOpen) conn.Close(); }
    return cols;
}

static void EnsureTable(AppDbContext db, string table, bool useMysql, string createSql)
{
    if (!TableExists(db, table, useMysql)) db.Database.ExecuteSqlRaw(createSql);
}

static void EnsureColumn(AppDbContext db, string table, string column, bool useMysql, string addSql)
{
    if (!ExistingColumns(db, table, useMysql).Contains(column)) db.Database.ExecuteSqlRaw(addSql);
}

static void InitSqlite(AppDbContext db)
{
    const bool mysql = false;

    EnsureTable(db, "WorkOrders", mysql, """
        CREATE TABLE "WorkOrders" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_WorkOrders" PRIMARY KEY AUTOINCREMENT,
            "WoNumber" TEXT NOT NULL,
            "SessionId" TEXT NOT NULL,
            "Model" TEXT NOT NULL,
            "DriverName" TEXT NOT NULL,
            "UnitNumber" TEXT NOT NULL,
            "Complaint" TEXT NOT NULL,
            "QrKimo" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            "CreatedAt" TEXT NOT NULL
        );
        """);
    EnsureTable(db, "VehicleUnits", mysql, """
        CREATE TABLE "VehicleUnits" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_VehicleUnits" PRIMARY KEY AUTOINCREMENT,
            "Nopol" TEXT NOT NULL,
            "NopolNorm" TEXT NOT NULL,
            "Active" INTEGER NOT NULL,
            "TruckType" TEXT NOT NULL
        );
        """);
    EnsureTable(db, "DriverPairings", mysql, """
        CREATE TABLE "DriverPairings" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_DriverPairings" PRIMARY KEY AUTOINCREMENT,
            "NopolNorm" TEXT NOT NULL,
            "DriverCode" TEXT NOT NULL,
            "DriverName" TEXT NOT NULL,
            "FullName" TEXT NOT NULL
        );
        """);

    db.Database.ExecuteSqlRaw(
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_VehicleUnits_NopolNorm\" ON \"VehicleUnits\" (\"NopolNorm\")");
    db.Database.ExecuteSqlRaw(
        "CREATE INDEX IF NOT EXISTS \"IX_DriverPairings_NopolNorm\" ON \"DriverPairings\" (\"NopolNorm\")");

    EnsureColumn(db, "WorkOrders", "DriverCode", mysql,
        "ALTER TABLE \"WorkOrders\" ADD COLUMN \"DriverCode\" TEXT NOT NULL DEFAULT ''");
    EnsureColumn(db, "WorkOrders", "RepairCategories", mysql,
        "ALTER TABLE \"WorkOrders\" ADD COLUMN \"RepairCategories\" TEXT NOT NULL DEFAULT ''");
    EnsureColumn(db, "WorkOrders", "ParkingLocation", mysql,
        "ALTER TABLE \"WorkOrders\" ADD COLUMN \"ParkingLocation\" TEXT NOT NULL DEFAULT ''");
}

static void InitMysql(AppDbContext db)
{

    const bool mysql = true;
    EnsureTable(db, "Sessions", mysql, """
        CREATE TABLE `Sessions` (
            `Id` CHAR(36) NOT NULL PRIMARY KEY,
            `Title` VARCHAR(200) NOT NULL,
            `Model` VARCHAR(100) NOT NULL,
            `CreatedAt` DATETIME(6) NOT NULL
        )
        """);
    EnsureTable(db, "Messages", mysql, """
        CREATE TABLE `Messages` (
            `Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `SessionId` CHAR(36) NOT NULL,
            `Role` VARCHAR(20) NOT NULL,
            `Content` LONGTEXT NOT NULL,
            `CreatedAt` DATETIME(6) NOT NULL,
            INDEX `IX_Messages_SessionId` (`SessionId`)
        )
        """);
    EnsureTable(db, "WorkOrders", mysql, """
        CREATE TABLE `WorkOrders` (
            `Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `WoNumber` VARCHAR(255) NOT NULL,
            `SessionId` CHAR(36) NOT NULL,
            `Model` VARCHAR(255) NOT NULL,
            `DriverName` TEXT NOT NULL,
            `DriverCode` TEXT NOT NULL,
            `UnitNumber` TEXT NOT NULL,
            `RepairCategories` TEXT NOT NULL,
            `Complaint` TEXT NOT NULL,
            `ParkingLocation` TEXT NOT NULL,
            `QrKimo` TEXT NOT NULL,
            `Status` TEXT NOT NULL,
            `CreatedAt` DATETIME(6) NOT NULL
        )
        """);
    EnsureTable(db, "VehicleUnits", mysql, """
        CREATE TABLE `VehicleUnits` (
            `Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `Nopol` TEXT NOT NULL,
            `NopolNorm` VARCHAR(50) NOT NULL,
            `Active` TINYINT(1) NOT NULL,
            `TruckType` TEXT NOT NULL,
            UNIQUE INDEX `IX_VehicleUnits_NopolNorm` (`NopolNorm`)
        )
        """);
    EnsureTable(db, "DriverPairings", mysql, """
        CREATE TABLE `DriverPairings` (
            `Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `NopolNorm` VARCHAR(50) NOT NULL,
            `DriverCode` TEXT NOT NULL,
            `DriverName` TEXT NOT NULL,
            `FullName` TEXT NOT NULL,
            INDEX `IX_DriverPairings_NopolNorm` (`NopolNorm`)
        )
        """);

    EnsureColumn(db, "WorkOrders", "DriverCode", mysql,
        "ALTER TABLE `WorkOrders` ADD COLUMN `DriverCode` TEXT NOT NULL");
    EnsureColumn(db, "WorkOrders", "RepairCategories", mysql,
        "ALTER TABLE `WorkOrders` ADD COLUMN `RepairCategories` TEXT NOT NULL");
    EnsureColumn(db, "WorkOrders", "ParkingLocation", mysql,
        "ALTER TABLE `WorkOrders` ADD COLUMN `ParkingLocation` TEXT NOT NULL");
    EnsureMysqlFk(db);
}

static void EnsureMysqlFk(AppDbContext db)
{
    var conn = db.Database.GetDbConnection();
    var wasOpen = conn.State == ConnectionState.Open;
    if (!wasOpen) conn.Open();
    try
    {
        bool exists;
        using (var cmd = conn.CreateCommand())
        {
            // Cek FK apa pun dari Messages.SessionId -> Sessions, apa pun namanya
            // (EnsureCreated milik EF biasanya memberi nama FK_Messages_Sessions_SessionId)
            cmd.CommandText =
                "SELECT 1 FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE " +
                "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Messages' " +
                "AND COLUMN_NAME = 'SessionId' AND REFERENCED_TABLE_NAME = 'Sessions' LIMIT 1";
            using var reader = cmd.ExecuteReader();
            exists = reader.Read();
        } // reader & cmd ditutup di sini

        if (!exists)
            db.Database.ExecuteSqlRaw(
                "ALTER TABLE `Messages` ADD CONSTRAINT `FK_Messages_Sessions` " +
                "FOREIGN KEY (`SessionId`) REFERENCES `Sessions` (`Id`) ON DELETE CASCADE");
    }
    finally { if (!wasOpen) conn.Close(); }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("frontend");
app.UseAuthorization();
app.MapControllers();


_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var voice = scope.ServiceProvider.GetRequiredService<IVoiceService>();
        foreach (var daypart in new[] { "pagi", "siang", "sore", "malam" })
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await voice.SpeakAsync(
                $"Selamat {daypart}, saya Mira dari loket WO. Dengan abang siapa saya berbicara?",
                "id-ID-GadisNeural", 0, cts.Token);
        }
    }
    catch {  }
});

app.Run();
