using HtmlAgilityPack;
using KvtSu.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ErmenrichParserApp;

public class Parser
{
    private readonly HttpClient client;
    private readonly string HOST = "https://www.levenhuk-opt.ru";

    private static HttpClient GlobalClient = null;
    public static HttpClient Client => GlobalClient;

    private static readonly Dictionary<string, string> brandsMap;

    public Parser()
    {
        var handler = new HttpClientHandler()
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };
        client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
        GlobalClient = client;
    }

    static Parser()
    {
        brandsMap = new Dictionary<string, string>()
        {
            ["Bounty Hunter (Баунти Хантер)"] = "Bounty Hunter",
            ["Bresser (Брессер)"] = "Bresser",
            ["Ermenrich"] = "Ermenrich",
            ["Ermenrich (Эрменрих)"] = "Explore Scientific",
            ["Konus (Конус)"] = "Konus",
            ["Levenhuk (Левенгук)"] = "Levenhuk",
            ["Sky-Watcher (Скай-Вотчер)"] = "Sky-Watcher"
        };
    }

    public async Task<List<Product>> ParseBrandMissingModels(IProgress<double> progress, IEnumerable<string> missingSku)
    {
        await client.GetAsync(HOST);
        var parsedProducts = new List<Product>();

        int current = 0;
        int total = missingSku.Count();

        foreach (var sku in missingSku)
        {
            var product = new Product()
            {
                Sku = sku
            };

            product.Uri = await SearchProductUri(sku);


            if (!string.IsNullOrWhiteSpace(product.Uri))
            {
                await ParseProductDetails(product);
            }

            parsedProducts.Add(product);
            progress?.Report((double)++current / total);
        }

        return parsedProducts;
    }

    private async Task<string?> SearchProductUri(string sku)
    {
        string searchUri = $"{HOST}/search/?q={sku}";
        var doc = await GetDocument(searchUri);
        if (doc.DocumentNode.InnerHtml.Contains("К сожалению, ничего не найдено."))
        {
            return null;
        }

        var match = Regex.Match(doc.DocumentNode.InnerHtml, $"\"article\":\"({sku})\",.*\"url\":\"(.*?)\"");
        if (match.Success)
        {
            string source = match.Value;

            var regex = new Regex(
                "\"article\"\\s*:\\s*\"?(\\d+)\"?(?:(?!\"article\"\\s*:)[\\s\\S])*?\"url\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"",
                RegexOptions.Compiled
            );

            Dictionary<string, string> urls = regex.Matches(source)
                .Cast<Match>()
                .ToDictionary(
                    m => m.Groups[1].Value,
                    m => JsonSerializer.Deserialize<string>(
                        "\"" + HOST + m.Groups[2].Value + "\""
                    )!
                );
            var result = urls[sku];
            return result;
        }
        return null;
    }

    private async Task ParseProductDetails(Product product)
    {
        var doc = await GetDocument(product.Uri);
        if (doc == null)
        {
            return;
        }

        var productSku = doc.DocumentNode
            .SelectSingleNode("//table[@class='product-detail__short-specifications']//td[contains(text(), 'Артикул')]/following-sibling::td")
            ?.InnerText;
        if (string.IsNullOrWhiteSpace(productSku) || productSku != product.Sku)
        {
            throw new ArgumentException($"Product SKU mismatch. Expected: {product.Sku}, Found: {productSku}");
        }

        try
        {
            product.Manufacturer = MapManufacturerName(doc.DocumentNode.SelectSingleNode("//table[@class='product-detail__short-specifications']//a[contains(@href, '/brands/')]")
                .InnerText);
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        product.Name = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText.TrimHtml();
        product.DescriptionMarkup = doc.DocumentNode.SelectSingleNode("//div[@itemprop='description']")?.InnerHtml.TrimHtml();
        var charTableNode = doc.DocumentNode.SelectSingleNode("//div[@id='product-specification']/table[@class='specification-table']");
        if (charTableNode != null)
        {
            product.Characteristics = ParseCharacteristics(charTableNode);

            var packSizeCharacteristic = product.Characteristics.FirstOrDefault(c => c.Name.Equals("Размер упаковки (ДxШxВ)", StringComparison.OrdinalIgnoreCase));

            if (packSizeCharacteristic != null)
            {
                var match = Regex.Match(
                    packSizeCharacteristic.Value,
                    @"(\d+(?:[.,]\d+)?)\s*[xх×]\s*" +
                    @"(\d+(?:[.,]\d+)?)\s*[xх×]\s*" +
                    @"(\d+(?:[.,]\d+)?)"
                );

                if (match.Success)
                {
                    product.Dimensions = new decimal[3];
                    product.Dimensions[0] = decimal.Parse(
                        match.Groups[1].Value.Replace(',', '.'),
                        System.Globalization.CultureInfo.InvariantCulture) * 10;

                    product.Dimensions[1] = decimal.Parse(
                        match.Groups[2].Value.Replace(',', '.'),
                        System.Globalization.CultureInfo.InvariantCulture) * 10;

                    product.Dimensions[2] = decimal.Parse(
                        match.Groups[3].Value.Replace(',', '.'),
                        System.Globalization.CultureInfo.InvariantCulture) * 10;
                }
            }


            var weightCharacteristic = product.Characteristics.FirstOrDefault(c => c.Name.Equals("Вес в упаковке", StringComparison.OrdinalIgnoreCase));
            if (weightCharacteristic != null)
            {
                product.Weight = decimal.Parse(
                        weightCharacteristic.Value
                        .Replace(" кг", string.Empty)
                        .Replace(',', '.'),
                        System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        var reg = Regex.Match(product.Name, $"^(.*?) {product.Manufacturer} (.*)$");
        product.Model = reg.Groups[2].Value;
        product.ProductType = reg.Groups[1].Value;

        var imagesXpatch = "//div[@class='product-detail__gallery']//div[@data-gallery-target]";
        if (doc.DocumentNode.SelectSingleNode(imagesXpatch) != null)
        {
            var images = doc.DocumentNode.SelectNodes(imagesXpatch)
                .Select(img => img.GetAttributeValue("href", string.Empty))
                .Where(img => !string.IsNullOrWhiteSpace(img))

                .ToArray();

            product.Images.AddRange(images.Select(img => img.StartsWith("http") ? img : HOST + img));
        }

        if (doc.DocumentNode.SelectSingleNode("//a[@class='download-files__file']") != null)
        {
            var instructions = doc.DocumentNode.SelectNodes("//a[@class='download-files__file']")
                //.Where(a => Regex.IsMatch(a.InnerText.TrimHtml(), @"^(.*?)\(pdf\).*$"))
                .Select(a => new PdfInstruction()
                {
                    Name = a.InnerText.TrimHtml(),
                    Uri = a.GetAttributeValue("href", null)
                })
                .Where(pdf => pdf.Uri.ToLower().EndsWith(".pdf") &&
                    !pdf.Name.StartsWith("Измерительные инструменты Ermenrich") &&
                    (pdf.Name.Contains("Руководство") || pdf.Name.Contains("Инструкция")))
                .ToArray();

            product.Instructions.AddRange(instructions);
        }
    }

    public static List<Characteristic> ParseCharacteristics(HtmlNode tableNode)
    {
        var result = new List<Characteristic>();

        if (tableNode == null)
            return result;

        string currentGroup = string.Empty;

        var rows = tableNode.SelectNodes(
            ".//tr[contains(concat(' ', normalize-space(@class), ' '), ' specification-table__item ')]"
        );

        if (rows == null)
            return result;

        foreach (var row in rows)
        {
            var titleNode = row.SelectSingleNode(
                ".//td[contains(concat(' ', normalize-space(@class), ' '), ' specification-table__title ')]"
            );

            var valueNode = row.SelectSingleNode(
                ".//td[contains(concat(' ', normalize-space(@class), ' '), ' specification-table__value ')]"
            );

            if (titleNode == null)
                continue;

            string name = NormalizeText(titleNode.InnerText);
            string value = valueNode != null
                ? NormalizeText(valueNode.InnerText)
                : string.Empty;

            if (string.IsNullOrWhiteSpace(name))
                continue;

            // Заголовок группы: название есть, значение отсутствует.
            if (string.IsNullOrWhiteSpace(value))
            {
                currentGroup = name;
                continue;
            }

            result.Add(new Characteristic
            {
                Group = currentGroup,
                Name = name,
                Value = value
            });


        }

        return result;
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ");

        return text.Trim();
    }

    private static string MapManufacturerName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !brandsMap.TryGetValue(name, out var mappedName))
        {
            throw new ArgumentOutOfRangeException("Unknown manufacturer name: " + name);
        }

        return mappedName;
    }

    private async Task<HtmlDocument> GetDocument(string uri)
    {
        try
        {
            var doc = new HtmlDocument();

            var str = await client.GetStringAsync(uri);

            doc.LoadHtml(str);
            return doc;
        }
        catch
        {
            return null;
        }

    }
}
