namespace InteractiveAI.Backend.Models.DB
{
    public partial class TrWoJobProcess
    {
        public string PreSrcNo { get; set; } = null!;

        public string PreJobId { get; set; } = null!;

        public string PreJobDesc { get; set; } = null!;

        public string TmsDurationJam { get; set; } = null!;

        public string TmsDurationMenit { get; set; } = null!;

        public string PreJobRemarks { get; set; } = null!;

        public int Lnitmseq { get; set; }

        public string TmsGolongan { get; set; } = null!;

        public short TmsGroup { get; set; }

        public string Crusrid { get; set; } = null!;

        public DateTime Creatddt { get; set; }

        public DateTime Createtime { get; set; }

        public string Mdfusrid { get; set; } = null!;

        public DateTime Modifdt { get; set; }

        public DateTime Modiftime { get; set; }

        public short RedoJob { get; set; }

        public string PreSubJobId { get; set; } = null!;

        public string PreSubJobDesc { get; set; } = null!;

        public string SubJobDurationJam { get; set; } = null!;

        public string SubJobDurationMenit { get; set; } = null!;

        public int DexRowId { get; set; }
    }
}
