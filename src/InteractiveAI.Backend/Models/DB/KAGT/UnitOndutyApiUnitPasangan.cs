using System;
using System.Collections.Generic;

namespace InteractiveAI.Backend.Data.Models.DB.KAGT;

public partial class UnitOndutyApiUnitPasangan
{
    public int Id { get; set; }

    public string? VehicleNo { get; set; }

    public string? JenisTruck { get; set; }

    public string? NamaDriver { get; set; }

    public string? StatusDriverKimo { get; set; }

    public string? KodeDriverBaru { get; set; }

    public string? KodeDriverLama { get; set; }

    public string? StatusUnitKimo { get; set; }

    public string? UnitKontrak { get; set; }

    public string? CustomerKontrakCode { get; set; }

    public string? CustomerKontrakNama { get; set; }

    public string? AlasanDriver { get; set; }

    public string? AlasanUnitTdkAktif { get; set; }

    public string? KetUnitTdkAktif { get; set; }

    public DateTime? DateStatusUnit { get; set; }

    public string? UserStatusUnit { get; set; }

    public bool? IsChecklistUnit { get; set; }

    public DateTime? TglChecklistUnit { get; set; }
}
