using System.Windows.Media;

namespace SwitchBlade.Services
{
    /// <summary>
    /// App-only icon extraction seam (WPF ImageSource). Lives in the main assembly —
    /// not part of the shared Contracts kernel consumed by plugins and the UIA worker.
    /// </summary>
    public interface IIconExtractor
    {
        ImageSource? ExtractIcon(string executablePath);
    }
}
