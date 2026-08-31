using System.Windows;
using System.Windows.Controls;
using System.Linq;
using StreamForge.Services;

namespace StreamForge;

public partial class RegistrationWindow : Window
{
    private readonly TrialLicenseService _license;
    public bool RegisteredNow { get; private set; }

    public RegistrationWindow(TrialLicenseService license, string language)
    {
        InitializeComponent();
        _license = license;
        InstallationCodeBox.Text = license.InstallationCode;
        ApplyLanguage(language);
        RefreshStatus();
    }

    private void ApplyLanguage(string language)
    {
        var texts = language switch
        {
            "en" => ("This is a test version. You may request the complete version free of charge for personal use by emailing max@freewaves.it. It cannot be requested for professional use.", "Installation code", "Registration code", "Register", "Close", "Copy"),
            "es" => ("Esta es una versión de prueba. Puedes solicitar gratuitamente la versión completa para uso personal escribiendo a max@freewaves.it. No puede solicitarse para uso profesional.", "Código de instalación", "Código de registro", "Registrar", "Cerrar", "Copiar"),
            "fr" => ("Ceci est une version d’essai. Vous pouvez demander gratuitement la version complète pour un usage personnel en écrivant à max@freewaves.it. Elle ne peut pas être demandée pour un usage professionnel.", "Code d’installation", "Code d’enregistrement", "Enregistrer", "Fermer", "Copier"),
            _ => ("Questa è una versione di test. Puoi richiedere gratuitamente la versione completa per uso personale inviando una mail a max@freewaves.it. Non è possibile richiederla per uso professionale.", "Codice installazione", "Codice di registrazione", "Registra", "Chiudi", "Copia")
        };
        NoticeText.Text = texts.Item1; InstallationLabel.Text = texts.Item2; RegistrationLabel.Text = texts.Item3; RegisterButton.Content = texts.Item4; CloseButton.Content = texts.Item5;
        if (InstallationCodeBox.Parent is DockPanel panel && panel.Children.OfType<Button>().FirstOrDefault() is { } copy) copy.Content = texts.Item6;
    }

    private void RefreshStatus()
    {
        if (_license.IsRegistered) { StatusText.Text = "StreamPAL è registrato su questo PC."; RegisterButton.IsEnabled = false; }
        else if (_license.IsExpired) StatusText.Text = "I 60 minuti di prova sono terminati. Inserisci il codice di registrazione per continuare.";
        else StatusText.Text = $"Tempo di prova rimanente: {_license.Remaining:hh\\:mm\\:ss}";
    }

    private void CopyInstallation_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(_license.InstallationCode);
    private async void Register_Click(object sender, RoutedEventArgs e)
    {
        RegisterButton.IsEnabled = false;
        var result = await _license.TryRegisterAsync(RegistrationCodeBox.Text);
        RegisterButton.IsEnabled = true;
        if (!result.Success) { MessageBox.Show(result.Error, "StreamPAL", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        RegisteredNow = true; MessageBox.Show("Registrazione completata. Grazie.", "StreamPAL", MessageBoxButton.OK, MessageBoxImage.Information); DialogResult = true;
    }
}
