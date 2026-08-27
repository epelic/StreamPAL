using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StreamForge.Services;
public static class LocalizationService
{
    public static string CurrentLanguage { get; private set; } = "it";
    private static readonly Dictionary<string, Dictionary<string, string>> Words = new()
    {
        ["en"] = new() { ["File"]="File",["Carica configurazione…"]="Load configuration…",["Salva configurazione"]="Save configuration",["Esporta configurazione…"]="Export configuration…",["Esporta statistiche…"]="Export statistics…",["Avvia con Windows"]="Start with Windows",["Avvia tutto con Windows"]="Start all with Windows",["Salva setup corrente"]="Save current setup",["Esci"]="Exit",["Lingua"]="Language",["Info"]="About",["Informazioni su StreamPAL"]="About StreamPAL",["Nome istanza"]="Instance name",["Tipo sorgente condivisa"]="Shared source type",["Dispositivo, file o URL"]="Device, file or URL",["Ingresso kHz"]="Input kHz",["Statistiche"]="Statistics",["Duplica"]="Duplicate",["Rimuovi"]="Remove",["Nome encoder"]="Encoder name",["Canali dalla sorgente"]="Source channels",["Server"]="Server",["Password"]="Password",["Metadata"]="Metadata",["Titolo corrente"]="Current title",["Nome stazione"]="Station name",["Descrizione"]="Description",["URL stazione"]="Station URL",["Genere"]="Genre",["Avvia streaming"]="Start streaming",["Ferma"]="Stop",["Salva"]="Save",["Esporta tutto"]="Export all",["Importa"]="Import",["Ascoltatori per stream · ultime 72 ore · aggiornamento in tempo reale"]="Listeners per stream · last 72 hours · real-time updates",["Totale adesso"]="Current total",["Massimo totale 72h"]="72h maximum total",["Esporta Excel"]="Export to Excel",["Chiudi"]="Close" },
        ["es"] = new() { ["File"]="Archivo",["Carica configurazione…"]="Cargar configuración…",["Salva configurazione"]="Guardar configuración",["Esporta configurazione…"]="Exportar configuración…",["Esporta statistiche…"]="Exportar estadísticas…",["Avvia con Windows"]="Iniciar con Windows",["Avvia tutto con Windows"]="Iniciar todo con Windows",["Salva setup corrente"]="Guardar configuración actual",["Esci"]="Salir",["Lingua"]="Idioma",["Info"]="Información",["Informazioni su StreamPAL"]="Acerca de StreamPAL",["Nome istanza"]="Nombre de instancia",["Tipo sorgente condivisa"]="Tipo de fuente compartida",["Dispositivo, file o URL"]="Dispositivo, archivo o URL",["Ingresso kHz"]="Entrada kHz",["Statistiche"]="Estadísticas",["Duplica"]="Duplicar",["Rimuovi"]="Eliminar",["Nome encoder"]="Nombre del codificador",["Canali dalla sorgente"]="Canales de fuente",["Server"]="Servidor",["Password"]="Contraseña",["Metadata"]="Metadatos",["Titolo corrente"]="Título actual",["Nome stazione"]="Nombre de emisora",["Descrizione"]="Descripción",["URL stazione"]="URL de emisora",["Genere"]="Género",["Avvia streaming"]="Iniciar transmisión",["Ferma"]="Detener",["Salva"]="Guardar",["Esporta tutto"]="Exportar todo",["Importa"]="Importar",["Esporta Excel"]="Exportar a Excel",["Chiudi"]="Cerrar" },
        ["fr"] = new() { ["File"]="Fichier",["Carica configurazione…"]="Charger la configuration…",["Salva configurazione"]="Enregistrer la configuration",["Esporta configurazione…"]="Exporter la configuration…",["Esporta statistiche…"]="Exporter les statistiques…",["Avvia con Windows"]="Démarrer avec Windows",["Avvia tutto con Windows"]="Tout démarrer avec Windows",["Salva setup corrente"]="Enregistrer la configuration actuelle",["Esci"]="Quitter",["Lingua"]="Langue",["Info"]="À propos",["Informazioni su StreamPAL"]="À propos de StreamPAL",["Nome istanza"]="Nom de l’instance",["Tipo sorgente condivisa"]="Type de source partagée",["Dispositivo, file o URL"]="Périphérique, fichier ou URL",["Ingresso kHz"]="Entrée kHz",["Statistiche"]="Statistiques",["Duplica"]="Dupliquer",["Rimuovi"]="Supprimer",["Nome encoder"]="Nom de l’encodeur",["Canali dalla sorgente"]="Canaux source",["Server"]="Serveur",["Password"]="Mot de passe",["Metadata"]="Métadonnées",["Titolo corrente"]="Titre actuel",["Nome stazione"]="Nom de la station",["Descrizione"]="Description",["URL stazione"]="URL de la station",["Genere"]="Genre",["Avvia streaming"]="Démarrer la diffusion",["Ferma"]="Arrêter",["Salva"]="Enregistrer",["Esporta tutto"]="Tout exporter",["Importa"]="Importer",["Esporta Excel"]="Exporter vers Excel",["Chiudi"]="Fermer" }
    };
    static LocalizationService()
    {
        Words["en"]["Test di stabilità…"] = "Stability test…";
        Words["es"]["Test di stabilità…"] = "Prueba de estabilidad…";
        Words["fr"]["Test di stabilità…"] = "Test de stabilité…";
        Words["en"]["Uscita codec"] = "Codec output";
        Words["es"]["Uscita codec"] = "Salida del códec";
        Words["fr"]["Uscita codec"] = "Sortie du codec";
    }
    public static void Apply(DependencyObject root, string language)
    {
        CurrentLanguage = language;
        if (language == "it") return;
        if (!Words.TryGetValue(language, out var words)) return;
        Visit(root, words, []);
    }
    private static string Translate(string text, Dictionary<string,string> words)
    {
        var source = text;
        foreach (var dictionary in Words.Values) { var match = dictionary.FirstOrDefault(x => x.Value == text); if (!string.IsNullOrEmpty(match.Key)) { source = match.Key; break; } }
        return words.GetValueOrDefault(source, source);
    }
    private static void Visit(DependencyObject node, Dictionary<string,string> words, HashSet<DependencyObject> visited)
    {
        if (!visited.Add(node)) return;
        if (node is TextBlock t) t.Text = Translate(t.Text, words);
        if (node is ContentControl c && c.Content is string s) c.Content = Translate(s, words);
        if (node is HeaderedItemsControl h && h.Header is string hs) h.Header = Translate(hs, words);
        if (node is DataGrid grid) foreach (var col in grid.Columns) if (col.Header is string ch) col.Header = Translate(ch, words);
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) Visit(child, words, visited);
        for (var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) Visit(VisualTreeHelper.GetChild(node,i), words, visited);
    }
}
