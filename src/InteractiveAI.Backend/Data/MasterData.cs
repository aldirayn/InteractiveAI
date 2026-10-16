namespace InteractiveAI.Backend.Data;

public class VehicleUnit
{
    public long Id { get; set; }
    public string Nopol { get; set; } = string.Empty;
    public string NopolNorm { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public string TruckType { get; set; } = string.Empty;
}

public class DriverPairing
{
    public long Id { get; set; }
    public string NopolNorm { get; set; } = string.Empty;
    public string DriverCode { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}
