using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;

namespace VintageStoryModManager.Views
{

    /// <summary>
    /// A multi-select dropdown control for WPF.
    /// </summary>
    public partial class MultiSelectDropdown : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(MultiSelectDropdown),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register(
                nameof(SelectedItems),
                typeof(IList),
                typeof(MultiSelectDropdown),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedItemsChanged));

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(
                nameof(PlaceholderText),
                typeof(string),
                typeof(MultiSelectDropdown),
                new PropertyMetadata("Select items...", OnPlaceholderTextChanged));

        public static readonly DependencyProperty DisplayMemberPathProperty =
            DependencyProperty.Register(
                nameof(DisplayMemberPath),
                typeof(string),
                typeof(MultiSelectDropdown),
                new PropertyMetadata("Name"));

        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.Register(
                nameof(DisplayText),
                typeof(string),
                typeof(MultiSelectDropdown),
                new PropertyMetadata("Select items..."));

        public static readonly DependencyProperty DisplayTextBrushProperty =
            DependencyProperty.Register(
                nameof(DisplayTextBrush),
                typeof(Brush),
                typeof(MultiSelectDropdown),
                new PropertyMetadata(new SolidColorBrush(System.Windows.Media.Color.FromRgb(161, 161, 170)))); // TextSecondaryBrush

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public IList SelectedItems
        {
            get => (IList)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public string DisplayMemberPath
        {
            get => (string)GetValue(DisplayMemberPathProperty);
            set => SetValue(DisplayMemberPathProperty, value);
        }

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            private set => SetValue(DisplayTextProperty, value);
        }

        public Brush DisplayTextBrush
        {
            get => (Brush)GetValue(DisplayTextBrushProperty);
            private set => SetValue(DisplayTextBrushProperty, value);
        }

        private ObservableCollection<SelectableItem> _selectableItems = [];
        private Brush? _textPrimaryBrush;
        private Brush? _textSecondaryBrush;

        // Fallback brushes if resources aren't available
        private static readonly Brush FallbackTextPrimaryBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 250, 250));
        private static readonly Brush FallbackTextSecondaryBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(161, 161, 170));

        private Brush TextPrimaryBrush => _textPrimaryBrush ??= TryFindResource("TextPrimaryBrush") as Brush ?? FallbackTextPrimaryBrush;
        private Brush TextSecondaryBrush => _textSecondaryBrush ??= TryFindResource("TextSecondaryBrush") as Brush ?? FallbackTextSecondaryBrush;

        public MultiSelectDropdown()
        {
            InitializeComponent();
            ItemsList.ItemsSource = _selectableItems;
            Loaded += (_, _) => UpdateDisplayText(); // Update after resources are available
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MultiSelectDropdown dropdown)
            {
                System.Diagnostics.Debug.WriteLine($"MultiSelectDropdown.OnItemsSourceChanged: OldValue={e.OldValue?.GetType().Name}, NewValue={e.NewValue?.GetType().Name}");
                
                // Unsubscribe from old collection
                if (e.OldValue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= dropdown.OnItemsSourceCollectionChanged;
                    System.Diagnostics.Debug.WriteLine("MultiSelectDropdown.OnItemsSourceChanged: Unsubscribed from old collection");
                }

                // Subscribe to new collection
                if (e.NewValue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += dropdown.OnItemsSourceCollectionChanged;
                    System.Diagnostics.Debug.WriteLine($"MultiSelectDropdown.OnItemsSourceChanged: Subscribed to new collection");
                }

                dropdown.UpdateSelectableItems();
            }
        }

        private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"MultiSelectDropdown.OnItemsSourceCollectionChanged: Action={e.Action}");
            UpdateSelectableItems();
        }

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MultiSelectDropdown dropdown)
            {
                // Unsubscribe from old collection
                if (e.OldValue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= dropdown.OnSelectedItemsCollectionChanged;
                }

                // Subscribe to new collection
                if (e.NewValue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += dropdown.OnSelectedItemsCollectionChanged;
                }

                dropdown.UpdateDisplayText();
                dropdown.UpdateSelectableItems();
            }
        }

        private void OnSelectedItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateDisplayText();
            UpdateSelectableItems();
        }

        private static void OnPlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MultiSelectDropdown dropdown)
            {
                dropdown.UpdateDisplayText();
            }
        }

        private void UpdateDisplayText()
        {
            if (SelectedItems == null || SelectedItems.Count == 0)
            {
                DisplayText = PlaceholderText;
                DisplayTextBrush = TextSecondaryBrush;
            }
            else if (SelectedItems.Count == 1)
            {
                DisplayText = GetDisplayText(SelectedItems[0]!);
                DisplayTextBrush = TextPrimaryBrush;
            }
            else
            {
                DisplayText = $"{SelectedItems.Count} selected";
                DisplayTextBrush = TextPrimaryBrush;
            }
        }

        private void UpdateSelectableItems()
        {
            System.Diagnostics.Debug.WriteLine($"MultiSelectDropdown.UpdateSelectableItems: Clearing {_selectableItems.Count} items");
            _selectableItems.Clear();

            if (ItemsSource == null)
            {
                System.Diagnostics.Debug.WriteLine("MultiSelectDropdown.UpdateSelectableItems: ItemsSource is null, returning");
                return;
            }

            var count = 0;
            foreach (var item in ItemsSource)
            {
                var displayText = GetDisplayText(item);
                var isSelected = SelectedItems?.Contains(item) == true;

                _selectableItems.Add(new SelectableItem
                {
                    Item = item,
                    DisplayText = displayText,
                    IsSelected = isSelected
                });
                count++;
            }
            System.Diagnostics.Debug.WriteLine($"MultiSelectDropdown.UpdateSelectableItems: Added {count} items to _selectableItems");
        }

        private string GetDisplayText(object item)
        {
            if (string.IsNullOrEmpty(DisplayMemberPath))
                return item.ToString() ?? string.Empty;

            var property = item.GetType().GetProperty(DisplayMemberPath);
            return property?.GetValue(item)?.ToString() ?? item.ToString() ?? string.Empty;
        }

        private void DropdownToggle_Click(object sender, RoutedEventArgs e)
        {
            // Refresh selection state when opening
            if (DropdownToggle.IsChecked == true)
            {
                foreach (var selectableItem in _selectableItems)
                {
                    selectableItem.IsSelected = SelectedItems?.Contains(selectableItem.Item) == true;
                }
            }
        }

        private void Item_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SelectableItem selectableItem)
            {
                selectableItem.IsSelected = !selectableItem.IsSelected;

                if (SelectedItems != null)
                {
                    if (selectableItem.IsSelected)
                    {
                        if (!SelectedItems.Contains(selectableItem.Item))
                        {
                            SelectedItems.Add(selectableItem.Item);
                        }
                    }
                    else
                    {
                        SelectedItems.Remove(selectableItem.Item);
                    }
                }

                UpdateDisplayText();
            }
        }
    }

    /// <summary>
    /// Wrapper for items in the multi-select dropdown.
    /// </summary>
    public class SelectableItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;

        public object Item { get; set; } = null!;
        public string DisplayText { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}