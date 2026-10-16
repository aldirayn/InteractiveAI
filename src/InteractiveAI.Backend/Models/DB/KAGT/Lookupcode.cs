using System;
using System.Collections.Generic;

namespace InteractiveAI.Backend.Data.Models.DB.KAGT;

public partial class Lookupcode
{
    public int Id { get; set; }

    public int IdKategori { get; set; }

    public string Nama { get; set; } = null!;

    public int Order { get; set; }

    public string Deskripsi { get; set; } = null!;

    public int AcId { get; set; }

    public string? AcCode { get; set; }

    public string? AcName { get; set; }

    public int? VendorId { get; set; }

    public string DipakaidiAuj { get; set; } = null!;

    public int? ParentId { get; set; }
}
