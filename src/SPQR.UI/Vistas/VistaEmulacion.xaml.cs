using System.Windows.Controls;
using SPQR.UI.ViewModels;

namespace SPQR.UI.Vistas;

public partial class VistaEmulacion : UserControl
{
    public VistaEmulacion() => InitializeComponent();

    private void Velocidad_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not PrincipalViewModel vm || sender is not ComboBox cb) return;
        vm.Velocidad = cb.SelectedIndex switch { 0 => 0.5, 1 => 1, 2 => 2, _ => 4 };
    }
}
