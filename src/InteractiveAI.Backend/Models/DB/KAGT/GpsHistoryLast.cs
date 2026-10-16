using System;
using System.Collections.Generic;

namespace InteractiveAI.Backend.Data.Models.DB.KAGT;

public partial class GpsHistoryLast
{
    public int Id { get; set; }

    public string? DeviceId { get; set; }

    public string? Plate { get; set; }

    public string? Vendor { get; set; }

    public string? VehicleType { get; set; }

    public decimal? Lat { get; set; }

    public decimal? Long { get; set; }

    public decimal? Temperature { get; set; }

    public int? EngineCar { get; set; }

    public int? EngineRef { get; set; }

    public int? Speed { get; set; }

    public DateTime? TimeGps { get; set; }

    public DateTime? TimeServer { get; set; }

    public DateTime? TimeLocal { get; set; }

    public string? Location { get; set; }

    public string? TokoTujuan { get; set; }

    public int? Mileage { get; set; }

    public decimal? Hourmeter { get; set; }

    public int? Heading { get; set; }

    public string? Geofence { get; set; }

    public string? KabKota { get; set; }

    public string? Prov { get; set; }

    public string? KategoriPendingin { get; set; }

    public string? KategoriBox { get; set; }

    public DateTime? Kir { get; set; }

    public DateTime? Stnk { get; set; }

    public decimal? Temperature2 { get; set; }

    public DateTime? LastPing { get; set; }

    public DateTime? LastPingHm { get; set; }

    public string? GeofenceNew { get; set; }
}
