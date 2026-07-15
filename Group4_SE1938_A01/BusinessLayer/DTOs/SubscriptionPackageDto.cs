namespace BusinessLayer.DTOs
{
    public class SubscriptionPackageDto
    {
        public int PackageId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int ExtraTokenAmount { get; set; }
        public string DurationUnit { get; set; } = "Month";
        public int DurationValue { get; set; }
    }
}
