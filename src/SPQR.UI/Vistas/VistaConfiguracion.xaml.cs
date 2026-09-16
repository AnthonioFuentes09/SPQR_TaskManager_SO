using System.Windows;
using System.Windows.Controls;
using SPQR.UI.ViewModels;

namespace SPQR.UI.Vistas;

public partial class VistaConfiguracion : UserControl
{
    public VistaConfiguracion()
    {
        InitializeComponent();
        Loaded += (_, _) => MarcarSeleccionado();
    }

    private PrincipalViewModel? Vm => DataContext as PrincipalViewModel;

    private void Algoritmo_Checked(object sender, RoutedEventArgs e)
    {
        if (Vm is null || sender is not RadioButton rb || rb.Content is not string clave) return;
        Vm.Config.AlgoritmoPaginacion = clave;
        Vm.Mensaje = $"Algoritmo de la MMU: {clave}. Presioná «Simular» para correr el escenario con esta elección.";
    }

    /// <summary>Deja marcado el algoritmo que ya trae la configuración.</summary>
    private void MarcarSeleccionado()
    {
        if (Vm is null) return;
        foreach (var rb in Buscar<RadioButton>(this))
            if (rb.Content is string clave && clave == Vm.Config.AlgoritmoPaginacion)
                rb.IsChecked = true;
    }

    private static IEnumerable<T> Buscar<T>(DependencyObject raiz) where T : DependencyObject
    {
        var n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(raiz);
        for (var i = 0; i < n; i++)
        {
            var hijo = System.Windows.Media.VisualTreeHelper.GetChild(raiz, i);
            if (hijo is T encontrado) yield return encontrado;
            foreach (var nieto in Buscar<T>(hijo)) yield return nieto;
        }
    }
}
