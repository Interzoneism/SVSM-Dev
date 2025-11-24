// Temporary WPF compatibility stubs for migration
// These will be replaced with proper Avalonia implementations

namespace System.Windows
{
    public enum MessageBoxButton
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel
    }

    public enum MessageBoxImage
    {
        None,
        Error,
        Question,
        Warning,
        Information
    }

    public enum MessageBoxResult
    {
        None,
        OK,
        Cancel,
        Yes,
        No
    }

    public enum WindowStartupLocation
    {
        Manual,
        CenterScreen,
        CenterOwner
    }

    // Stub Window class
    public class Window
    {
        public bool? IsVisible { get; set; }
        public bool IsActive { get; set; }
        public Window? Owner { get; set; }
        public WindowStartupLocation WindowStartupLocation { get; set; }
        public bool Topmost { get; set; }
        
        public bool? ShowDialog() => true;
    }

    // Stub Application class
    public class Application
    {
        public static Application? Current { get; set; }
        public System.Collections.Generic.IEnumerable<Window> Windows { get; set; } = Array.Empty<Window>();
        public Window? MainWindow { get; set; }
        public System.Windows.Threading.Dispatcher Dispatcher { get; } = new System.Windows.Threading.Dispatcher();
        
        public static System.IO.Stream? GetResourceStream(Uri uri) => null;
    }
}

namespace System.Windows.Data
{
    public interface ICollectionView : System.Collections.IEnumerable, System.Collections.Specialized.INotifyCollectionChanged
    {
        System.Collections.IEnumerable SourceCollection { get; }
        System.Predicate<object>? Filter { get; set; }
        System.ComponentModel.SortDescriptionCollection SortDescriptions { get; }
        System.Collections.IEnumerable Groups { get; }
        object? CurrentItem { get; }
        int CurrentPosition { get; }
        bool IsCurrentAfterLast { get; }
        bool IsCurrentBeforeFirst { get; }
        
        bool MoveCurrentToFirst();
        bool MoveCurrentToLast();
        bool MoveCurrentToNext();
        bool MoveCurrentToPrevious();
        bool MoveCurrentTo(object item);
        bool MoveCurrentToPosition(int position);
        void Refresh();
        event EventHandler? CurrentChanged;
        event System.ComponentModel.CurrentChangingEventHandler? CurrentChanging;
    }

    // Stub IValueConverter
    public interface IValueConverter
    {
        object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture);
        object? ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture);
    }

    // Stub IMultiValueConverter
    public interface IMultiValueConverter
    {
        object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture);
        object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture);
    }
}

namespace System.Windows.Media
{
    public abstract class ImageSource
    {
    }

    public class Brush
    {
    }

    public class SolidColorBrush : Brush
    {
    }
}

namespace System.Windows.Media.Imaging
{
    public class BitmapImage : System.Windows.Media.ImageSource
    {
        public BitmapImage() { }
        public BitmapImage(Uri uriSource) { }
        public Uri? UriSource { get; set; }
    }
}

namespace System.Windows.Threading
{
    public enum DispatcherPriority
    {
        Normal = 9,
        Background = 4,
        Send = 10,
        ContextIdle = 3
    }

    public class Dispatcher
    {
        public void Invoke(Action callback, DispatcherPriority priority) => callback();
        public bool CheckAccess() => true;
        public void BeginInvoke(DispatcherPriority priority, Delegate method) => method.DynamicInvoke();
        public System.Threading.Tasks.Task InvokeAsync(Action callback) => System.Threading.Tasks.Task.Run(callback);
    }
}

namespace System.ComponentModel
{
    public delegate void CurrentChangingEventHandler(object sender, CurrentChangingEventArgs e);
    
    public class CurrentChangingEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
        public bool IsCancelable { get; }
    }
    
    public class SortDescriptionCollection : System.Collections.ObjectModel.Collection<SortDescription>
    {
    }
    
    public struct SortDescription
    {
        public string PropertyName { get; set; }
        public System.ComponentModel.ListSortDirection Direction { get; set; }
    }
}

