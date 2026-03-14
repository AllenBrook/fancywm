using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

using FancyWM.ViewModels;

namespace FancyWM.Controls
{
    /// <summary>
    /// Interaction logic for TabBar.xaml
    /// </summary>
    public partial class TabBar : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<TilingNodeViewModel>),
            typeof(TabBar));

        public ObservableCollection<TilingNodeViewModel> ItemsSource
        {
            get => (ObservableCollection<TilingNodeViewModel>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public TabBar()
        {
            InitializeComponent();
        }
    }
}
