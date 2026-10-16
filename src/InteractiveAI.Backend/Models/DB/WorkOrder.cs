namespace InteractiveAI.Backend.Models.DB
{
    public class WorkOrder
    {
        public long Id { get; set; }
        public string WoNumber { get; set; } = string.Empty;
        public Guid SessionId { get; set; }
        public string Model { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string UnitNumber { get; set; } = string.Empty;
        public string Complaint { get; set; } = string.Empty;
        public string QrKimo { get; set; } = string.Empty;
        public string Status { get; set; } = "OPEN";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string DriverCode { get; set; }
        public string RepairCategories { get; set; }
        public string ParkingLocation { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public Dictionary<string, string>? KeluhanRows { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public Dictionary<string, string>? WoNumbers { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int WoCount { get; set; }
    }
}
