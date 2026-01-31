using System.Windows.Controls;

namespace SwitchBlade.Views.Components
{
    public partial class SearchBar : System.Windows.Controls.UserControl
    {
        public SearchBar()
        {
            InitializeComponent();
        }

        public void FocusInput()
        {
            InputBox.Focus();
        }
    }
}
