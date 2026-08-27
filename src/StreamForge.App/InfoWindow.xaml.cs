using System.Windows;
namespace StreamForge;
public partial class InfoWindow : Window { public InfoWindow() { InitializeComponent(); Loaded += (_,_) => Services.LocalizationService.Apply(this, Services.LocalizationService.CurrentLanguage); } private void Close_Click(object sender, RoutedEventArgs e) => Close(); }
