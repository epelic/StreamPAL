using Avalonia.Controls;

namespace StreamPAL.Linux;

internal static class AvaloniaCompatibility
{
    public static void Refresh(this ItemCollection items)
    {
        // Avalonia aggiorna la vista alla successiva iterazione del dispatcher.
    }
}
