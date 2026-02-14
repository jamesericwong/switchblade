using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SwitchBlade.Contracts;

namespace SwitchBlade.Views.Components
{
    public partial class ResultList : System.Windows.Controls.UserControl
    {
        public event EventHandler<WindowItem>? ActivateItemRequested;
        public event EventHandler<WindowItem>? PreviewItemRequested;

        public ResultList()
        {
            InitializeComponent();
        }

        public double ListActualHeight => ResultsConfig.ActualHeight;

        public System.Windows.Controls.ListBox InnerListBox => ResultsConfig;

        public void ScrollIntoView(object item)
        {
            ResultsConfig.ScrollIntoView(item);
        }

        private void ListBoxItem_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is WindowItem windowItem)
            {
                PreviewItemRequested?.Invoke(this, windowItem);
            }
        }

        private void ListBoxItem_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is WindowItem windowItem)
            {
                ActivateItemRequested?.Invoke(this, windowItem);
            }
        }
    }
}
