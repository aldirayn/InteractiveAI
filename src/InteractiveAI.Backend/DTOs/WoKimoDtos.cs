using System.Text.Json.Serialization;

namespace InteractiveAI.Backend.DTOs;

public class GetDataBengkelKimoRequest
{
    [JsonPropertyName("srdnumber")]
    public string? Srdnumber { get; set; }
}

public class KimoPerbaikanBengkelDetailResponse
{
    [JsonPropertyName("NoPol")]
    public string? NoPol { get; init; }

    [JsonPropertyName("JenisTruck")]
    public string? JenisTruck { get; init; }

    [JsonPropertyName("KodeDriverLama")]
    public string? KodeDriverLama { get; init; }

    [JsonPropertyName("NamaDriver")]
    public string? NamaDriver { get; init; }

    [JsonPropertyName("SRDNumber")]
    public string? SRDNumber { get; init; }

    [JsonPropertyName("LastStatus")]
    public string? LastStatus { get; init; }

    [JsonPropertyName("JenisPerbaikan")]
    public string? JenisPerbaikan { get; init; }

    [JsonPropertyName("DescPerbaikan")]
    public string? DescPerbaikan { get; init; }

    [JsonPropertyName("KodeDriverBaru")]
    public string? KodeDriverBaru { get; init; }

    [JsonPropertyName("OddoKm")]
    public string? OddoKm { get; init; }

    [JsonPropertyName("TujuanPoolPerbaikan")]
    public string? TujuanPoolPerbaikan { get; init; }
}

public class ActorProfileVM
{
    public string? Username { get; set; }
    public int? DefaultPoolId { get; set; }
    public string? DefaultPoolName { get; set; }
}

public class GetDataBengkelKimoResponse
{
    public string? Nopol { get; set; }
    public string? JenisTruk { get; set; }
    public string? KodeDriver { get; set; }
    public string? DriverName { get; set; }
    public string? DriverId { get; set; }
    public decimal? OdometerKm { get; set; }
    public string? TujuanService { get; set; }
    public string? NoMsr { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Requestor { get; set; }
    public DateTimeOffset? SubmitTime { get; set; }
    public string? NoWO { get; set; }
    public string? NoHP { get; set; }
}
