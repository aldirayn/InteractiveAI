namespace InteractiveAI.Backend.Models.DB.KAGT
{
    public partial class VwAllUnitV3
    {
        public string NoPol { get; set; } = null!;

        public string NoPol2 { get; set; } = null!;

        public string? Ranking { get; set; }

        public string? JenisTruck { get; set; }

        public string? SoNo { get; set; }

        public string? NoRk { get; set; }

        public DateTime? TglMuat { get; set; }

        public string? TmsStatus { get; set; }

        public string? KodeCust { get; set; }

        public string? NamaCust { get; set; }

        public string? JenisBarang { get; set; }

        public decimal? TargetSuhu { get; set; }

        public string? RangeSuhu { get; set; }

        public string? NamaRute { get; set; }

        public string? FlagRuteDalamKota { get; set; }

        public string? TempatMuat { get; set; }

        public string? TempatBongkar { get; set; }

        public string? ProvTujuan { get; set; }

        public string? KodeDriverOld { get; set; }

        public string? KodeDriver { get; set; }

        public string? NoHpDriver { get; set; }

        public string? NoHpBatangan { get; set; }

        public DateTime? TglKir { get; set; }

        public DateTime? TglStnk { get; set; }

        public string? NamaDriver { get; set; }

        public string? GolAc { get; set; }

        public string? Spec { get; set; }

        public ulong? SpecDc { get; set; }

        public ulong? SpecDe { get; set; }

        public string? Ws { get; set; }

        public string? LastDetectedPool { get; set; }

        public DateTime? LastDetectedTime { get; set; }

        public string? LoadName { get; set; }

        public string? Custcodeload { get; set; }

        public decimal? LoadLat { get; set; }

        public decimal? LoadLong { get; set; }

        public string? LoadRadius { get; set; }

        public string? UnloadName { get; set; }

        public string? Custcodeunload { get; set; }

        public decimal? UnloadLat { get; set; }

        public decimal? UnloadLong { get; set; }

        public string? UnloadRadius { get; set; }

        public DateTime? WsTgl { get; set; }

        public string? KetMkt { get; set; }

        public string? DriverBatangan { get; set; }

        public string? SrNumber { get; set; }

        public string? EkorId { get; set; }

        public string? GensetId { get; set; }

        public string? ContainerId { get; set; }

        public string? DriverStatus { get; set; }

        public string? DriverSubStatus { get; set; }

        public ulong? Oliacgabungolimesin { get; set; }

        public string? KodeDriverBatangan { get; set; }

        public string? NoEkor { get; set; }

        public string? NoContainer { get; set; }

        public string? NoGenset { get; set; }

        public DateTime? TglKirEkor { get; set; }

        public int? IsTerpairing { get; set; }

        public string? CustPriority { get; set; }

        public int? ResetKmMaintenance { get; set; }

        public ulong? OcDoubleChamber { get; set; }

        public ulong? OcDry { get; set; }

        public string? Listidrute { get; set; }
    }
}
