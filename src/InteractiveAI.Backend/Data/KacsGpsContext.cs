using InteractiveAI.Backend.Data.Models.DB.KAGT;
using InteractiveAI.Backend.Models.DB.KAGT;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace InteractiveAI.Backend.Data;

public class KacsGpsContext : DbContext
{
    public KacsGpsContext(DbContextOptions<KacsGpsContext> options) : base(options) { }

    public virtual DbSet<KimoPerbaikanBengkelV3> KimoPerbaikan { get; set; }
    public virtual DbSet<Driver> Drivers { get; set; }
    public virtual DbSet<Datatruck> Datatrucks { get; set; }
    public virtual DbSet<Jenistruck> Jenistrucks { get; set; }
    public virtual DbSet<GpsHistoryLast> GpsHistoryLasts { get; set; }
    public virtual DbSet<VwAllUnitV3> VwAllUnitV3s { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https:

        if (!optionsBuilder.IsConfigured)
        {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https:
            string environment = AppConfig.Config.Environment.ToUpper();
            string connectionString = environment == "DEV" ?
                               AppConfig.Config.ConfigDev.Kagt :
                                environment == "PROD" ? AppConfig.Config.ConfigProd.KagtConnection :
                               string.Empty;
            if (!string.IsNullOrEmpty(connectionString))
                optionsBuilder.UseMySQL(connectionString)
                              .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
                              .EnableSensitiveDataLogging();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KimoPerbaikanBengkelV3>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("kimo_perbaikan_bengkel_v3");

            entity.HasIndex(e => e.Srdnumber, "idx_SRDNumber");

            entity.HasIndex(e => e.NoPol, "idx_vehicleno");

            entity.Property(e => e.Id)
                .HasMaxLength(100)
                .HasColumnName("id");
            entity.Property(e => e.AcceptDate).HasColumnType("datetime");
            entity.Property(e => e.AcceptUser).HasMaxLength(100);
            entity.Property(e => e.BatalDate).HasColumnType("datetime");
            entity.Property(e => e.BatalKeterangan).HasMaxLength(100);
            entity.Property(e => e.BatalUser).HasMaxLength(100);
            entity.Property(e => e.CancelDate).HasColumnType("datetime");
            entity.Property(e => e.CancelKeterangan).HasMaxLength(255);
            entity.Property(e => e.CancelUser).HasMaxLength(100);
            entity.Property(e => e.CloseDate).HasColumnType("datetime");
            entity.Property(e => e.CloseUser).HasMaxLength(100);
            entity.Property(e => e.CreateDate).HasColumnType("datetime");
            entity.Property(e => e.CreateUser).HasMaxLength(100);
            entity.Property(e => e.DescPerbaikan).HasMaxLength(5000);
            entity.Property(e => e.DoneDate).HasColumnType("datetime");
            entity.Property(e => e.DoneUser).HasMaxLength(100);
            entity.Property(e => e.DraftDate).HasColumnType("datetime");
            entity.Property(e => e.DraftUser).HasMaxLength(100);
            entity.Property(e => e.JenisPerbaikan).HasMaxLength(10);
            entity.Property(e => e.JenisTruck).HasMaxLength(25);
            entity.Property(e => e.KodeDriverLama).HasMaxLength(100);
            entity.Property(e => e.LastStatus).HasMaxLength(20);
            entity.Property(e => e.NamaDriver).HasMaxLength(100);
            entity.Property(e => e.NoPol).HasMaxLength(25);
            entity.Property(e => e.Srdnumber)
                .HasMaxLength(18)
                .HasColumnName("SRDNumber");
            entity.Property(e => e.SubJenisPerbaikan).HasMaxLength(15);
            entity.Property(e => e.TimestampEntry)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("timestamp_entry");
            entity.Property(e => e.OdoKm)
                .HasColumnName("OdoKm");
            entity.Property(e => e.TujuanPoolPerbaikan)
                .HasMaxLength(100)
                .HasColumnName("TujuanPoolPerbaikan");
            entity.Property(e => e.KodeDriver)
                .HasMaxLength(15)
                .HasColumnName("KodeDriver");
            entity.Property(e => e.NoHpDriver)
                .HasMaxLength(16)
                .HasColumnName("NoHpDriver");
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("driver");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.Agama).HasColumnType("int(11)");
            entity.Property(e => e.AlamatLat).HasMaxLength(255);
            entity.Property(e => e.AlamatLong).HasMaxLength(255);
            entity.Property(e => e.Dmterima).HasColumnName("DMTerima");
            entity.Property(e => e.EmergencyContactKeterangan).HasMaxLength(100);
            entity.Property(e => e.EmergencyContactKeterangan2).HasMaxLength(100);
            entity.Property(e => e.EmergencyContactName).HasMaxLength(100);
            entity.Property(e => e.EmergencyContactName2).HasMaxLength(100);
            entity.Property(e => e.EmergencyContactPhoneNumber).HasMaxLength(100);
            entity.Property(e => e.EmergencyContactPhoneNumber2).HasMaxLength(100);
            entity.Property(e => e.EmergencyContactRelation).HasMaxLength(100);
            entity.Property(e => e.EmergencyContactRelation2).HasMaxLength(100);
            entity.Property(e => e.GeofenceId).HasColumnType("int(11)");
            entity.Property(e => e.IdAbsen).HasMaxLength(21);
            entity.Property(e => e.IdArea).HasColumnType("int(11)");
            entity.Property(e => e.IdJenisSim).HasColumnType("int(11)");
            entity.Property(e => e.IdKabKota).HasColumnType("int(11)");
            entity.Property(e => e.IdKabKotaDomisili).HasColumnType("int(11)");
            entity.Property(e => e.IdKec).HasColumnType("int(11)");
            entity.Property(e => e.IdKecDomisili).HasColumnType("int(11)");
            entity.Property(e => e.IdKel).HasColumnType("int(11)");
            entity.Property(e => e.IdKelDomisili).HasColumnType("int(11)");
            entity.Property(e => e.IdProvinsi).HasColumnType("int(11)");
            entity.Property(e => e.IdProvinsiDomisili).HasColumnType("int(11)");
            entity.Property(e => e.IdRef).HasColumnType("int(11)");
            entity.Property(e => e.IdReferensiDriver).HasColumnType("int(11)");
            entity.Property(e => e.IdStatus).HasColumnType("int(11)");
            entity.Property(e => e.IdStatus2).HasColumnType("int(11)");
            entity.Property(e => e.IdSubArea).HasColumnType("int(11)");
            entity.Property(e => e.IdSubStatus).HasColumnType("int(11)");
            entity.Property(e => e.IdSubStatus2).HasColumnType("int(11)");
            entity.Property(e => e.IsAkteLahir).HasMaxLength(5);
            entity.Property(e => e.IsAkteLahirAsli).HasMaxLength(5);
            entity.Property(e => e.IsBukuNikah).HasMaxLength(5);
            entity.Property(e => e.IsBukuNikahAsli).HasMaxLength(5);
            entity.Property(e => e.IsDomisili).HasMaxLength(5);
            entity.Property(e => e.IsDomisiliAsli).HasMaxLength(5);
            entity.Property(e => e.IsIjazah).HasMaxLength(5);
            entity.Property(e => e.IsIjazahAsli).HasMaxLength(5);
            entity.Property(e => e.IsJaminanKel).HasMaxLength(5);
            entity.Property(e => e.IsJaminanKelAsli).HasMaxLength(5);
            entity.Property(e => e.IsKemitraan).HasMaxLength(5);
            entity.Property(e => e.IsKemitraanAsli).HasMaxLength(5);
            entity.Property(e => e.IsKk)
                .HasMaxLength(5)
                .HasColumnName("IsKK");
            entity.Property(e => e.IsKkasli)
                .HasMaxLength(5)
                .HasColumnName("IsKKAsli");
            entity.Property(e => e.IsKtp)
                .HasMaxLength(5)
                .HasColumnName("IsKTP");
            entity.Property(e => e.IsKtpasli)
                .HasMaxLength(5)
                .HasColumnName("IsKTPAsli");
            entity.Property(e => e.IsSameKtp).HasMaxLength(5);
            entity.Property(e => e.IsSim)
                .HasMaxLength(5)
                .HasColumnName("IsSIM");
            entity.Property(e => e.IsSimasli)
                .HasMaxLength(5)
                .HasColumnName("IsSIMAsli");
            entity.Property(e => e.IsSkck)
                .HasMaxLength(5)
                .HasColumnName("IsSKCK");
            entity.Property(e => e.IsSkckasli)
                .HasMaxLength(5)
                .HasColumnName("IsSKCKAsli");
            entity.Property(e => e.IsSms).HasMaxLength(5);
            entity.Property(e => e.IsSsvc)
                .HasMaxLength(5)
                .HasColumnName("IsSSVC");
            entity.Property(e => e.IsSsvcasli)
                .HasMaxLength(5)
                .HasColumnName("IsSSVCAsli");
            entity.Property(e => e.IsSuratLamaran).HasMaxLength(5);
            entity.Property(e => e.IsSuratLamaranAsli).HasMaxLength(5);
            entity.Property(e => e.IsSuratSehat).HasMaxLength(5);
            entity.Property(e => e.IsSuratSehatAsli).HasMaxLength(5);
            entity.Property(e => e.LastSync).HasColumnType("datetime");
            entity.Property(e => e.ServerTmsenvironment)
                .HasMaxLength(1)
                .HasColumnName("ServerTMSEnvironment");
            entity.Property(e => e.ShowKpd)
                .HasColumnType("int(11)")
                .HasColumnName("show_kpd");
            entity.Property(e => e.SubStatus2).HasMaxLength(100);
            entity.Property(e => e.TglBerlakuSim).HasColumnType("datetime");
            entity.Property(e => e.TglGabung).HasColumnType("datetime");
            entity.Property(e => e.TglLahir).HasColumnType("datetime");
            entity.Property(e => e.TglStatus).HasColumnType("datetime");
            entity.Property(e => e.TimingThr)
                .HasColumnType("int(11)")
                .HasColumnName("TimingTHR");
            entity.Property(e => e.UrlAkteLahir).HasMaxLength(255);
            entity.Property(e => e.UrlKk).HasColumnName("UrlKK");
            entity.Property(e => e.UrlKtp).HasColumnName("UrlKTP");
            entity.Property(e => e.UrlSim).HasColumnName("UrlSIM");
            entity.Property(e => e.UrlSkck).HasColumnName("UrlSKCK");
            entity.Property(e => e.UrlSsvc)
                .HasMaxLength(255)
                .HasColumnName("UrlSSVC");
            entity.Property(e => e.UrlSuratLamaran).HasMaxLength(255);
            entity.Property(e => e.UrlSuratSehat).HasMaxLength(255);
            entity.Property(e => e.Urutan).HasColumnType("int(11)");
        });

        modelBuilder.Entity<Datatruck>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("datatruck");

            entity.HasIndex(e => new { e.VehiclenoClean, e.Unitpurpose }, "idx_vehicleno_clean");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Asuransi)
                .HasColumnType("datetime")
                .HasColumnName("asuransi");
            entity.Property(e => e.Atasnama).HasColumnName("atasnama");
            entity.Property(e => e.Bpkb).HasColumnName("bpkb");
            entity.Property(e => e.Currentsuhu).HasColumnName("currentsuhu");
            entity.Property(e => e.Doublechamber)
                .HasMaxLength(5)
                .HasColumnName("doublechamber");
            entity.Property(e => e.Doubleengine)
                .HasMaxLength(5)
                .HasColumnName("doubleengine");
            entity.Property(e => e.Fnameasuransi).HasColumnName("fnameasuransi");
            entity.Property(e => e.Fnamebpkb).HasColumnName("fnamebpkb");
            entity.Property(e => e.Fnameibm).HasColumnName("fnameibm");
            entity.Property(e => e.Fnamekir).HasColumnName("fnamekir");
            entity.Property(e => e.Fnamekiu).HasColumnName("fnamekiu");
            entity.Property(e => e.Fnameleasing).HasColumnName("fnameleasing");
            entity.Property(e => e.Fnamenopolis).HasColumnName("fnamenopolis");
            entity.Property(e => e.Fnamepeminjam).HasColumnName("fnamepeminjam");
            entity.Property(e => e.Fnamereklame).HasColumnName("fnamereklame");
            entity.Property(e => e.Fnamestnk).HasColumnName("fnamestnk");
            entity.Property(e => e.Garansiend)
                .HasColumnType("datetime")
                .HasColumnName("garansiend");
            entity.Property(e => e.Garansistr)
                .HasColumnType("datetime")
                .HasColumnName("garansistr");
            entity.Property(e => e.Golonganac).HasColumnName("golonganac");
            entity.Property(e => e.Gpslastupdate)
                .HasColumnType("datetime")
                .HasColumnName("gpslastupdate");
            entity.Property(e => e.Hmgps).HasColumnName("hmgps");
            entity.Property(e => e.Hmworkshop).HasColumnName("hmworkshop");
            entity.Property(e => e.Ibm)
                .HasColumnType("datetime")
                .HasColumnName("ibm");
            entity.Property(e => e.Idarea)
                .HasColumnType("int(11)")
                .HasColumnName("idarea");
            entity.Property(e => e.Idcustkontrak)
                .HasColumnType("int(11)")
                .HasColumnName("idcustkontrak");
            entity.Property(e => e.Idgolonganac)
                .HasPrecision(65, 30)
                .HasColumnName("idgolonganac");
            entity.Property(e => e.Idjenistruck)
                .HasColumnType("int(11)")
                .HasColumnName("idjenistruck");
            entity.Property(e => e.Idmerk)
                .HasColumnType("int(11)")
                .HasColumnName("idmerk");
            entity.Property(e => e.Idparkirpool)
                .HasColumnType("int(11)")
                .HasColumnName("idparkirpool");
            entity.Property(e => e.Idpool)
                .HasColumnType("int(11)")
                .HasColumnName("idpool");
            entity.Property(e => e.Idresetkmmaintenance)
                .HasColumnType("int(11)")
                .HasColumnName("idresetkmmaintenance");
            entity.Property(e => e.Idsemidedicatedproduct)
                .HasColumnType("int(11)")
                .HasColumnName("idsemidedicatedproduct");
            entity.Property(e => e.Idsubarea)
                .HasColumnType("int(11)")
                .HasColumnName("idsubarea");
            entity.Property(e => e.Idunit)
                .HasColumnType("int(11)")
                .HasColumnName("idunit");
            entity.Property(e => e.Keterangan).HasColumnName("keterangan");
            entity.Property(e => e.Keteranganasuransi).HasColumnName("keteranganasuransi");
            entity.Property(e => e.Keteranganbpkb).HasColumnName("keteranganbpkb");
            entity.Property(e => e.Keteranganibm).HasColumnName("keteranganibm");
            entity.Property(e => e.Keterangankir).HasColumnName("keterangankir");
            entity.Property(e => e.Keterangankiu).HasColumnName("keterangankiu");
            entity.Property(e => e.Keteranganleasing).HasColumnName("keteranganleasing");
            entity.Property(e => e.Keterangannopolis).HasColumnName("keterangannopolis");
            entity.Property(e => e.Keteranganpeminjam).HasColumnName("keteranganpeminjam");
            entity.Property(e => e.Keteranganreklame).HasColumnName("keteranganreklame");
            entity.Property(e => e.Keteranganstnk).HasColumnName("keteranganstnk");
            entity.Property(e => e.Kir)
                .HasColumnType("datetime")
                .HasColumnName("kir");
            entity.Property(e => e.Kiu)
                .HasColumnType("datetime")
                .HasColumnName("kiu");
            entity.Property(e => e.Kmgps).HasColumnName("kmgps");
            entity.Property(e => e.Kmlimit)
                .HasColumnType("int(11)")
                .HasColumnName("kmlimit");
            entity.Property(e => e.Kmworkshop).HasColumnName("kmworkshop");
            entity.Property(e => e.Kondisi).HasColumnName("kondisi");
            entity.Property(e => e.Leasing).HasColumnName("leasing");
            entity.Property(e => e.Maxspeed).HasColumnName("maxspeed");
            entity.Property(e => e.Nomesin).HasColumnName("nomesin");
            entity.Property(e => e.Nopolis).HasColumnName("nopolis");
            entity.Property(e => e.Norangka).HasColumnName("norangka");
            entity.Property(e => e.Notruck).HasColumnName("notruck");
            entity.Property(e => e.Oliacgabungolimesin)
                .HasMaxLength(5)
                .HasColumnName("oliacgabungolimesin");
            entity.Property(e => e.Peminjam).HasColumnName("peminjam");
            entity.Property(e => e.Reklame)
                .HasColumnType("datetime")
                .HasColumnName("reklame");
            entity.Property(e => e.Sensorsuhulastupdate)
                .HasColumnType("datetime")
                .HasColumnName("sensorsuhulastupdate");
            entity.Property(e => e.Specketerangan).HasColumnName("specketerangan");
            entity.Property(e => e.Specmodel).HasColumnName("specmodel");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Statusflow).HasColumnName("statusflow");
            entity.Property(e => e.Stnk)
                .HasColumnType("datetime")
                .HasColumnName("stnk");
            entity.Property(e => e.Tahunbeli)
                .HasColumnType("int(11)")
                .HasColumnName("tahunbeli");
            entity.Property(e => e.Tahunbuat)
                .HasColumnType("int(11)")
                .HasColumnName("tahunbuat");
            entity.Property(e => e.Tanggaldijual)
                .HasColumnType("datetime")
                .HasColumnName("tanggaldijual");
            entity.Property(e => e.Tanggaljual)
                .HasColumnType("date")
                .HasColumnName("tanggaljual");
            entity.Property(e => e.Tglperiksaban)
                .HasColumnType("date")
                .HasColumnName("tglperiksaban");
            entity.Property(e => e.Tglperiksabox)
                .HasColumnType("date")
                .HasColumnName("tglperiksabox");
            entity.Property(e => e.Unitkontrak)
                .HasMaxLength(5)
                .HasColumnName("unitkontrak");
            entity.Property(e => e.Unitpurpose)
                .HasMaxLength(99)
                .HasColumnName("unitpurpose");
            entity.Property(e => e.Urlasuransi).HasColumnName("urlasuransi");
            entity.Property(e => e.Urlbpkb).HasColumnName("urlbpkb");
            entity.Property(e => e.Urlibm).HasColumnName("urlibm");
            entity.Property(e => e.Urlkir).HasColumnName("urlkir");
            entity.Property(e => e.Urlkiu).HasColumnName("urlkiu");
            entity.Property(e => e.Urlleasing).HasColumnName("urlleasing");
            entity.Property(e => e.Urlnopolis).HasColumnName("urlnopolis");
            entity.Property(e => e.Urlpeminjam).HasColumnName("urlpeminjam");
            entity.Property(e => e.Urlreklame).HasColumnName("urlreklame");
            entity.Property(e => e.Urlstnk).HasColumnName("urlstnk");
            entity.Property(e => e.Urutan)
                .HasColumnType("int(11)")
                .HasColumnName("urutan");
            entity.Property(e => e.Vehicleno).HasColumnName("vehicleno");
            entity.Property(e => e.VehiclenoClean)
                .HasMaxLength(50)
                .HasColumnName("vehicleno_clean");
            entity.Property(e => e.Idteknologi)
               .HasColumnType("int(11)")
               .HasColumnName("idteknologi");
            entity.Property(e => e.IdModel)
               .HasColumnType("int(11)")
               .HasColumnName("specmodelid");
            entity.Property(e => e.MerkPendingin)
               .HasColumnType("varchar(50)")
               .HasColumnName("merkpendingin");
        });

        modelBuilder.Entity<Jenistruck>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("jenistrucks");

            entity.Property(e => e.AcInterval).HasColumnType("int(11)");
            entity.Property(e => e.GolTol).HasColumnType("int(11)");
            entity.Property(e => e.Id).HasColumnType("int(11)");
        });

        modelBuilder.Entity<GpsHistoryLast>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("gps_history_last");

            entity.HasIndex(e => e.DeviceId, "device_id");

            entity.HasIndex(e => e.Plate, "plate");

            entity.HasIndex(e => e.TimeLocal, "time_local");

            entity.HasIndex(e => e.VehicleType, "vehicle_type");

            entity.HasIndex(e => e.Vendor, "vendor");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.DeviceId)
                .HasMaxLength(25)
                .HasColumnName("device_id");
            entity.Property(e => e.EngineCar)
                .HasColumnType("int(1)")
                .HasColumnName("engine_car");
            entity.Property(e => e.EngineRef)
                .HasColumnType("int(1)")
                .HasColumnName("engine_Ref");
            entity.Property(e => e.Geofence)
                .HasMaxLength(128)
                .HasColumnName("geofence");
            entity.Property(e => e.GeofenceNew)
                .HasMaxLength(128)
                .HasColumnName("geofence_new");
            entity.Property(e => e.Heading)
                .HasColumnType("int(3) unsigned")
                .HasColumnName("heading");
            entity.Property(e => e.Hourmeter)
                .HasPrecision(8, 1)
                .HasColumnName("hourmeter");
            entity.Property(e => e.KabKota)
                .HasMaxLength(128)
                .HasColumnName("kab_kota");
            entity.Property(e => e.KategoriBox)
                .HasMaxLength(255)
                .HasColumnName("kategori_box");
            entity.Property(e => e.KategoriPendingin)
                .HasMaxLength(255)
                .HasColumnName("kategori_pendingin");
            entity.Property(e => e.Kir)
                .HasColumnType("date")
                .HasColumnName("kir");
            entity.Property(e => e.LastPing)
                .HasColumnType("datetime")
                .HasColumnName("last_ping");
            entity.Property(e => e.LastPingHm)
                .HasColumnType("datetime")
                .HasColumnName("last_ping_hm");
            entity.Property(e => e.Lat)
                .HasPrecision(12, 8)
                .HasColumnName("lat");
            entity.Property(e => e.Location)
                .HasMaxLength(1024)
                .HasColumnName("location");
            entity.Property(e => e.Long)
                .HasPrecision(12, 8)
                .HasColumnName("long");
            entity.Property(e => e.Mileage)
                .HasColumnType("int(11)")
                .HasColumnName("mileage");
            entity.Property(e => e.Plate)
                .HasMaxLength(50)
                .HasColumnName("plate");
            entity.Property(e => e.Prov)
                .HasMaxLength(128)
                .HasColumnName("prov");
            entity.Property(e => e.Speed)
                .HasColumnType("int(5)")
                .HasColumnName("speed");
            entity.Property(e => e.Stnk)
                .HasColumnType("date")
                .HasColumnName("stnk");
            entity.Property(e => e.Temperature)
                .HasPrecision(6)
                .HasColumnName("temperature");
            entity.Property(e => e.Temperature2)
                .HasPrecision(6)
                .HasColumnName("temperature2");
            entity.Property(e => e.TimeGps)
                .HasColumnType("datetime")
                .HasColumnName("time_gps");
            entity.Property(e => e.TimeLocal)
                .HasColumnType("datetime")
                .HasColumnName("time_local");
            entity.Property(e => e.TimeServer)
                .HasColumnType("datetime")
                .HasColumnName("time_server");
            entity.Property(e => e.TokoTujuan)
                .HasMaxLength(2048)
                .HasColumnName("toko_tujuan");
            entity.Property(e => e.VehicleType)
                .HasMaxLength(30)
                .HasColumnName("vehicle_type");
            entity.Property(e => e.Vendor)
                .HasMaxLength(20)
                .HasColumnName("vendor");
        });

        modelBuilder.Entity<VwAllUnitV3>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("vw_all_unit_v3");

            entity.HasIndex(e => e.NoPol2, "idx_main_nopol2");

            entity.HasIndex(e => e.NoPol2, "idx_no_pol2").IsUnique();

            entity.Property(e => e.ContainerId)
                .HasMaxLength(10)
                .HasColumnName("container_id");
            entity.Property(e => e.CustPriority)
                .HasMaxLength(1)
                .HasColumnName("cust_priority");
            entity.Property(e => e.Custcodeload)
                .HasMaxLength(100)
                .HasColumnName("custcodeload");
            entity.Property(e => e.Custcodeunload)
                .HasMaxLength(100)
                .HasColumnName("custcodeunload");
            entity.Property(e => e.DriverBatangan)
                .HasMaxLength(255)
                .HasColumnName("driver_batangan");
            entity.Property(e => e.DriverStatus)
                .HasMaxLength(15)
                .HasColumnName("driver_status");
            entity.Property(e => e.DriverSubStatus)
                .HasMaxLength(15)
                .HasColumnName("driver_sub_status");
            entity.Property(e => e.EkorId)
                .HasMaxLength(10)
                .HasColumnName("ekor_id");
            entity.Property(e => e.FlagRuteDalamKota)
                .HasMaxLength(1)
                .HasColumnName("flag_rute_dalam_kota");
            entity.Property(e => e.GensetId)
                .HasMaxLength(10)
                .HasColumnName("genset_id");
            entity.Property(e => e.GolAc)
                .HasMaxLength(10)
                .HasColumnName("gol_ac");
            entity.Property(e => e.IsTerpairing)
                .HasColumnType("int(11)")
                .HasColumnName("is_terpairing");
            entity.Property(e => e.JenisBarang)
                .HasMaxLength(30)
                .HasColumnName("jenis_barang");
            entity.Property(e => e.JenisTruck)
                .HasMaxLength(15)
                .HasColumnName("jenis_truck");
            entity.Property(e => e.KetMkt)
                .HasColumnType("text")
                .HasColumnName("ket_mkt");
            entity.Property(e => e.KodeCust)
                .HasMaxLength(20)
                .HasColumnName("kode_cust");
            entity.Property(e => e.KodeDriver)
                .HasMaxLength(12)
                .HasColumnName("kode_driver");
            entity.Property(e => e.KodeDriverBatangan)
                .HasMaxLength(13)
                .HasColumnName("kode_driver_batangan");
            entity.Property(e => e.KodeDriverOld)
                .HasMaxLength(13)
                .HasColumnName("kode_driver_old");
            entity.Property(e => e.LastDetectedPool)
                .HasMaxLength(30)
                .HasColumnName("last_detected_pool");
            entity.Property(e => e.LastDetectedTime)
                .HasColumnType("datetime")
                .HasColumnName("last_detected_time");
            entity.Property(e => e.Listidrute)
                .HasMaxLength(20)
                .HasColumnName("listidrute");
            entity.Property(e => e.LoadLat).HasColumnName("load_lat");
            entity.Property(e => e.LoadLong).HasColumnName("load_long");
            entity.Property(e => e.LoadName)
                .HasMaxLength(80)
                .HasColumnName("load_name");
            entity.Property(e => e.LoadRadius)
                .HasMaxLength(5)
                .HasColumnName("load_radius");
            entity.Property(e => e.NamaCust)
                .HasMaxLength(55)
                .HasColumnName("nama_cust");
            entity.Property(e => e.NamaDriver)
                .HasMaxLength(40)
                .HasColumnName("nama_driver");
            entity.Property(e => e.NamaRute)
                .HasMaxLength(200)
                .HasColumnName("nama_rute");
            entity.Property(e => e.NoContainer)
                .HasMaxLength(25)
                .HasColumnName("no_container");
            entity.Property(e => e.NoEkor)
                .HasMaxLength(25)
                .HasColumnName("no_ekor");
            entity.Property(e => e.NoGenset)
                .HasMaxLength(25)
                .HasColumnName("no_genset");
            entity.Property(e => e.NoHpBatangan)
                .HasMaxLength(25)
                .HasColumnName("no_hp_batangan");
            entity.Property(e => e.NoHpDriver)
                .HasMaxLength(25)
                .HasColumnName("no_hp_driver");
            entity.Property(e => e.NoPol)
                .HasMaxLength(25)
                .HasColumnName("no_pol");
            entity.Property(e => e.NoPol2)
                .HasMaxLength(25)
                .HasColumnName("no_pol2");
            entity.Property(e => e.NoRk)
                .HasMaxLength(30)
                .HasColumnName("no_rk");
            entity.Property(e => e.OcDoubleChamber)
                .HasColumnType("bit(1)")
                .HasColumnName("oc_double_chamber");
            entity.Property(e => e.OcDry)
                .HasColumnType("bit(1)")
                .HasColumnName("oc_dry");
            entity.Property(e => e.Oliacgabungolimesin)
                .HasColumnType("bit(1)")
                .HasColumnName("oliacgabungolimesin");
            entity.Property(e => e.ProvTujuan)
                .HasMaxLength(35)
                .HasColumnName("prov_tujuan");
            entity.Property(e => e.RangeSuhu)
                .HasMaxLength(10)
                .HasColumnName("range_suhu");
            entity.Property(e => e.Ranking)
                .HasMaxLength(1)
                .HasColumnName("ranking");
            entity.Property(e => e.ResetKmMaintenance)
                .HasColumnType("int(11)")
                .HasColumnName("reset_km_maintenance");
            entity.Property(e => e.SoNo)
                .HasMaxLength(18)
                .HasColumnName("so_no");
            entity.Property(e => e.Spec)
                .HasMaxLength(50)
                .HasColumnName("spec");
            entity.Property(e => e.SpecDc)
                .HasColumnType("bit(1)")
                .HasColumnName("spec_dc");
            entity.Property(e => e.SpecDe)
                .HasColumnType("bit(1)")
                .HasColumnName("spec_de");
            entity.Property(e => e.SrNumber)
                .HasColumnType("text")
                .HasColumnName("sr_number");
            entity.Property(e => e.TargetSuhu)
                .HasPrecision(6)
                .HasColumnName("target_suhu");
            entity.Property(e => e.TempatBongkar)
                .HasMaxLength(255)
                .HasColumnName("tempat_bongkar");
            entity.Property(e => e.TempatMuat)
                .HasMaxLength(255)
                .HasColumnName("tempat_muat");
            entity.Property(e => e.TglKir)
                .HasColumnType("date")
                .HasColumnName("tgl_kir");
            entity.Property(e => e.TglKirEkor)
                .HasColumnType("date")
                .HasColumnName("tgl_kir_ekor");
            entity.Property(e => e.TglMuat)
                .HasColumnType("timestamp")
                .HasColumnName("tgl_muat");
            entity.Property(e => e.TglStnk)
                .HasColumnType("date")
                .HasColumnName("tgl_stnk");
            entity.Property(e => e.TmsStatus)
                .HasMaxLength(30)
                .HasColumnName("tms_status");
            entity.Property(e => e.UnloadLat).HasColumnName("unload_lat");
            entity.Property(e => e.UnloadLong).HasColumnName("unload_long");
            entity.Property(e => e.UnloadName)
                .HasMaxLength(80)
                .HasColumnName("unload_name");
            entity.Property(e => e.UnloadRadius)
                .HasMaxLength(5)
                .HasColumnName("unload_radius");
            entity.Property(e => e.Ws)
                .HasMaxLength(50)
                .HasColumnName("ws");
            entity.Property(e => e.WsTgl)
                .HasColumnType("datetime")
                .HasColumnName("ws_tgl");
        });
    }
}
