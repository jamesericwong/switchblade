using System.Windows.Media;

namespace SwitchBlade.Contracts
{
    public interface IIconExtractor
    {
        ImageSource? ExtractIcon(string executablePath);
    }
}
