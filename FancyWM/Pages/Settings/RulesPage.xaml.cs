using System.Windows.Controls;

using FancyWM.Resources;
using FancyWM.ViewModels;

namespace FancyWM.Pages.Settings
{
    /// <summary>
    /// Interaction logic for ExclusionsPage.xaml
    /// </summary>
    public partial class RulesPage : UserControl
    {
        public RulesPage(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            ProcessInstanceIncludeListCaption.Text = GetString("Rules.ProcessInstanceIncludeList");
            ProcessInstanceIncludeListDescription.Text = GetString("Rules.ProcessInstanceIncludeList.Description");
        }

        private static string GetString(string name)
            => Strings.ResourceManager.GetString(name, Strings.Culture) ?? name;
    }
}
