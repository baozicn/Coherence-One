using System.Windows;
using System.Windows.Controls;

namespace CoherenceOne.UI.Controls;

public partial class PomodoroRing : UserControl
{
    public PomodoroRing()
    {
        InitializeComponent();
    }

    public string DisplayText
    {
        get => (string)GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(
        nameof(DisplayText), typeof(string), typeof(PomodoroRing), new PropertyMetadata(string.Empty));
}
