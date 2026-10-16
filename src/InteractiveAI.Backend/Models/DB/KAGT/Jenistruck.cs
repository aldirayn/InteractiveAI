using System;
using System.Collections.Generic;

namespace InteractiveAI.Backend.Data.Models.DB.KAGT;

public partial class Jenistruck
{
    public int Id { get; set; }

    public string? StrJenisTruck { get; set; }

    public int GolTol { get; set; }

    public string? Alias { get; set; }

    public decimal Biaya { get; set; }

    public int AcInterval { get; set; }

    public decimal? TargetSales { get; set; }
}
