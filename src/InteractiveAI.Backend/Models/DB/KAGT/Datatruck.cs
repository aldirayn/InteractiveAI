using System;
using System.Collections.Generic;

namespace InteractiveAI.Backend.Data.Models.DB.KAGT;

public partial class Datatruck
{
    public int Id { get; set; }

    public string? Notruck { get; set; }

    public string? Vehicleno { get; set; }

    public int? Idmerk { get; set; }

    public int? Idjenistruck { get; set; }

    public int? Tahunbuat { get; set; }

    public int? Tahunbeli { get; set; }

    public int? Idpool { get; set; }

    public int? Idunit { get; set; }

    public string? Keterangan { get; set; }

    public string? Kondisi { get; set; }

    public string? Specmodel { get; set; }

    public int? Kmlimit { get; set; }

    public string? Nomesin { get; set; }

    public string? Norangka { get; set; }

    public DateTime? Garansistr { get; set; }

    public DateTime? Garansiend { get; set; }

    public string? Specketerangan { get; set; }

    public string? Atasnama { get; set; }

    public string? Bpkb { get; set; }

    public string? Urlbpkb { get; set; }

    public string? Fnamebpkb { get; set; }

    public string? Keteranganbpkb { get; set; }

    public DateTime? Stnk { get; set; }

    public string? Urlstnk { get; set; }

    public string? Fnamestnk { get; set; }

    public string? Keteranganstnk { get; set; }

    public DateTime? Kir { get; set; }

    public string? Urlkir { get; set; }

    public string? Fnamekir { get; set; }

    public string? Keterangankir { get; set; }

    public DateTime? Kiu { get; set; }

    public string? Urlkiu { get; set; }

    public string? Fnamekiu { get; set; }

    public string? Keterangankiu { get; set; }

    public DateTime? Ibm { get; set; }

    public string? Urlibm { get; set; }

    public string? Fnameibm { get; set; }

    public string? Keteranganibm { get; set; }

    public DateTime? Asuransi { get; set; }

    public string? Urlasuransi { get; set; }

    public string? Fnameasuransi { get; set; }

    public string? Keteranganasuransi { get; set; }

    public DateTime? Reklame { get; set; }

    public string? Urlreklame { get; set; }

    public string? Fnamereklame { get; set; }

    public string? Keteranganreklame { get; set; }

    public string? Nopolis { get; set; }

    public string? Urlnopolis { get; set; }

    public string? Fnamenopolis { get; set; }

    public string? Keterangannopolis { get; set; }

    public string? Peminjam { get; set; }

    public string? Urlpeminjam { get; set; }

    public string? Fnamepeminjam { get; set; }

    public string? Keteranganpeminjam { get; set; }

    public string? Leasing { get; set; }

    public string? Urlleasing { get; set; }

    public string? Fnameleasing { get; set; }

    public string? Keteranganleasing { get; set; }

    public int Urutan { get; set; }

    public float Maxspeed { get; set; }

    public float? Kmworkshop { get; set; }

    public float? Hmworkshop { get; set; }

    public float? Kmgps { get; set; }

    public float? Hmgps { get; set; }

    public string? Currentsuhu { get; set; }

    public DateTime? Sensorsuhulastupdate { get; set; }

    public DateTime? Gpslastupdate { get; set; }

    public string? Status { get; set; }

    public string? Statusflow { get; set; }

    public string? Golonganac { get; set; }

    public DateTime? Tanggaljual { get; set; }

    public decimal? Idgolonganac { get; set; }

    public DateTime? Tanggaldijual { get; set; }

    public int? Idparkirpool { get; set; }

    public string Doublechamber { get; set; } = null!;

    public string Doubleengine { get; set; } = null!;

    public string Oliacgabungolimesin { get; set; } = null!;

    public int? Idcustkontrak { get; set; }

    public string Unitkontrak { get; set; } = null!;

    public int? Idarea { get; set; }

    public int? Idsubarea { get; set; }

    public int? Idresetkmmaintenance { get; set; }

    public string? Unitpurpose { get; set; }

    public int? Idsemidedicatedproduct { get; set; }

    public string? VehiclenoClean { get; set; }

    public DateTime? Tglperiksabox { get; set; }

    public DateTime? Tglperiksaban { get; set; }
    public int? Idteknologi { get; set; }
    public int? IdModel { get; set; }
    public string? MerkPendingin { get; set; }
}
