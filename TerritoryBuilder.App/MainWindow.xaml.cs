using System.Windows;
using TerritoryBuilder.App.ViewModels;

namespace TerritoryBuilder.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
