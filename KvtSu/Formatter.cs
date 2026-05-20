using HtmlAgilityPack;
using KvtSu;
using KvtSu.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace ErmenrichParserApp;

public class Formatter
{
    private readonly IReadOnlyList<Product> products;
    private readonly int startId;

    public Formatter(IEnumerable<Product> products, int startId)
    {
        this.products = products.ToList();
        this.startId = startId;
    }
    internal string GetGeneralExport()
    {
        var sb = new StringBuilder();

        int id = startId;
        foreach (var product in products)
        {
            string mainImage = GetMainImage(product);

            string caption = $"{product.Model} {product.Manufacturer} {product.ProductType}";
            string keyword = Regex.Replace(Transliteration.Front(caption, true), "-{2,}", "-").Trim('-') + "-art-" + product.Sku;
            string meta_title = $"{caption} купить в Санкт-Петербурге";
            string meta_desc = $"{meta_title} с доставкой по России";

            string length = product.HasDimensions ? product.Dimensions[0].ToString("F0").Replace(",", ".") : "0";
            string width = product.HasDimensions ? product.Dimensions[1].ToString("F0").Replace(",", ".") : "0";
            string height = product.HasDimensions ? product.Dimensions[2].ToString("F0").Replace(",", ".") : "0";

            sb
                .AppendTab(id.ToString())   //product_id
                .AppendTab(product.Name)   //name(ru)
                .AppendTab(string.Empty)   //categories
                .AppendTab(product.Sku)   //sku
                .AppendTab(string.Empty)   //upc
                .AppendTab(string.Empty)   //ean
                .AppendTab(string.Empty)   //jan
                .AppendTab(string.Empty)   //isbn
                .AppendTab(string.Empty)   //mpn
                .AppendTab(string.Empty)   //location
                .AppendTab("0")   //quantity
                .AppendTab(product.Model)   //model
                .AppendTab(product.Manufacturer)   //manufacturer
                .AppendTab(mainImage)   //image_name
                .AppendTab("yes")   //shipping
                .AppendTab("0")   //price
                .AppendTab("0")   //points
                .AppendTab(string.Empty)   //date_added
                .AppendTab(string.Empty)   //date_modified
                .AppendTab(string.Empty)   //date_available
                .AppendTab(product.Weight.ToString("F4"))   //weight
                .AppendTab("kg")   //weight_unit
                .AppendTab(length)    //length
                .AppendTab(width)    //width
                .AppendTab(height)    //height
                .AppendTab("mm")   //length_unit
                .AppendTab("true")   //status
                .AppendTab("0")   //tax_class_id
                .AppendTab(keyword)   //seo_keyword
                .AppendTab(string.Empty)   //description(ru)
                .AppendTab("0")   //category_show(ru)
                .AppendTab("0")   //main_product(ru)
                .AppendTab(meta_title)   //meta_title(ru)
                .AppendTab(meta_desc)   //meta_description(ru)
                .AppendTab(string.Empty)   //meta_keywords(ru)
                .AppendTab("10")   //stock_status_id
                .AppendTab("0,1,2,3,4,5,6,7,8")   //store_ids
                .AppendTab("0:,1:,2:,3:,4:,5:,6:,7:,8:")   //layout
                .AppendTab(string.Empty)   //related_ids
                .AppendTab(string.Empty)   //adjacent_ids
                .AppendTab(string.Empty)   //tags(ru)
                .AppendTab("1")   //sort_order
                .AppendTab("true")   //subtract
                .AppendLine("1"); //minimum

            id++;
        }

        var result = sb.ToString();
        return result;
    }
    internal string GetAdditionalImagesExport()
    {
        var sb = new StringBuilder();

        int id = startId;
        foreach (var product in products)
        {
            int sort_order = 0;
            foreach (var image in product.Images.Skip(1))
            {
                string imagePath = $"catalog/{product.ManufacturerFtpPath}/products/" + image.CreateMD5() + ".jpg";
                sb.AppendLine($"{id}\t{imagePath}\t{sort_order++}");
            }

            id++;
        }

        var result = sb.ToString();
        return result;
    }
    internal string GetDescriptionSql()
    {
        var sb = new StringBuilder();

        foreach (var product in products)
        {
            string product_id = GetProductIdFromSku(product);
            sb.AppendLine($"UPDATE oc_product_description SET description = '{HttpUtility.HtmlEncode(BuildDescriptionForProduct(product))}' WHERE product_id = {product_id};");
        }

        var result = sb.ToString().TrimHtml() + ";";
        return result;
    }
    private string GetMainImage(Product product)
    {
        string? firstImage = product.Images.FirstOrDefault();

        if (firstImage != null)
        {
            return $"catalog/{product.ManufacturerFtpPath}/products/" + firstImage.CreateMD5() + ".jpg";
        }
        else
        {
            return "catalog/placeholder.jpg";
        }
    }
    private string BuildDescriptionForProduct(Product product)
    {
        //throw new Exception("Нужно удалить в начале описания вставляет пустой <p></p>");

        var sb = new StringBuilder();

        sb.AppendLine(product.DescriptionMarkup);

        if (product.SpecificationTableMarkup != null)
        {
            sb.AppendLine("<h2>Технические характеристики</h2>");
            sb.AppendLine("<div class=\"table-responsive\">");
            sb.AppendLine(product.SpecificationTableMarkup.Replace("<table ", "<table class=\"table\""));
            sb.Append("</div>");
        }

        string result = sb.ToString();

        var doc = new HtmlDocument();
        doc.LoadHtml(result);


        if (doc.DocumentNode.SelectSingleNode("//iframe") != null)
        {
            doc.DocumentNode.SelectNodes("//iframe")?.ToList().ForEach(i => i.Remove());
            result = doc.DocumentNode.InnerHtml.TrimHtml();
        }

        if (result.Contains("<a"))
        {
            result = Regex.Replace(result, "<a(.*?)>(.*?)</a>", "<strong>$2</strong>");
        }

        if (result.Contains("<img"))
        {
            result = Regex.Replace(result, "src=\"(.*?)\"", "src=\"\"");
        }

        if (result.StartsWith("<p><p>"))
        {
            result = result.Substring("<p>".Length).Trim();
        }

        return result;
    }

    public string GetPdfSql()
    {
        var sb = new StringBuilder();

        sb.AppendLine("INSERT INTO oc_product_pdf (product_id, name, path) VALUES");

        foreach (var p in products)
        {
            string product_id = GetProductIdFromSku(p);

            foreach (var pdf in p.Instructions)
            {
                string pdfName = pdf.Name.Substring(0, pdf.Name.IndexOf("(pdf)")).Trim();
                string pdfMd5Name = pdf.Uri.CreateMD5() + ".pdf";
                string path = $"https://disk.yandex.ru/d/V1KNVJO3SY3ROw/{p.ManufacturerFtpPath}/{pdfMd5Name}";
                sb.AppendLine($"({product_id}, '{pdfName}', '{path}'),");
            }
        }

        var result = sb.ToString().Trim('\r', '\n', '\t', ' ', ',') + ";";

        return result;
    }

    internal string GetImagesSql()
    {
        var sb = new StringBuilder();

        foreach (var product in products)
        {
            if (product.Images.Count == 0)
            {
                continue;
            }

            string product_id = GetProductIdFromSku(product);
            string imagePath = $"catalog/{product.ManufacturerFtpPath}/products/" + product.Images.First().CreateMD5() + ".jpg";
            int sort_order = 0;

            sb.AppendLine($"UPDATE oc_product SET image = '{imagePath}' WHERE product_id = {product_id};");

            foreach (var image in product.Images.Skip(1))
            {
                imagePath = $"catalog/{product.ManufacturerFtpPath}/products/" + image.CreateMD5() + ".jpg";

                sb.Append($"INSERT INTO oc_product_image (product_id, image, sort_order) VALUES ");
                sb.AppendLine($"({product_id}, '{imagePath}', {sort_order++});");
            }
        }

        var result = sb.ToString();
        return result;
    }

    private string GetProductIdFromSku(Product p)
    {
        return $"(SELECT product_id FROM oc_product WHERE sku = '{p.Sku}' AND manufacturer_id IN (SELECT manufacturer_id FROM oc_manufacturer WHERE name IN ('Levenhuk', 'Ermenrich')))";
    }

    internal string GetDimensionsSql()
    {
        var sb = new StringBuilder();

        foreach (var product in products)
        {
            string product_id = GetProductIdFromSku(product);

            string length = product.Dimensions != null ? product.Dimensions[0].ToString().Replace(",", ".") : "0";
            string width = product.Dimensions != null ? product.Dimensions[1].ToString().Replace(",", ".") : "0";
            string height = product.Dimensions != null ? product.Dimensions[2].ToString().Replace(",", ".") : "0";
            string weight = product.Weight.ToString().Replace(",", ".");

            sb.AppendLine($"UPDATE oc_product SET length = '{length}', width = '{width}', height = '{height}', weight = '{weight}' WHERE product_id = {product_id};");
        }

        return sb.ToString();
    }
}
