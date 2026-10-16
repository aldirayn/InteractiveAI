using System;
using System.Collections.Generic;

namespace InteractiveAI.Backend.Data.Models.DB.KAGT;

public partial class Driver
{
    public int Id { get; set; }

    public int? IdStatus { get; set; }

    public string? KodeDriver { get; set; }

    public string? KodeDriverOld { get; set; }

    public DateTime? TglGabung { get; set; }

    public string? NoKtp { get; set; }

    public string? NamaDriver { get; set; }

    public string? NamaPangilan { get; set; }

    public string? TempatLahir { get; set; }

    public DateTime? TglLahir { get; set; }

    public int? IdJenisSim { get; set; }

    public string? NoSim { get; set; }

    public DateTime? TglBerlakuSim { get; set; }

    public string? NoTlp { get; set; }

    public string? NoHp1 { get; set; }

    public string? NoHp2 { get; set; }

    public string? Alamat { get; set; }

    public string? Rt { get; set; }

    public string? Rw { get; set; }

    public int? IdProvinsi { get; set; }

    public int? IdKabKota { get; set; }

    public int? IdKec { get; set; }

    public int? IdKel { get; set; }

    public string IsSameKtp { get; set; } = null!;

    public string? AlamatDomisili { get; set; }

    public string? RtDomisili { get; set; }

    public string? RwDomisili { get; set; }

    public int? IdProvinsiDomisili { get; set; }

    public int? IdKabKotaDomisili { get; set; }

    public int? IdKecDomisili { get; set; }

    public int? IdKelDomisili { get; set; }

    public string? Keterangan { get; set; }

    public string IsSms { get; set; } = null!;

    public string? Pathfoto { get; set; }

    public int? IdReferensiDriver { get; set; }

    public int? IdRef { get; set; }

    public string? HubunganRef { get; set; }

    public string? KeteranganRef { get; set; }

    public string IsKemitraan { get; set; } = null!;

    public string IsKemitraanAsli { get; set; } = null!;

    public string? UrlKemitraan { get; set; }

    public string IsJaminanKel { get; set; } = null!;

    public string IsJaminanKelAsli { get; set; } = null!;

    public string? UrlJaminanKel { get; set; }

    public string IsIjazah { get; set; } = null!;

    public string IsIjazahAsli { get; set; } = null!;

    public string? UrlIjazah { get; set; }

    public string IsBukuNikah { get; set; } = null!;

    public string IsBukuNikahAsli { get; set; } = null!;

    public string? UrlBukuNikah { get; set; }

    public string IsSkck { get; set; } = null!;

    public string IsSkckasli { get; set; } = null!;

    public string? UrlSkck { get; set; }

    public string IsDomisili { get; set; } = null!;

    public string IsDomisiliAsli { get; set; } = null!;

    public string? UrlDomisili { get; set; }

    public string IsKk { get; set; } = null!;

    public string IsKkasli { get; set; } = null!;

    public string? UrlKk { get; set; }

    public string IsKtp { get; set; } = null!;

    public string IsKtpasli { get; set; } = null!;

    public string? UrlKtp { get; set; }

    public string IsSim { get; set; } = null!;

    public string IsSimasli { get; set; } = null!;

    public string? UrlSim { get; set; }

    public int Urutan { get; set; }

    public decimal SaldoPiutang { get; set; }

    public decimal SaldoKlaim { get; set; }

    public string? LastPbyOid { get; set; }

    public DateTime? LastSync { get; set; }

    public int? IdSubStatus { get; set; }

    public string? IdAbsen { get; set; }

    public DateTime? TglStatus { get; set; }

    public string? Dmterima { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhoneNumber { get; set; }

    public string? EmergencyContactRelation { get; set; }

    public string? EmergencyContactKeterangan { get; set; }

    public string? EmergencyContactName2 { get; set; }

    public string? EmergencyContactPhoneNumber2 { get; set; }

    public string? EmergencyContactRelation2 { get; set; }

    public string? EmergencyContactKeterangan2 { get; set; }

    public string? ServerTmsenvironment { get; set; }

    public int? IdStatus2 { get; set; }

    public int? IdSubStatus2 { get; set; }

    public string? SubStatus2 { get; set; }

    public int? GeofenceId { get; set; }

    public int? ShowKpd { get; set; }

    public int? IdArea { get; set; }

    public int? IdSubArea { get; set; }

    public string? UrlAkteLahir { get; set; }

    public string? UrlSsvc { get; set; }

    public string? UrlSuratSehat { get; set; }

    public string? UrlSuratLamaran { get; set; }

    public string? AlamatLat { get; set; }

    public string? AlamatLong { get; set; }

    public string? IsAkteLahir { get; set; }

    public string? IsAkteLahirAsli { get; set; }

    public string? IsSsvc { get; set; }

    public string? IsSsvcasli { get; set; }

    public string? IsSuratSehat { get; set; }

    public string? IsSuratSehatAsli { get; set; }

    public string? IsSuratLamaran { get; set; }

    public string? IsSuratLamaranAsli { get; set; }

    public int? Agama { get; set; }

    public int? TimingThr { get; set; }
}
