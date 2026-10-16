namespace InteractiveAI.Backend.Data;

public partial class ErrLog
{
    public long Id { get; set; }

    public string? Modul { get; set; }

    public string? User { get; set; }

    public string? IdData { get; set; }

    public string? ErrMessage { get; set; }

    public DateTime? CreatedDt { get; set; }

    public string? FunctionName { get; set; }
}
