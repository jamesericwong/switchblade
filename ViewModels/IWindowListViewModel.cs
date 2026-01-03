using System.Collections.ObjectModel;
using SwitchBlade.Contracts;

namespace SwitchBlade.ViewModels
{
    public interface IWindowListViewModel
    {
        ObservableCollection<WindowItem> FilteredWindows { get; }
        WindowItem? SelectedWindow { get; }

        void MoveSelection(int direction);
        void MoveSelectionToFirst();
        void MoveSelectionToLast();
        void MoveSelectionByPage(int direction, int pageSize);
    }
}
