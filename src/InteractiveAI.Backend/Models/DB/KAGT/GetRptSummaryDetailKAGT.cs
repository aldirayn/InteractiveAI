using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InteractiveAI.Backend.Data.Models.DB.KAGT
{
    public class GetRptSummaryDetailKAGT
    {
        public string? RptTimestamp { get; set; }
        public string? UnitKontrak { get; set; }
        public string? KontrakCust { get; set; }
        public string? Area { get; set; }
        public string? Nopol { get; set; }
        public string? JenisTruck { get; set; }
        public string? Status { get; set; }
        public string? PopupText { get; set; }
        public string? JenisTruck2 { get; set; }
    }
}
