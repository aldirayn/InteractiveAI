using System;
using System.Collections.Generic;

namespace InteractiveAI.Backend.Data.Models.DB.KAGT;

public partial class VwDefaultVendor
{
    public string? Plate { get; set; }

    public string? JenisTruck { get; set; }

    public string? Vendor { get; set; }

    public string DefaultVendor { get; set; } = null!;

    public string? DeviceId { get; set; }

    public int? StatusDel { get; set; }
}
