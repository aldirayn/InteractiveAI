using System;
using System.Collections.Generic;

namespace InteractiveAI.Backend.Data;

public partial class TrWoHdrLog
{
    public int Id { get; set; }

    public string? ActionStatus { get; set; }

    public string? ActionUser { get; set; }

    public DateTime? ActionDate { get; set; }

    public string? Noted { get; set; }
    public string? WoNo { get; set; }
}
