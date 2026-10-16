using InteractiveAI.Backend.Models.DB;
using InteractiveAI.Backend.Models.DB.ViewModels;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Reflection;

namespace InteractiveAI.Backend.Data
{
    public partial class MkaPoolContext : DbContext
    {
        public MkaPoolContext()
        {
        }

        public MkaPoolContext(DbContextOptions<MkaPoolContext> options)
            : base(options)
        {

        }

        public virtual DbSet<TrWoHdrProcess> TrWoHdrProcess { get; set; }
        public virtual DbSet<TrWoHdrLog> TrWoHdrLog { get; set; }
        public virtual DbSet<TrWoHdrHistory> TrWoHdrHistories { get; set; }
        public virtual DbSet<TrLockingwo> TrLockingwoes { get; set; }
        public virtual DbSet<ErrLog> ErrLogs { get; set; }
        public virtual DbSet<MsLocation> MsLocations { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https:

            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https:
                string environment = AppConfig.Config.Environment.ToUpper();
                string connectionString = environment == "DEV" ?
                                   AppConfig.Config.ConfigDev.MkaPool :
                                    environment == "PROD" ? AppConfig.Config.ConfigProd.connection :
                                   string.Empty;
                if (!string.IsNullOrEmpty(connectionString))
                    optionsBuilder.UseMySQL(connectionString)
                                  .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
                                  .EnableSensitiveDataLogging();
            }
        }

        #region Stored Procedure

        public async Task<int> ExecuteTransactionSP(string query, List<ParamTaskViewModel>? listParam = null)
        {
            try
            {

                List<DbParameter> parameters = new List<DbParameter>();
                #region Construct Params
                if (listParam != null && listParam.Count > 0)
                {
                    for (int i = 0; i < listParam.Count; i++)
                    {

                        var p = new MySqlParameter(listParam[i].colName, listParam[i].value);
                        parameters.Add(p);
                    }
                }
                #endregion Construct Params

                int exec = await Database.ExecuteSqlRawAsync(query, parameters);

                return exec;
            }
            catch
            {
                throw;
            }
        }

        #region Using Dapper











        #endregion Using Dapper


        public async Task<List<T>> ExecuteQuery<T>(string query, List<ParamTaskViewModel>? listParam = null) where T : class, new()
        {
            try
            {
                #region Using existing connection
                using (var db = Database.GetDbConnection())
                {
                    var results = new List<T>();
                    try
                    {
                        if (listParam != null && listParam.Count > 0)
                        {
                            var parameters = listParam.Select(x => new MySqlParameter(x.colName, x.value)).ToArray();
                            results = await this.Set<T>().FromSqlRaw(query, parameters).ToListAsync();
                        }
                        else
                            results = await this.Set<T>().FromSqlRaw(query).ToListAsync();
                    }
                    catch (Exception ex)
                    {

                        throw;
                    }
                    return results;
                }
                #endregion Using existing connection

                #region Using new connection

                #endregion Using new connection

            }
            catch
            {
                throw;
            }
        }
        public async Task<List<T>> ExecuteQuery2<T>(string query, List<ParamTaskViewModel>? listParam = null) where T : class, new()
        {
            var results = new List<T>();
            try
            {
                using (var connection = new MySqlConnection(AppConfig.Config.ConfigDev.MkaPool))
                {
                    await connection.OpenAsync();
                    using (var command = new MySqlCommand(query, connection))
                    {
                        if (listParam != null)
                        {
                            foreach (var param in listParam)
                                command.Parameters.AddWithValue(param.colName, param.value);
                        }

                        try
                        {
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var item = new T();

                                    foreach (var prop in typeof(T).GetProperties())
                                    {
                                        var colAttribute = prop.GetCustomAttribute<ColumnAttribute>();
                                        var colName = colAttribute?.Name ?? prop.Name;
                                        int columnIndex = -1;
                                        try
                                        {
                                            columnIndex = reader.GetOrdinal(colName);
                                        }
                                        catch (Exception ex)
                                        {
                                            if (ex.Message.Contains("Could not find specified column in results"))
                                                continue;
                                            else
                                                throw;
                                        }

                                        if (columnIndex >= -1)
                                        {
                                            var value = await reader.IsDBNullAsync(columnIndex) ? null : reader.GetValue(columnIndex);
                                            prop.SetValue(item, value);
                                        }
                                    }

                                    results.Add(item);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error executing stored procedure: {ex.Message}");
                            throw;
                        }
                    }
                }

            }
            catch
            {
                throw;
            }
            return results;
        }
        #endregion Stored Procedure

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TrWoHdrHistory>(entity =>
            {
                entity.HasKey(e => new { e.PreSrNo, e.PreSrcNo })
                    .HasName("PRIMARY")
                    .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

                entity
                    .ToTable("tr_wo_hdr_history");


                entity.HasIndex(e => e.CreatedAt, "idx_created_at");
                entity.HasIndex(e => e.PreQuickCheck, "idx_pre_quick_check");
                entity.HasIndex(e => e.PreExternal, "idx_pre_external");

                entity.HasIndex(e => new { e.Creatddt, e.Createtime }, "idx_pre01333103_datetime");

                entity.HasIndex(e => new { e.PreStatus, e.PreUnitNo, e.Creatddt, e.Createtime }, "idx_status_unit_date");

                entity.HasIndex(e => e.PreUnitNo, "idx_unit_no");

                entity.Property(e => e.PreSrNo)
                    .HasMaxLength(17)
                    .IsFixedLength()
                    .HasColumnName("pre_sr_no");
                entity.Property(e => e.PreSrcNo)
                    .HasMaxLength(15)
                    .IsFixedLength()
                    .HasColumnName("pre_src_no");
                entity.Property(e => e.Creatddt)
                    .HasColumnType("datetime")
                    .HasColumnName("creatddt");
                entity.Property(e => e.CreatedAt)
                    .HasComputedColumnSql("timestamp(cast(`CREATDDT` as date),cast(`CREATETIME` as time))", true)
                    .HasColumnType("datetime")
                    .HasColumnName("created_at");
                entity.Property(e => e.Createtime)
                    .HasColumnType("datetime")
                    .HasColumnName("createtime");
                entity.Property(e => e.Crusrid)
                    .HasMaxLength(15)
                    .IsFixedLength()
                    .HasColumnName("crusrid");
                entity.Property(e => e.DexRowId).HasColumnName("dex_row_id");
                entity.Property(e => e.Interid)
                    .HasMaxLength(5)
                    .IsFixedLength()
                    .HasColumnName("interid");
                entity.Property(e => e.Mdfusrid)
                    .HasMaxLength(15)
                    .IsFixedLength()
                    .HasColumnName("mdfusrid");
                entity.Property(e => e.Modifdt)
                    .HasColumnType("datetime")
                    .HasColumnName("modifdt");
                entity.Property(e => e.Modiftime)
                    .HasColumnType("datetime")
                    .HasColumnName("modiftime");
                entity.Property(e => e.PreBayId)
                    .HasMaxLength(15)
                    .IsFixedLength()
                    .HasColumnName("pre_bay_id");
                entity.Property(e => e.PreBayName)
                    .HasMaxLength(201)
                    .IsFixedLength()
                    .HasColumnName("pre_bay_name");
                entity.Property(e => e.PreDriverId)
                    .HasMaxLength(35)
                    .IsFixedLength()
                    .HasColumnName("pre_driver_id");
                entity.Property(e => e.PreDriverName)
                    .HasMaxLength(101)
                    .IsFixedLength()
                    .HasColumnName("pre_driver_name");
                entity.Property(e => e.PreEstimasi)
                    .HasMaxLength(21)
                    .IsFixedLength()
                    .HasColumnName("pre_estimasi");
                entity.Property(e => e.PreEstimasiDate)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_estimasi_date");
                entity.Property(e => e.PreEstimasiTime)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_estimasi_time");
                entity.Property(e => e.PreGroup)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_group");
                entity.Property(e => e.PreHmActual)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_hm_actual");
                entity.Property(e => e.PreHmGps)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_hm_gps");
                entity.Property(e => e.PreJenis)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_jenis");
                entity.Property(e => e.PreJnsPerbaikan)
                    .HasMaxLength(33)
                    .IsFixedLength()
                    .HasColumnName("pre_jns_perbaikan");
                entity.Property(e => e.PreKeluhan1)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_1");
                entity.Property(e => e.PreKeluhan10)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_10");
                entity.Property(e => e.PreKeluhan11)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_11");
                entity.Property(e => e.PreKeluhan12)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_12");
                entity.Property(e => e.PreKeluhan2)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_2");
                entity.Property(e => e.PreKeluhan3)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_3");
                entity.Property(e => e.PreKeluhan4)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_4");
                entity.Property(e => e.PreKeluhan5)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_5");
                entity.Property(e => e.PreKeluhan6)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_6");
                entity.Property(e => e.PreKeluhan7)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_7");
                entity.Property(e => e.PreKeluhan8)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_8");
                entity.Property(e => e.PreKeluhan9)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_9");
                entity.Property(e => e.PreKmActual)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_km_actual");
                entity.Property(e => e.PreKmGps)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_km_gps");
                entity.Property(e => e.PreLastHmActual)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_hm_actual");
                entity.Property(e => e.PreLastHmGps)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_hm_gps");
                entity.Property(e => e.PreLastKmActual)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_km_actual");
                entity.Property(e => e.PreLastKmGps)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_km_gps");
                entity.Property(e => e.PreLastMaintenDate)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_last_mainten_date");
                entity.Property(e => e.PreLastMaintenTime)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_last_mainten_time");
                entity.Property(e => e.PreLastQcDate)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_last_qc_date");
                entity.Property(e => e.PreLastQcHmActual)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_qc_hm_actual");
                entity.Property(e => e.PreLastQcHmGps)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_qc_hm_gps");
                entity.Property(e => e.PreLastQcKmActual)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_qc_km_actual");
                entity.Property(e => e.PreLastQcKmGps)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_qc_km_gps");
                entity.Property(e => e.PreLastQcTime)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_last_qc_time");
                entity.Property(e => e.PreLastUpdate)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_last_update");
                entity.Property(e => e.PreLastUpdateTime)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_last_update_time");
                entity.Property(e => e.PreMerk)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_merk");
                entity.Property(e => e.PreModel)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_model");
                entity.Property(e => e.PreOhEngine)
                    .HasColumnType("smallint(6)")
                    .HasColumnName("pre_oh_engine");
                entity.Property(e => e.PreQuickCheck)
                    .HasColumnType("smallint(6)")
                    .HasColumnName("pre_quick_check");
                entity.Property(e => e.PreExternal)
                    .HasColumnType("smallint(6)")
                    .HasColumnName("pre_external");
                entity.Property(e => e.PrePerbaikanDesc)
                    .HasColumnType("text")
                    .HasColumnName("pre_perbaikan_desc");
                entity.Property(e => e.PrePoolId)
                  .HasColumnType("int(11)")
                  .HasColumnName("pre_pool_id");
                entity.Property(e => e.PrePoolName)
                    .HasMaxLength(201)
                    .IsFixedLength()
                    .HasColumnName("pre_pool_name");
                entity.Property(e => e.PrePriorityDesc)
                    .HasMaxLength(33)
                    .IsFixedLength()
                    .HasColumnName("pre_priority_desc");
                entity.Property(e => e.PrePriorityId)
                    .HasMaxLength(13)
                    .IsFixedLength()
                    .HasColumnName("pre_priority_id");
                entity.Property(e => e.PrePriorityName)
                    .HasMaxLength(33)
                    .IsFixedLength()
                    .HasColumnName("pre_priority_name");
                entity.Property(e => e.PreRedoNo)
                    .HasMaxLength(17)
                    .IsFixedLength()
                    .HasColumnName("pre_redo_no");
                entity.Property(e => e.PreReject)
                    .HasMaxLength(251)
                    .IsFixedLength()
                    .HasColumnName("pre_reject");
                entity.Property(e => e.PreRejectType).HasColumnName("pre_reject_type");
                entity.Property(e => e.PreRemarks)
                    .HasMaxLength(1000)
                    .IsFixedLength()
                    .HasColumnName("pre_remarks");
                entity.Property(e => e.PreRemarksPool)
                    .HasMaxLength(255)
                    .IsFixedLength()
                    .HasColumnName("pre_remarks_pool");
                entity.Property(e => e.PreReqBy)
                    .HasMaxLength(101)
                    .IsFixedLength()
                    .HasColumnName("pre_req_by");
                entity.Property(e => e.PreRev).HasColumnName("pre_rev");
                entity.Property(e => e.PreSaranPm)
                    .HasMaxLength(101)
                    .IsFixedLength()
                    .HasColumnName("pre_saran_pm");
                entity.Property(e => e.PreStatus).HasColumnName("pre_status");
                entity.Property(e => e.PreStatusGps).HasColumnName("pre_status_gps");
                entity.Property(e => e.PreSubJobDesc)
                    .HasMaxLength(101)
                    .IsFixedLength()
                    .HasColumnName("pre_sub_job_desc");
                entity.Property(e => e.PreSubJobId)
                    .HasMaxLength(15)
                    .IsFixedLength()
                    .HasColumnName("pre_sub_job_id");
                entity.Property(e => e.PreUnitId)
                    .HasMaxLength(13)
                    .IsFixedLength()
                    .HasColumnName("pre_unit_id");
                entity.Property(e => e.PreUnitNo)
                    .HasMaxLength(33)
                    .IsFixedLength()
                    .HasColumnName("pre_unit_no");
                entity.Property(e => e.PreWoDate)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_wo_date");
                entity.Property(e => e.PreWoEndDate)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_wo_end_date");
                entity.Property(e => e.PreWoEndTime)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_wo_end_time");
                entity.Property(e => e.PreWoTime)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_wo_time");
                entity.Property(e => e.SubJobDurationJam)
                    .HasMaxLength(5)
                    .IsFixedLength()
                    .HasColumnName("sub_job_duration_jam");
                entity.Property(e => e.SubJobDurationMenit)
                    .HasMaxLength(5)
                    .IsFixedLength()
                    .HasColumnName("sub_job_duration_menit");
                entity.Property(e => e.Trxtype).HasColumnName("trxtype");
                entity.Property(e => e.PreRevRemarks)
                    .HasMaxLength(255)
                    .HasColumnName("pre_rev_remarks");
                entity.Property(e => e.DeskId)
                    .HasColumnType("int(11)")
                    .HasColumnName("desk_id");
                entity.Property(e => e.IsToday)
                   .HasColumnType("int(11)")
                   .HasColumnName("is_today");
                entity.Property(e => e.IsTodayNote)
                    .HasMaxLength(100)
                    .HasColumnName("is_today_note");
                entity.Property(e => e.NoHpDrv)
                    .HasMaxLength(30)
                    .HasColumnName("no_hp_drv");
                entity.Property(e => e.GolInsentif)
                    .HasMaxLength(10)
                    .HasColumnName("gol_insentif");
                entity.Property(e => e.LokasiParkir)
                    .HasMaxLength(100)
                    .HasColumnName("lokasi_parkir");
            });

            modelBuilder.Entity<TrWoHdrProcess>(entity =>
            {
                entity.HasKey(e => e.PreSrcNo).HasName("PRIMARY");

                entity.ToTable("tr_wo_hdr_process");

                entity.HasIndex(e => e.CreatedAt, "idx_created_at");
                entity.HasIndex(e => e.PreQuickCheck, "idx_pre_quick_check");
                entity.HasIndex(e => e.PreExternal, "idx_pre_external");
                entity.HasIndex(e => new { e.Creatddt, e.Createtime }, "idx_pre01133103_datetime");
                entity.HasIndex(e => e.DexRowId, "idx_pre01133103_dex_row_id");
                entity.HasIndex(e => new { e.PreUnitNo, e.Creatddt, e.Createtime }, "idx_pre01133103_nopol_datetime");
                entity.HasIndex(e => new { e.PreStatus, e.Creatddt, e.Createtime }, "idx_pre01133103_status_datetime");
                entity.HasIndex(e => e.PreSrcNo, "idx_pre_src_no");
                entity.HasIndex(e => new { e.PreUnitNo, e.PreStatus, e.Creatddt }, "idx_pre_unit_status_date");
                entity.HasIndex(e => new { e.PreStatus, e.PreUnitNo, e.Creatddt, e.Createtime }, "idx_status_unit_date");
                entity.HasIndex(e => e.PreUnitNo, "idx_unit_no");
                entity.Property(e => e.PreSrNo)
                    .HasMaxLength(17)
                    .IsFixedLength()
                    .HasColumnName("pre_sr_no");
                entity.Property(e => e.PreSrcNo)
                    .HasMaxLength(15)
                    .IsFixedLength()
                    .HasColumnName("pre_src_no");
                entity.Property(e => e.Creatddt)
                    .HasColumnType("datetime")
                    .HasColumnName("creatddt");
                entity.Property(e => e.CreatedAt)
                    .HasComputedColumnSql("timestamp(cast(`CREATDDT` as date),cast(`CREATETIME` as time))", true)
                    .HasColumnType("datetime")
                    .HasColumnName("created_at")
                    .ValueGeneratedOnAdd()
                    .Metadata.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
                entity.Property(e => e.Createtime)
                    .HasColumnType("datetime")
                    .HasColumnName("createtime");
                entity.Property(e => e.Crusrid)
                    .HasMaxLength(15)
                    .IsFixedLength()
                    .HasColumnName("crusrid");
                entity.Property(e => e.DexRowId)
                    .HasColumnType("int(11)")
                    .HasColumnName("dex_row_id");
                entity.Property(e => e.Interid)
                    .HasMaxLength(5)
                    .IsFixedLength()
                    .HasColumnName("interid");
                entity.Property(e => e.Mdfusrid)
                    .HasMaxLength(15)
                    .IsFixedLength()
                    .HasColumnName("mdfusrid");
                entity.Property(e => e.Modifdt)
                    .HasColumnType("datetime")
                    .HasColumnName("modifdt");
                entity.Property(e => e.Modiftime)
                    .HasColumnType("datetime")
                    .HasColumnName("modiftime");
                entity.Property(e => e.PreBayId)
                    .HasMaxLength(15)
                    .IsFixedLength()
                    .HasColumnName("pre_bay_id");
                entity.Property(e => e.PreBayName)
                    .HasMaxLength(201)
                    .IsFixedLength()
                    .HasColumnName("pre_bay_name");
                entity.Property(e => e.PreDriverId)
                    .HasMaxLength(35)
                    .IsFixedLength()
                    .HasColumnName("pre_driver_id");
                entity.Property(e => e.PreDriverName)
                    .HasMaxLength(101)
                    .IsFixedLength()
                    .HasColumnName("pre_driver_name");
                entity.Property(e => e.PreEstimasi)
                    .HasMaxLength(21)
                    .IsFixedLength()
                    .HasColumnName("pre_estimasi");
                entity.Property(e => e.PreEstimasiDate)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_estimasi_date");
                entity.Property(e => e.PreEstimasiTime)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_estimasi_time");
                entity.Property(e => e.PreGroup)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_group");
                entity.Property(e => e.PreHmActual)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_hm_actual");
                entity.Property(e => e.PreHmGps)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_hm_gps");
                entity.Property(e => e.PreJenis)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_jenis");
                entity.Property(e => e.PreJnsPerbaikan)
                    .HasMaxLength(33)
                    .IsFixedLength()
                    .HasColumnName("pre_jns_perbaikan");
                entity.Property(e => e.PreKeluhan1)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_1");
                entity.Property(e => e.PreKeluhan10)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_10");
                entity.Property(e => e.PreKeluhan11)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_11");
                entity.Property(e => e.PreKeluhan12)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_12");
                entity.Property(e => e.PreKeluhan2)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_2");
                entity.Property(e => e.PreKeluhan3)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_3");
                entity.Property(e => e.PreKeluhan4)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_4");
                entity.Property(e => e.PreKeluhan5)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_5");
                entity.Property(e => e.PreKeluhan6)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_6");
                entity.Property(e => e.PreKeluhan7)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_7");
                entity.Property(e => e.PreKeluhan8)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_8");
                entity.Property(e => e.PreKeluhan9)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_keluhan_9");
                entity.Property(e => e.PreKmActual)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_km_actual");
                entity.Property(e => e.PreKmGps)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_km_gps");
                entity.Property(e => e.PreLastHmActual)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_hm_actual");
                entity.Property(e => e.PreLastHmGps)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_hm_gps");
                entity.Property(e => e.PreLastKmActual)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_km_actual");
                entity.Property(e => e.PreLastKmGps)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_km_gps");
                entity.Property(e => e.PreLastMaintenDate)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_last_mainten_date");
                entity.Property(e => e.PreLastMaintenTime)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_last_mainten_time");
                entity.Property(e => e.PreLastQcDate)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_last_qc_date");
                entity.Property(e => e.PreLastQcHmActual)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_qc_hm_actual");
                entity.Property(e => e.PreLastQcHmGps)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_qc_hm_gps");
                entity.Property(e => e.PreLastQcKmActual)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_qc_km_actual");
                entity.Property(e => e.PreLastQcKmGps)
                    .HasPrecision(19, 5)
                    .HasColumnName("pre_last_qc_km_gps");
                entity.Property(e => e.PreLastQcTime)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_last_qc_time");
                entity.Property(e => e.PreLastUpdate)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_last_update");
                entity.Property(e => e.PreLastUpdateTime)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_last_update_time");
                entity.Property(e => e.PreMerk)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_merk");
                entity.Property(e => e.PreModel)
                    .HasMaxLength(51)
                    .IsFixedLength()
                    .HasColumnName("pre_model");
                entity.Property(e => e.PreOhEngine)
                    .HasColumnType("smallint(6)")
                    .HasColumnName("pre_oh_engine");
                entity.Property(e => e.PreQuickCheck)
                    .HasColumnType("smallint(6)")
                    .HasColumnName("pre_quick_check");
                entity.Property(e => e.PreExternal)
                    .HasColumnType("smallint(6)")
                    .HasColumnName("pre_external");
                entity.Property(e => e.PrePerbaikanDesc)
                    .HasColumnType("mediumtext")
                    .HasColumnName("pre_perbaikan_desc");
                entity.Property(e => e.PrePoolId)
                    .HasColumnType("int(11)")
                    .HasColumnName("pre_pool_id");

                entity.Property(e => e.PrePoolName)
                    .HasMaxLength(201)
                    .IsFixedLength()
                    .HasColumnName("pre_pool_name");
                entity.Property(e => e.PrePriorityDesc)
                    .HasMaxLength(33)
                    .IsFixedLength()
                    .HasColumnName("pre_priority_desc");
                entity.Property(e => e.PrePriorityId)
                    .HasMaxLength(13)
                    .IsFixedLength()
                    .HasColumnName("pre_priority_id");
                entity.Property(e => e.PrePriorityName)
                    .HasMaxLength(33)
                    .IsFixedLength()
                    .HasColumnName("pre_priority_name");
                entity.Property(e => e.PreRedoNo)
                    .HasMaxLength(17)
                    .IsFixedLength()
                    .HasColumnName("pre_redo_no");
                entity.Property(e => e.PreReject)
                    .HasMaxLength(251)
                    .IsFixedLength()
                    .HasColumnName("pre_reject");
                entity.Property(e => e.PreRejectType)
                    .HasColumnType("smallint(6)")
                    .HasColumnName("pre_reject_type");
                entity.Property(e => e.PreRemarks)
                    .HasMaxLength(1000)
                    .IsFixedLength()
                    .HasColumnName("pre_remarks");
                entity.Property(e => e.PreRemarksPool)
                    .HasMaxLength(255)
                    .IsFixedLength()
                    .HasColumnName("pre_remarks_pool");
                entity.Property(e => e.PreReqBy)
                    .HasMaxLength(101)
                    .IsFixedLength()
                    .HasColumnName("pre_req_by");
                entity.Property(e => e.PreRev)
                    .HasColumnType("int(11)")
                    .HasColumnName("pre_rev");
                entity.Property(e => e.PreSaranPm)
                    .HasMaxLength(101)
                    .IsFixedLength()
                    .HasColumnName("pre_saran_pm");
                entity.Property(e => e.PreStatus)
                    .HasColumnType("int(11)")
                    .HasColumnName("pre_status");
                entity.Property(e => e.PreStatusGps)
                    .HasColumnType("smallint(6)")
                    .HasColumnName("pre_status_gps");
                entity.Property(e => e.PreSubJobDesc)
                    .HasMaxLength(101)
                    .IsFixedLength()
                    .HasColumnName("pre_sub_job_desc");
                entity.Property(e => e.PreSubJobId)
                    .HasMaxLength(15)
                    .IsFixedLength()
                    .HasColumnName("pre_sub_job_id");
                entity.Property(e => e.PreUnitId)
                    .HasMaxLength(13)
                    .IsFixedLength()
                    .HasColumnName("pre_unit_id");
                entity.Property(e => e.PreUnitNo)
                    .HasMaxLength(33)
                    .IsFixedLength()
                    .HasColumnName("pre_unit_no");
                entity.Property(e => e.PreWoDate)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_wo_date");
                entity.Property(e => e.PreWoEndDate)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_wo_end_date");
                entity.Property(e => e.PreWoEndTime)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_wo_end_time");
                entity.Property(e => e.PreWoTime)
                    .HasColumnType("datetime")
                    .HasColumnName("pre_wo_time");
                entity.Property(e => e.SubJobDurationJam)
                    .HasMaxLength(5)
                    .IsFixedLength()
                    .HasColumnName("sub_job_duration_jam");
                entity.Property(e => e.SubJobDurationMenit)
                    .HasMaxLength(5)
                    .IsFixedLength()
                    .HasColumnName("sub_job_duration_menit");
                entity.Property(e => e.Trxtype)
                    .HasColumnType("smallint(6)")
                    .HasColumnName("trxtype");
                entity.Property(e => e.PreRevRemarks)
                    .HasMaxLength(255)
                    .HasColumnName("pre_rev_remarks");
                entity.Property(e => e.DeskId)
                    .HasColumnType("int(11)")
                    .HasColumnName("desk_id");
                entity.Property(e => e.IsToday)
                    .HasColumnType("int(11)")
                    .HasColumnName("is_today");
                entity.Property(e => e.IsTodayNote)
                    .HasMaxLength(100)
                    .HasColumnName("is_today_note");
                entity.Property(e => e.NoHpDrv)
                    .HasMaxLength(30)
                    .HasColumnName("no_hp_drv");
                entity.Property(e => e.GolInsentif)
                    .HasMaxLength(10)
                    .HasColumnName("gol_insentif");
                entity.Property(e => e.LokasiParkir)
                    .HasMaxLength(100)
                    .HasColumnName("lokasi_parkir");
            });


            modelBuilder.Entity<TrWoHdrLog>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PRIMARY");
                entity.ToTable("tr_wo_hdr_log");
                entity.Property(e => e.Id).HasColumnType("int(11)").HasColumnName("id");
                entity.Property(e => e.ActionStatus).HasMaxLength(10).HasColumnName("action_status");
                entity.Property(e => e.ActionUser).HasMaxLength(50).HasColumnName("action_user");
                entity.Property(e => e.ActionDate).HasColumnType("datetime").HasColumnName("action_date");
                entity.Property(e => e.Noted).HasMaxLength(1500).HasColumnName("noted");
                entity.Property(e => e.WoNo).HasMaxLength(15).HasColumnName("wo_no");
            });

            modelBuilder.Entity<TrLockingwo>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PRIMARY");
                entity.ToTable("tr_lockingwo");
                entity.Property(e => e.Id).HasColumnType("int(11)").HasColumnName("id");
                entity.Property(e => e.NomorData).HasColumnName("nomor_data");
                entity.Property(e => e.User).HasColumnName("user");
                entity.Property(e => e.Timestamp).HasColumnType("datetime").HasColumnName("timestamp");
            });

            modelBuilder.Entity<ErrLog>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PRIMARY");
                entity.ToTable("err_log");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Modul).HasColumnName("modul");
                entity.Property(e => e.User).HasColumnName("user");
                entity.Property(e => e.IdData).HasColumnName("id_data");
                entity.Property(e => e.ErrMessage).HasColumnName("err_message");
                entity.Property(e => e.CreatedDt).HasColumnName("created_dt");
                entity.Property(e => e.FunctionName).HasColumnName("function_name");
            });

            modelBuilder.Entity<MsLocation>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PRIMARY");
                entity.ToTable("ms_location");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.LocCode).HasMaxLength(15).HasColumnName("loc_code");
                entity.Property(e => e.LocName).HasMaxLength(101).HasColumnName("loc_name");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }

    public static class ServiceProviderFactory
    {
        public static IServiceProvider CreateServiceProvider()
        {
            string environment = AppConfig.Config.Environment.ToUpper();
            string connectionString = environment == "DEV" ?
                               AppConfig.Config.ConfigDev.MkaPool :
                                environment == "PROD" ? AppConfig.Config.ConfigProd.connection :
                               string.Empty;

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDbContextFactory<MkaPoolContext>(options =>
                options.UseMySQL(connectionString));

            return serviceCollection.BuildServiceProvider();
        }

    }

}
