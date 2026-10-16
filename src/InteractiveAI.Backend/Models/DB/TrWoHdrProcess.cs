using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteractiveAI.Backend.Data;

public partial class TrWoHdrProcess
{
    public string PreSrNo { get; set; } = null!;

    public string PreSrcNo { get; set; } = null!;

    public string PreRedoNo { get; set; } = null!;

    public DateTime PreWoDate { get; set; }

    public DateTime PreWoTime { get; set; }

    public DateTime PreWoEndDate { get; set; }

    public DateTime PreWoEndTime { get; set; }

    public string PreUnitId { get; set; } = null!;

    public string PreUnitNo { get; set; } = null!;

    public string PreJenis { get; set; } = null!;

    public string PreMerk { get; set; } = null!;

    public string PreModel { get; set; } = null!;

    public string PreReqBy { get; set; } = null!;

    public string PreDriverId { get; set; } = null!;

    public string PreDriverName { get; set; } = null!;

    public string PrePriorityId { get; set; } = null!;

    public string PrePriorityName { get; set; } = null!;

    public string PrePriorityDesc { get; set; } = null!;

    public DateTime PreLastMaintenDate { get; set; }

    public DateTime PreLastMaintenTime { get; set; }

    public decimal PreLastKmActual { get; set; }

    public short PreStatusGps { get; set; }

    public DateTime PreLastUpdate { get; set; }

    public DateTime PreLastUpdateTime { get; set; }

    public decimal PreKmGps { get; set; }

    public decimal? PreHmGps { get; set; }

    public decimal? PreKmActual { get; set; }

    public decimal? PreHmActual { get; set; }

    public string PrePerbaikanDesc { get; set; } = null!;

    public string PreKeluhan1 { get; set; } = null!;

    public string PreKeluhan2 { get; set; } = null!;

    public string PreKeluhan3 { get; set; } = null!;

    public string PreKeluhan4 { get; set; } = null!;

    public string PreKeluhan5 { get; set; } = null!;

    public string PreKeluhan6 { get; set; } = null!;

    public string PreKeluhan7 { get; set; } = null!;

    public string PreKeluhan8 { get; set; } = null!;

    public string PreKeluhan9 { get; set; } = null!;

    public string PreKeluhan10 { get; set; } = null!;

    public string PreKeluhan11 { get; set; } = null!;

    public string PreKeluhan12 { get; set; } = null!;

    public int? PrePoolId { get; set; } = null!;

    public string PrePoolName { get; set; } = null!;

    public string PreRemarksPool { get; set; } = null!;

    public string PreBayId { get; set; } = null!;

    public string PreBayName { get; set; } = null!;

    public string PreSaranPm { get; set; } = null!;

    public string PreEstimasi { get; set; } = null!;

    public DateTime PreEstimasiDate { get; set; }

    public DateTime PreEstimasiTime { get; set; }

    public int PreRev { get; set; }

    public string PreRemarks { get; set; } = null!;

    public int PreStatus { get; set; }

    public string Interid { get; set; } = null!;

    public string Crusrid { get; set; } = null!;

    public DateTime Creatddt { get; set; }

    public string Mdfusrid { get; set; } = null!;

    public DateTime Modifdt { get; set; }

    public short PreRejectType { get; set; }

    public string PreReject { get; set; } = null!;

    public DateTime Createtime { get; set; }

    public string PreJnsPerbaikan { get; set; } = null!;

    public string PreGroup { get; set; } = null!;

    public string PreSubJobId { get; set; } = null!;

    public string PreSubJobDesc { get; set; } = null!;

    public string SubJobDurationJam { get; set; } = null!;

    public string SubJobDurationMenit { get; set; } = null!;

    public DateTime PreLastQcDate { get; set; }

    public DateTime PreLastQcTime { get; set; }

    public decimal PreLastKmGps { get; set; }

    public decimal PreLastHmActual { get; set; }

    public decimal PreLastHmGps { get; set; }

    public decimal PreLastQcKmGps { get; set; }

    public decimal PreLastQcKmActual { get; set; }

    public decimal PreLastQcHmGps { get; set; }

    public decimal PreLastQcHmActual { get; set; }

    public short PreOhEngine { get; set; }
    public short PreQuickCheck { get; set; }
    public short PreExternal { get; set; }

    public short Trxtype { get; set; }

    public DateTime Modiftime { get; set; }

    public int DexRowId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public string? PreRevRemarks { get; set; }
    public int? DeskId { get; set; }
    public int? IsToday { get; set; }
    public string? IsTodayNote { get; set; }
    public string? NoHpDrv { get; set; }


    public string? GolInsentif { get; set; }
    public string? LokasiParkir { get; set; }
}
