using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
                typeof(System.Drawing.Brush),
                typeof(MultiSelectDropdown),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(161, 161, 170)))); // TextSecondaryBrush

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

        public System.Drawing.Brush DisplayTextBrush
        {
            get => (System.Drawing.Brush)GetValue(DisplayTextBrushProperty);
            private set => SetValue(DisplayTextBrushProperty, value);
        }

        private ObservableCollection<SelectableItem> _selectableItems = [];
        private System.Drawing.Brush? _textPrimaryBrush;
        private System.Drawing.Brush? _textSecondaryBrush;

        // Fallback brushes if resources aren't available
        private static readonly System.Drawing.Brush FallbackTextPrimaryBrush = new SolidColorBrush(Color.FromRgb(250, 250, 250));
        private static readonly System.Drawing.Brush FallbackTextSecondaryBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));

        private System.Drawing.Brush TextPrimaryBrush => _textPrimaryBrush ??= TryFindResource("TextPrimaryBrush") as System.Drawing.Brush ?? FallbackTextPrimaryBrush;
        private System.Drawing.Brush TextSecondaryBrush => _textSecondaryBrush ??= TryFindResource("TextSecondaryBrush") as System.Drawing.Brush ?? FallbackTextSecondaryBrush;

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
                // Unsubscribe from old collection
                if (e.OldValue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= dropdown.OnItemsSourceCollectionChanged;
                }

                // Subscribe to new collection
                if (e.NewValue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += dropdown.OnItemsSourceCollectionChanged;
                }

                dropdown.UpdateSelectableItems();
            }
        }

        private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
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
            _selectableItems.Clear();

            if (ItemsSource == null)
                return;

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
            }
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