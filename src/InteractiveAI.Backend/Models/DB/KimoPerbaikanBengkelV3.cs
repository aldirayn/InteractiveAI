using System;
using System.Collections.Generic;

namespace InteractiveAI.Backend.Data;

public partial class KimoPerbaikanBengkelV3
{
    public string Id { get; set; } = null!;

    public string NoPol { get; set; } = null!;

    public string JenisTruck { get; set; } = null!;

    public string? KodeDriverLama { get; set; }

    public string? NamaDriver { get; set; }

    public string Srdnumber { get; set; } = null!;

    public string? LastStatus { get; set; }

    public string? JenisPerbaikan { get; set; }

    public string? SubJenisPerbaikan { get; set; }

    public string? DescPerbaikan { get; set; }

    public string? DraftUser { get; set; }

    public DateTime? DraftDate { get; set; }

    public string? CreateUser { get; set; }

    public DateTime? CreateDate { get; set; }

    public string? AcceptUser { get; set; }

    public DateTime? AcceptDate { get; set; }

    public string? DoneUser { get; set; }

    public DateTime? DoneDate { get; set; }

    public string? CloseUser { get; set; }

    public DateTime? CloseDate { get; set; }

    public string? CancelUser { get; set; }

    public DateTime? CancelDate { get; set; }

    public string? CancelKeterangan { get; set; }

    public string? BatalUser { get; set; }

    public DateTime? BatalDate { get; set; }

    public string? BatalKeterangan { get; set; }

    public DateTime? TimestampEntry { get; set; }
    public int? OdoKm { get; set; }
    public string? TujuanPoolPerbaikan { get; set; }
    public string? KodeDriver { get; set; }
    public string? NoHpDriver { get; set; }
}
