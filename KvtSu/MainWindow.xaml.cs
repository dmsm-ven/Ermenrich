using System;
using System.Windows;

namespace KvtSu;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly Manager manager = new("products.json");

    private int PID
    {
        get
        {
            if (int.TryParse(txtProductId.Text, out var pid))
            {
                if (pid >= 0)
                {
                    return pid;
                }
            }
            throw new ArgumentOutOfRangeException(nameof(PID));
        }
    }

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void btnParseBrand_Click(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        try
        {
            await manager.ParseBrand(new Progress<double>(v => pbIndicator.Value = v), (bool)chbxParseOnlyListSku.IsChecked);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void btnGeneralExport_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("PID CHECK WARNING");
        string data = manager.GetGeneralExport(PID);
        Clipboard.SetText(data);
    }

    private void btnAdditionalImages_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Изображения должны быть преобразованы в jpg");
        string data = manager.GetAdditionalImagesExport(PID);
        Clipboard.SetText(data);
    }

    private void btnAdditionalImagesSql_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Изображения должны быть преобразованы в jpg");
        string data = manager.GetImagesSql();
        Clipboard.SetText(data);
    }

    private void btnSqlDescription_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetDescriptionSql();
        Clipboard.SetText(data);
    }

    private void btnSqlPdf_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetPdfSql();
        Clipboard.SetText(data);
    }

    private async void btnDownloadResources_Click(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        try
        {
            await manager.DownloadResources("resources", new Progress<double>(v => pbIndicator.Value = v));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void btnClipboardToCategories_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetCategories(Clipboard.GetText());
        Clipboard.SetText(data);
    }

    private void btnGetDimensionsSql_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetDimensionsSql();
        Clipboard.SetText(data);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("PID CHECK WARNING");
    }
}
