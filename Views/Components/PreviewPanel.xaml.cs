using System.Windows.Controls;

namespace SwitchBlade.Views.Components
{
    public partial class PreviewPanel : System.Windows.Controls.UserControl
    {
        public PreviewPanel()
        {
            InitializeComponent();
        }

        public Canvas PreviewCanvas => ThumbnailCanvas;
    }
}
