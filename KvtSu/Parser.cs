using HtmlAgilityPack;
using KvtSu.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ErmenrichParserApp;

public class Parser
{
    private readonly HttpClient client;
    private readonly string HOST = "https://www.levenhuk.ru";

    private static HttpClient GlobalClient = null;
    public static HttpClient Client => GlobalClient;

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

    public async Task<List<Product>> ParseBrandMissingModels(IProgress<double> progress, IEnumerable<string> missingSku, string subBrandName)
    {
        await client.GetAsync(HOST);
        var parsedProducts = new List<Product>();

        int current = 0;
        int total = missingSku.Count();
        var dic = await GetSkuToDetailsPageForBrand(missingSku, subBrandName.ToLower());

        foreach (var sku in missingSku)
        {
            var product = new Product()
            {
                Manufacturer = subBrandName,
                Sku = sku
            };

            if (dic.TryGetValue(sku, out var uri))
            {
                product.Uri = uri;
            }
            else
            {
                product.Uri = await SearchProductUri(sku);
            }


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
        string searchUri = $"https://www.levenhuk.ru/search/?q={sku}";
        var doc = await GetDocument(searchUri);
        if (doc.DocumentNode.InnerHtml.Contains("К сожалению, ничего не найдено."))
        {
            return null;
        }

        var match = Regex.Match(doc.DocumentNode.InnerHtml, "\"article\":\"(\\d+)\",.*\"url\":\"(.*?)\"");
        if (match.Success)
        {
            string href = HOST + match.Groups[2].Value.Replace("\\", string.Empty).Trim();
            return href;
        }
        return null;
    }

    private async Task<Dictionary<string, string>> GetSkuToDetailsPageForBrand(
        IEnumerable<string> missingSkus, string subBrandName)
    {
        var doc = await GetDocument($"{HOST}/{subBrandName}");
        if (doc == null)
        {
            return new();
        }
        Dictionary<string, string> dic = new();
        try
        {
            foreach (var sku in missingSkus)
            {
                //string searchXPatch = $"//script[contains(text(), '\"id\": \"{sku}\"')]/../div[@class='catalog-item']/div[@class='opis-item']/a";
                string searchXPatch = $"//div[@class='catalog-item']//img[contains(@src, '/images/products/medium/0/{sku}_')]/..";
                var href = doc.DocumentNode.SelectSingleNode(searchXPatch)?.GetAttributeValue("href", null);
                if (!string.IsNullOrWhiteSpace(href))
                {
                    dic[sku] = HOST + href;
                }
            }
        }
        catch
        {
            //ignore
        }

        return dic;
    }

    private async Task ParseProductDetails(Product product)
    {
        var doc = await GetDocument(product.Uri);
        if (doc == null)
        {
            return;
        }

        product.Name = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText.TrimHtml();
        product.DescriptionMarkup = doc.DocumentNode.SelectSingleNode("//div[@itemprop='description']")?.InnerHtml.TrimHtml();
        product.SpecificationTableMarkup = doc.DocumentNode.SelectSingleNode("//table[@id='specification']")?.OuterHtml.TrimHtml();

        var reg = Regex.Match(product.Name, $"^(.*?) {product.Manufacturer} (.*)$");
        product.Model = reg.Groups[2].Value;
        product.ProductType = reg.Groups[1].Value;

        string? weightString = doc.DocumentNode.SelectSingleNode("//li/span[contains(text(), 'Вес в упаковке: ')]")
            ?.ParentNode
            ?.InnerText
            ?.Replace("&times;", "x");

        if (weightString != null && weightString.Contains("кг."))
        {
            var weightRegex = Regex.Match(weightString, "Вес в упаковке: (.*?) кг.");
            if (weightRegex.Success && decimal.TryParse(weightRegex.Groups[1].Value, out var weight))
            {
                product.Weight = weight;
            }
        }

        string dimensionsString = doc.DocumentNode
            .SelectSingleNode("//li/span[contains(text(), 'Размер упаковки (ДхШхВ): ')]")
            ?.ParentNode
            ?.InnerText
            ?.Replace("&times;", "x");

        if (dimensionsString != null && dimensionsString.Contains("см."))
        {
            var dimensionsRegexp = Regex.Match(dimensionsString, @"Размер упаковки \(ДхШхВ\): (.*?) см.");

            var dimensions = dimensionsRegexp.Groups[1].Value.Split('x').Select(v => decimal.Parse(v) * 10).ToArray();

            if (dimensions.Length == 3)
            {
                product.Dimensions = dimensions;
            }
        }

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
