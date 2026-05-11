namespace MyAspNetApp.Models
{
    public class ViewSizeGuideViewModel
    {
        public int ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public bool IsPhotoUpload { get; set; }
        public List<string> UploadedPhotoUrls { get; set; } = new List<string>();
        public string MeasurementUnit { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string TableTitle { get; set; } = string.Empty;
        public List<List<string>> TableData { get; set; } = new List<List<string>>();
        public string FitTips { get; set; } = string.Empty;
        public string HowToMeasure { get; set; } = string.Empty;
        public string AdditionalNotes { get; set; } = string.Empty;
    }
}
