using System.Windows;

namespace CoherenceOne.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(CoherenceOne.UI.Views.HomeView homeView)
    {
        InitializeComponent();
        Content = homeView;
    }
}
