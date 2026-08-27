using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;
using StreamForge.Models;
using StreamForge.Services;

namespace StreamForge;
public partial class StatisticsWindow : Window
{
    private readonly SourceInstance _instance;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    public StatisticsWindow(SourceInstance instance) { InitializeComponent(); _instance = instance; TitleText.Text = $"Statistiche · {instance.Name}"; Chart.Instance = instance; _timer.Tick += (_, _) => Refresh(); Loaded += (_, _) => { LocalizationService.Apply(this, LocalizationService.CurrentLanguage); Refresh(); _timer.Start(); }; Closed += (_, _) => _timer.Stop(); }
    private void Refresh() { var data = StatisticsService.Instance.Get(_instance); Chart.Samples = data; Chart.InvalidateVisual(); CurrentTotalText.Text = _instance.TotalListeners.ToString("N0"); MaximumText.Text = data.Select(x => x.Total).DefaultIfEmpty().Max().ToString("N0"); LastUpdateText.Text = data.Count == 0 ? "In attesa del primo campione" : $"Ultimo campione: {data[^1].TimestampUtc.ToLocalTime():dd/MM/yyyy HH:mm:ss}"; }
    private void Export_Click(object sender, RoutedEventArgs e) { var d = new SaveFileDialog { Filter = "Cartella di lavoro Excel (*.xlsx)|*.xlsx", FileName = $"StreamPAL-{_instance.Name}-72h.xlsx", AddExtension = true }; if (d.ShowDialog() != true) return; try { StatisticsExcelExporter.Export(d.FileName, _instance, StatisticsService.Instance.Get(_instance)); MessageBox.Show("Dati esportati correttamente.", "StreamPAL"); } catch (Exception ex) { MessageBox.Show($"Esportazione non riuscita: {ex.Message}", "StreamPAL"); } }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
