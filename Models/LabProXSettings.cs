using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabReportApp.Models
{
    public static class LabProXJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public class LabProXSettings
    {
        public CustomerBranding Customer { get; set; } = new();
        public PdfTemplateSettings Bill { get; set; } = PdfTemplateSettings.CreateBillDefaults();
        public PdfTemplateSettings Report { get; set; } = PdfTemplateSettings.CreateReportDefaults();

        public LabProXSettings Clone()
        {
            string json = JsonSerializer.Serialize(this, LabProXJson.Options);
            return JsonSerializer.Deserialize<LabProXSettings>(json, LabProXJson.Options) ?? new LabProXSettings();
        }
    }
}
