using System.Configuration;
using System.Data;
using System.Windows;

namespace ModMover.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var window = new MainWindow(new MainWindowViewModel());
            window.Show();
            ((MainWindowViewModel)window.DataContext).Initialise();
        }
    }

}
