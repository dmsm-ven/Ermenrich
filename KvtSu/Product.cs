using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ErmenrichParserApp;

public class Product
{
    public string Uri { get; set; }
    public string Sku { get; set; }
    public string Model { get; set; }
    public string Name { get; set; }
    public string ProductType { get; set; }

    public List<string> Images { get; set; } = new();
    public List<PdfInstruction> Instructions { get; set; } = new();
    public string Manufacturer { get; set; }

    public string? DescriptionMarkup { get; set; }
    public List<Characteristic> Characteristics { get; set; } = new();

    public decimal[] Dimensions { get; set; }

    public bool HasDimensions => Dimensions != null && Dimensions.Length == 3;

    public decimal Weight { get; set; }

    [JsonIgnore]
    public string ManufacturerFtpPath => Manufacturer.ToLower();
}

public class Characteristic
{
    public string Group { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class PdfInstruction
{
    public string Name { get; set; }
    public string Uri { get; set; }
}
