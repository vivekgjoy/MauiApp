using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;

namespace MauiApp.ViewModels
{
    public class DisplayItem<T>
    {
        public T Item { get; }
        public string DisplayText { get; }
        public string? Description { get; }
        public bool IsSelected { get; }

        public DisplayItem(T item, string displayText, string? description = null, bool isSelected = false)
        {
            Item = item;
            DisplayText = displayText;
            Description = description;
            IsSelected = isSelected;
        }
    }
    public class BottomSheetSelectionViewModel<T> : BaseViewModel
    {
        private readonly IEnumerable<T> _items;
        private readonly Func<T, string> _displaySelector;
        private string _searchText = string.Empty;
        private T? _selectedItem;

        public IEnumerable<T> Items { get; }
        public string Title { get; }
        public T? SelectedItem => _selectedItem;

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                System.Diagnostics.Debug.WriteLine($"SearchText changed to: '{value}'");
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredItems));
            }
        }

        public IEnumerable<DisplayItem<T>> FilteredItems => string.IsNullOrWhiteSpace(SearchText)
            ? _items.Select(item => new DisplayItem<T>(item, _displaySelector(item), GetDescription(item), IsSelected(item)))
            : _items.Where(item => _displaySelector(item).Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    .Select(item => new DisplayItem<T>(item, _displaySelector(item), GetDescription(item), IsSelected(item)));

        public ICommand DismissCommand { get; }
        public ICommand SelectCommand { get; }

        public BottomSheetSelectionViewModel(IEnumerable<T> items, Func<T, string> displaySelector, string title)
        {
            _items = items;
            _displaySelector = displaySelector;
            Title = title;
            Items = items;

            DismissCommand = new Command(async () => await DismissAsync());
            SelectCommand = new Command<T>(async (item) => await SelectAsync(item));
        }

        private async Task DismissAsync()
        {
            await Application.Current.MainPage.Navigation.PopModalAsync();
        }

        private async Task SelectAsync(T item)
        {
            _selectedItem = item;
            OnPropertyChanged(nameof(SelectedItem));
            await Application.Current.MainPage.Navigation.PopModalAsync();
        }

        protected virtual string? GetDescription(T item)
        {
            // Override this method in derived classes to provide descriptions
            return null;
        }

        protected virtual bool IsSelected(T item)
        {
            // Override this method in derived classes to determine selection
            return false;
        }
    }
}
