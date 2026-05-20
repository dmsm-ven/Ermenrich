using ErmenrichParserApp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KvtSu;

public partial class Manager
{
    private readonly Parser parser;
    private readonly ResourceDownloader resourceDownloader;
    private readonly string storageFile;
    private List<Product> products;

    public Manager(string storageFile)
    {
        parser = new Parser();
        resourceDownloader = new ResourceDownloader();
        this.storageFile = storageFile;
        Load();
    }

    public async Task ParseBrand(IProgress<double> progress, bool parseOnlyInList)
    {
        HashSet<string> skuWhiteList = new();
        if (parseOnlyInList)
        {
            skuWhiteList = File.ReadAllLines("sku_white_list.txt").ToHashSet();
        }

        var list = await parser.ParseBrandMissingModels(progress, skuWhiteList, subBrandName: "Ermenrich");
        Save(list);
        products = list;
    }

    public string GetDescriptionSql()
    {
        var formatter = new Formatter(products, -1);
        return formatter.GetDescriptionSql();
    }

    public string GetPdfSql()
    {
        var formatter = new Formatter(products, -1);
        return formatter.GetPdfSql();
    }

    public string GetImagesSql()
    {
        var formatter = new Formatter(products, -1);
        return formatter.GetImagesSql();
    }

    public string GetAdditionalImagesExport(int pid)
    {
        var formatter = new Formatter(products, pid);
        return formatter.GetAdditionalImagesExport();
    }

    public string GetGeneralExport(int pid)
    {
        var formatter = new Formatter(products, pid);
        return formatter.GetGeneralExport();
    }

    private async Task Save(List<Product> productsToSave)
    {
        if (productsToSave != null)
        {
            var str = JsonSerializer.Serialize(productsToSave);
            File.WriteAllText(storageFile, str);
        }
    }

    private void Load()
    {
        if (File.Exists(storageFile))
        {
            var str = File.ReadAllText(storageFile);
            products = JsonSerializer.Deserialize<List<Product>>(str);
        }
    }

    internal async Task DownloadResources(string resourceFolder, IProgress<double> progress)
    {
        await resourceDownloader.DownloadResource(products, resourceFolder, progress);
    }

    internal string GetCategories(string str)
    {
        var lines = str
            .Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line
                .Split('\t')
                .Where(cell => !string.IsNullOrWhiteSpace(cell))
                .Select(v => int.Parse(v))
                .ToArray()
                )
            .ToArray();

        var sb = new StringBuilder();

        sb.AppendLine("INSERT INTO oc_product_to_category (product_id, category_id, main_category) VALUES");

        foreach (var line in lines)
        {
            int product_id = line[0];
            int first_category_id = line[1];

            foreach (var category_id in line.Skip(1))
            {
                sb.AppendLine($"({product_id}, {category_id}, {(category_id == first_category_id ? "1" : "0")}),");
            }
        }

        return sb.ToString().Trim('\r', '\n', '\t', ' ', ',') + ";";
    }

    internal string GetDimensionsSql()
    {
        var formatter = new Formatter(products, -1);
        return formatter.GetDimensionsSql();
    }
}
