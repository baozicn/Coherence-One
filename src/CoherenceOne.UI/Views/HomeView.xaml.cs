using System.Windows.Controls;
using CoherenceOne.UI.ViewModels;

namespace CoherenceOne.UI.Views;

public partial class HomeView : UserControl
{
    public HomeView(HomeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
