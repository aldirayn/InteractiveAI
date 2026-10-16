using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace InteractiveAI.Backend.Data.Models.DB.KAGT;

public partial class TempKacsGpsContext : DbContext
{
    public TempKacsGpsContext()
    {
    }

    public TempKacsGpsContext(DbContextOptions<TempKacsGpsContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Driver> Drivers { get; set; }

    public virtual DbSet<UnitOndutyApiUnitPasangan> UnitOndutyApiUnitPasangans { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https:
        => optionsBuilder.UseMySQL("server=192.168.225.168;user=mySqlDB;password=mySqlDB@2023;database=kacs_gps");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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

        modelBuilder.Entity<UnitOndutyApiUnitPasangan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("unit_onduty_api_unit_pasangan");

            entity.HasIndex(e => e.VehicleNo, "vehicle_no");

            entity.Property(e => e.Id)
                .HasColumnType("int(11) unsigned")
                .HasColumnName("id");
            entity.Property(e => e.AlasanDriver)
                .HasMaxLength(30)
                .HasColumnName("alasanDriver");
            entity.Property(e => e.AlasanUnitTdkAktif)
                .HasMaxLength(100)
                .HasColumnName("alasan_unit_tdk_aktif");
            entity.Property(e => e.CustomerKontrakCode).HasMaxLength(100);
            entity.Property(e => e.CustomerKontrakNama).HasMaxLength(100);
            entity.Property(e => e.DateStatusUnit)
                .HasColumnType("datetime")
                .HasColumnName("date_status_unit");
            entity.Property(e => e.IsChecklistUnit).HasColumnName("is_checklist_unit");
            entity.Property(e => e.JenisTruck)
                .HasMaxLength(25)
                .HasColumnName("jenis_truck");
            entity.Property(e => e.KetUnitTdkAktif)
                .HasMaxLength(255)
                .HasColumnName("ket_unit_tdk_aktif");
            entity.Property(e => e.KodeDriverBaru)
                .HasMaxLength(50)
                .HasColumnName("kode_driver_baru");
            entity.Property(e => e.KodeDriverLama)
                .HasMaxLength(25)
                .HasColumnName("kode_driver_lama");
            entity.Property(e => e.NamaDriver)
                .HasMaxLength(100)
                .HasColumnName("nama_driver");
            entity.Property(e => e.StatusDriverKimo)
                .HasMaxLength(25)
                .HasColumnName("status_driver_kimo");
            entity.Property(e => e.StatusUnitKimo)
                .HasMaxLength(25)
                .HasColumnName("status_unit_kimo");
            entity.Property(e => e.TglChecklistUnit)
                .HasColumnType("datetime")
                .HasColumnName("tgl_checklist_unit");
            entity.Property(e => e.UnitKontrak)
                .HasMaxLength(25)
                .HasColumnName("unit_kontrak");
            entity.Property(e => e.UserStatusUnit)
                .HasMaxLength(100)
                .HasColumnName("user_status_unit");
            entity.Property(e => e.VehicleNo)
                .HasMaxLength(25)
                .HasColumnName("vehicle_no");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
