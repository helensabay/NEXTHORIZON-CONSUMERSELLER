namespace NextHorizon.Models
{
    public class SizeGuideTableItem
    {
        public string Title { get; set; } = string.Empty;
        public string MeasurementUnit { get; set; } = string.Empty;
        public int PhotoOrder { get; set; }
        public List<List<string>> Data { get; set; } = new List<List<string>>();
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class ViewSizeGuideViewModel
    {
        public int ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public string ProductImageUrl { get; set; } = string.Empty;
        public string ProductBrand { get; set; } = string.Empty;
        public string ProductDetails { get; set; } = string.Empty;
        public bool IsPhotoUpload { get; set; }
        public List<string> UploadedPhotoUrls { get; set; } = new List<string>();
        public Dictionary<string, string> PhotoGuideUnitsByUrl { get; set; } = new Dictionary<string, string>();
        public string MeasurementUnit { get; set; } = string.Empty;
        public string PhotoMeasurementUnit { get; set; } = string.Empty;
        public string TableMeasurementUnit { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string TableTitle { get; set; } = string.Empty;
        public List<List<string>> TableData { get; set; } = new List<List<string>>();
        public List<SizeGuideTableItem> Tables { get; set; } = new List<SizeGuideTableItem>();
        public string FitTips { get; set; } = string.Empty;
        public string HowToMeasure { get; set; } = string.Empty;
        public string AdditionalNotes { get; set; } = string.Empty;
    }
}