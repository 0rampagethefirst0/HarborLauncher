using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Harbor;

internal static class Theme
{
    sealed class ColorValue : DependencyObject
    {
        internal static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(Color), typeof(ColorValue));
        public Color Value { get => (Color)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    }
    static readonly Dictionary<SolidColorBrush, ColorValue> Colors = new();
    internal static SolidColorBrush Bg = Mutable("#141820"), Panel = Mutable("#1C212D"), Raised = Mutable("#262D3B"), Line = Mutable("#343D4C"), Text = Mutable("#EDF1FA"), Muted = Mutable("#9AA8BC"), Accent = Mutable("#8AB4FF");
    static SolidColorBrush Mutable(string color)
    {
        var value = new ColorValue { Value = (Color)ColorConverter.ConvertFromString(color) }; var brush = new SolidColorBrush();
        BindingOperations.SetBinding(brush, SolidColorBrush.ColorProperty, new Binding(nameof(ColorValue.Value)) { Source = value }); Colors[brush] = value; return brush;
    }
    internal static void Palette(string background, string panel, string accent, string text, string muted = "#9AA8BC", string raised = "#262D3B", string border = "#343D4C")
    {
        Set(Bg, background, "#141820"); Set(Panel, panel, "#1C212D"); Set(Accent, accent, "#8AB4FF"); Set(Text, text, "#EDF1FA");
        Set(Muted, muted, "#9AA8BC"); Set(Raised, raised, "#262D3B"); Set(Line, border, "#343D4C");
        static void Set(SolidColorBrush brush, string value, string fallback) { try { Colors[brush].Value = (Color)ColorConverter.ConvertFromString(value); } catch { Colors[brush].Value = (Color)ColorConverter.ConvertFromString(fallback); } }
    }
    internal static SolidColorBrush Brush(string color) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); b.Freeze(); return b; }
    internal static void Install()
    {
        foreach (var entry in new[] { ("Bg", Bg), ("Panel", Panel), ("Raised", Raised), ("Text", Text), ("Muted", Muted), ("Accent", Accent), ("Line", Line) }) Application.Current.Resources["Harbor." + entry.Item1] = entry.Item2;
        var resources = (ResourceDictionary)XamlReader.Parse("""
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style TargetType="Window"><Setter Property="FontFamily" Value="Segoe UI"/><Setter Property="FontSize" Value="13"/><Setter Property="Foreground" Value="{DynamicResource Harbor.Text}"/><Setter Property="Background" Value="{DynamicResource Harbor.Bg}"/></Style>
          <Style TargetType="Button">
            <Setter Property="Foreground" Value="{DynamicResource Harbor.Text}"/><Setter Property="Background" Value="Transparent"/><Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding" Value="10,6"/><Setter Property="Cursor" Value="Hand"/><Setter Property="HorizontalContentAlignment" Value="Center"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="surface" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="surface" Property="Background" Value="#303A4C"/></Trigger><Trigger Property="IsPressed" Value="True"><Setter TargetName="surface" Property="Background" Value="#3E4D66"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="surface" Property="BorderBrush" Value="{DynamicResource Harbor.Accent}"/><Setter TargetName="surface" Property="BorderThickness" Value="1"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="TextBox"><Setter Property="Foreground" Value="{DynamicResource Harbor.Text}"/><Setter Property="Background" Value="#212836"/><Setter Property="CaretBrush" Value="{DynamicResource Harbor.Accent}"/><Setter Property="BorderBrush" Value="#3C485E"/><Setter Property="BorderThickness" Value="1"/><Setter Property="Padding" Value="12,10"/><Setter Property="FontSize" Value="15"/></Style>
          <Style TargetType="ListBox"><Setter Property="Background" Value="Transparent"/><Setter Property="Foreground" Value="{DynamicResource Harbor.Text}"/><Setter Property="BorderThickness" Value="0"/><Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled"/><Setter Property="ScrollViewer.CanContentScroll" Value="True"/></Style>
          <Style TargetType="ListBoxItem"><Setter Property="HorizontalContentAlignment" Value="Stretch"/><Setter Property="Padding" Value="0"/><Setter Property="Margin" Value="0,2"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ListBoxItem"><Border x:Name="surface" CornerRadius="8" Background="Transparent" Padding="10,7"><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="surface" Property="Background" Value="#262F40"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter TargetName="surface" Property="Background" Value="#303E57"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
          <Style TargetType="ContextMenu"><Setter Property="Background" Value="#202735"/><Setter Property="Foreground" Value="{DynamicResource Harbor.Text}"/><Setter Property="BorderBrush" Value="#3B4659"/><Setter Property="Padding" Value="6"/></Style>
          <Style TargetType="MenuItem"><Setter Property="Foreground" Value="{DynamicResource Harbor.Text}"/><Setter Property="Padding" Value="12,7"/></Style>
          <Style TargetType="CheckBox"><Setter Property="Foreground" Value="{DynamicResource Harbor.Text}"/><Setter Property="FontSize" Value="14"/><Setter Property="Margin" Value="0,8"/><Setter Property="Cursor" Value="Hand"/></Style>
          <Style TargetType="ScrollBar"><Setter Property="Width" Value="8"/><Setter Property="Background" Value="{DynamicResource Harbor.Panel}"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Grid Background="{TemplateBinding Background}"><Track x:Name="PART_Track" IsDirectionReversed="True" Orientation="Vertical"><Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border CornerRadius="4" Background="#46546C" Margin="1,0"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
          <Style TargetType="TreeViewItem"><Setter Property="Foreground" Value="{DynamicResource Harbor.Text}"/><Setter Property="Background" Value="Transparent"/><Setter Property="HorizontalContentAlignment" Value="Stretch"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TreeViewItem"><StackPanel><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="16"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><ToggleButton x:Name="Expander" IsChecked="{Binding IsExpanded,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}" Focusable="False"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="Transparent"><TextBlock x:Name="Arrow" Text="&#xE76C;" FontFamily="Segoe MDL2 Assets" FontSize="8" Foreground="{DynamicResource Harbor.Muted}" VerticalAlignment="Center" HorizontalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Arrow" Property="Text" Value="&#xE70D;"/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template></ToggleButton><Border x:Name="Selection" Grid.Column="1" Background="{TemplateBinding Background}" CornerRadius="5" Padding="5,6"><ContentPresenter ContentSource="Header" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"/></Border></Grid><ItemsPresenter x:Name="Children" Margin="14,0,0,0"/></StackPanel><ControlTemplate.Triggers><Trigger Property="IsExpanded" Value="False"><Setter TargetName="Children" Property="Visibility" Value="Collapsed"/></Trigger><Trigger Property="HasItems" Value="False"><Setter TargetName="Expander" Property="Visibility" Value="Hidden"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter TargetName="Selection" Property="Background" Value="{DynamicResource Harbor.Raised}"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
          <Style TargetType="GridViewColumnHeader"><Setter Property="Background" Value="{DynamicResource Harbor.Panel}"/><Setter Property="Foreground" Value="{DynamicResource Harbor.Muted}"/><Setter Property="BorderThickness" Value="0"/><Setter Property="Padding" Value="12,10"/></Style>
          <Style TargetType="ComboBoxItem"><Setter Property="Foreground" Value="{DynamicResource Harbor.Text}"/><Setter Property="Background" Value="{DynamicResource Harbor.Panel}"/><Setter Property="Padding" Value="8,6"/></Style>
          <Style TargetType="ComboBox"><Setter Property="Foreground" Value="{DynamicResource Harbor.Text}"/><Setter Property="Background" Value="{DynamicResource Harbor.Raised}"/><Setter Property="MinHeight" Value="34"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid><ToggleButton Focusable="False" Foreground="{TemplateBinding Foreground}" Background="{TemplateBinding Background}" IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="{TemplateBinding Background}" CornerRadius="6" Padding="10,6"><DockPanel><TextBlock Text="&#xE70D;" FontFamily="Segoe MDL2 Assets" DockPanel.Dock="Right" Foreground="{DynamicResource Harbor.Text}"/><ContentPresenter/></DockPanel></Border></ControlTemplate></ToggleButton.Template><ContentPresenter Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" HorizontalAlignment="Left" Margin="0,0,18,0"/></ToggleButton><Popup Name="PART_Popup" Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}" AllowsTransparency="True" Focusable="False"><Border Background="{DynamicResource Harbor.Panel}" BorderBrush="{DynamicResource Harbor.Line}" BorderThickness="1" MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"><ScrollViewer MaxHeight="300"><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter></Style>
          <Style TargetType="ToolTip"><Setter Property="Background" Value="#222A38"/><Setter Property="Foreground" Value="{DynamicResource Harbor.Text}"/><Setter Property="BorderBrush" Value="#3B4659"/><Setter Property="Padding" Value="10,7"/></Style>
        </ResourceDictionary>
        """);
        Application.Current.Resources.MergedDictionaries.Add(resources);
        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Harbor;component/Controls.xaml", UriKind.Absolute) });
    }
    internal static TextBlock Label(string text, double size = 13, Brush? color = null) => new() { Text = text, FontSize = size, Foreground = color ?? Text, VerticalAlignment = VerticalAlignment.Center };
    internal static TextBlock Glyph(string glyph, double size = 16) => new() { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = size, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    internal static Button Button(object content, string name, Action click)
    {
        var b = new Button { Content = content, ToolTip = name };
        AutomationProperties.SetName(b, name);
        b.Click += (_, _) => click();
        Motion.Hover(b);
        return b;
    }
    internal static Button IconButton(string glyph, string name, Action click) => Button(Glyph(glyph), name, click);
    internal static Grid Placeholder(TextBox box, string hint)
    {
        var grid = new Grid(); grid.Children.Add(box);
        var text = Label(hint, 13, Muted); text.Margin = new Thickness(14, 0, 14, box.Margin.Bottom); text.IsHitTestVisible = false; grid.Children.Add(text);
        box.TextChanged += (_, _) => text.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        text.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; return grid;
    }
    internal static Border Card(UIElement content, int radius = 14) => new() { Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(radius), Child = content };
    internal static UIElement Logo(double size = 22)
    {
        var g = new Grid { Width = size, Height = size };
        g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition());
        g.RowDefinitions.Add(new RowDefinition()); g.RowDefinitions.Add(new RowDefinition());
        for (int i = 0; i < 4; i++)
        {
            var b = new Border { CornerRadius = new CornerRadius(2), Background = i == 3 ? Brush("#EBF2FF") : Accent, Margin = new Thickness(1.4) };
            Grid.SetRow(b, i / 2); Grid.SetColumn(b, i % 2); g.Children.Add(b);
        }
        return g;
    }
    internal static FrameworkElement AppIcon(string path, string name, int size)
    {
        var grid = new Grid { Width = size, Height = size };
        var fallback = new Border { Background = Raised, CornerRadius = new CornerRadius(7), Child = new TextBlock { Text = name.Length > 0 ? name[..1].ToUpperInvariant() : "•", FontSize = size * .54, Foreground = Accent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        grid.Children.Add(fallback);
        bool started = false;
        if (Icons.Peek(path) is ImageSource cached)
        {
            started = true; fallback.Visibility = Visibility.Hidden;
            grid.Children.Add(new Image { Source = cached, Width = size, Height = size, Stretch = Stretch.Uniform });
        }
        grid.Loaded += (_, _) => { if (!started) { started = true; _ = Load(); } };
        async System.Threading.Tasks.Task Load()
        {
            var source = await Icons.Get(path);
            if (source == null || Application.Current.Dispatcher.HasShutdownStarted) return;
            fallback.Visibility = Visibility.Hidden;
            grid.Children.Add(new Image { Source = source, Width = size, Height = size, Stretch = Stretch.Uniform });
        }
        return grid;
    }
    internal static MenuItem Item(string name, Action click)
    {
        var item = new MenuItem { Header = name, Icon = UiIcons.Icon(UiIcons.Kind(name)) };
        item.Click += (_, _) => click();
        return item;
    }
    internal static void AppWindow(Window window, int width, int height)
    {
        window.Width = width; window.Height = height; window.MinWidth = 620; window.MinHeight = 430;
        window.WindowStyle = WindowStyle.None; window.ResizeMode = ResizeMode.CanResize; window.Background = Bg; window.Foreground = Text;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        System.Windows.Shell.WindowChrome.SetWindowChrome(window, new System.Windows.Shell.WindowChrome { CaptionHeight = 0, GlassFrameThickness = new Thickness(0), ResizeBorderThickness = new Thickness(6), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false });
        window.SourceInitialized += (_, _) => { var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle); if (source?.CompositionTarget is HwndTarget target) target.RenderMode = RenderMode.SoftwareOnly; };
    }
    internal static DockPanel Header(Window window, string title)
    {
        var header = new DockPanel { Height = 48, Background = Panel, LastChildFill = true };
        var controls = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(controls, System.Windows.Controls.Dock.Right);
        controls.Children.Add(IconButton("\uE921", "Minimize", () => window.WindowState = WindowState.Minimized));
        controls.Children.Add(IconButton("\uE922", "Maximize or restore", () => window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized));
        controls.Children.Add(IconButton("\uE8BB", "Close " + title, window.Close)); header.Children.Add(controls);
        var label = Label(title, 14); label.Margin = new Thickness(16, 0, 0, 0); header.Children.Add(label);
        label.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; else try { window.DragMove(); } catch { } };
        return header;
    }
    internal static void CloseOnDeactivate(Window window)
    {
        bool closing = false;
        window.Closing += (_, _) => closing = true;
        window.Deactivated += (_, _) => { if (!closing) window.Close(); };
    }
    internal static void PopupWindow(Window w, int width, int height, IntPtr monitor, bool atBottom = true)
    {
        var info = Native.Monitor(monitor);
        double scale = Native.Scale(monitor);
        w.Width = Math.Min(width, info.Work.Width / scale - 24);
        w.Height = Math.Min(height, info.Work.Height / scale - 24);
        w.WindowStyle = WindowStyle.None; w.ResizeMode = ResizeMode.NoResize; w.ShowInTaskbar = false;
        w.Foreground = Text; w.FontFamily = new FontFamily("Segoe UI"); w.FontSize = 13;
        w.Topmost = true; w.AllowsTransparency = true; w.Background = Brushes.Transparent;
        w.Left = info.Work.Left / scale + (info.Work.Width / scale - w.Width) / 2;
        w.Top = atBottom ? info.Work.Bottom / scale - w.Height - 12 : info.Work.Top / scale + (info.Work.Height / scale - w.Height) / 2;
        w.SourceInitialized += (_, _) =>
        {
            Native.ToolWindow(w);
            // Initial WPF coordinates use system DPI. Place in physical monitor coordinates.
            var h = new WindowInteropHelper(w).Handle;
            int pxWidth = (int)Math.Round(w.Width * scale), pxHeight = (int)Math.Round(w.Height * scale);
            int x = info.Work.Left + (info.Work.Width - pxWidth) / 2;
            int y = atBottom ? info.Work.Bottom - pxHeight - (int)(12 * scale) : info.Work.Top + (info.Work.Height - pxHeight) / 2;
            Native.SetWindowPos(h, new IntPtr(-1), x, y, pxWidth, pxHeight, 0x40);
        };
        w.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { w.Close(); e.Handled = true; } };
    }
}
