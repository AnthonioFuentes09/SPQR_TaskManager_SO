using System.Windows;
using System.Windows.Controls;
using SPQR.Core.Dominio;
using SPQR.UI.ViewModels;

namespace SPQR.UI.Vistas;

public partial class VistaProgramas : UserControl
{
    public VistaProgramas() => InitializeComponent();

    private PrincipalViewModel? Vm => DataContext as PrincipalViewModel;

    private void Agregar_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var nuevo = new Programa
        {
            Id = $"P{Vm.Programas.Count + 1}",
            Nombre = "Programa nuevo",
            TamanioEnPaginas = 4,
        };
        nuevo.CadenaTexto = "0, 1, 2, 1";
        Vm.Programas.Add(nuevo);
        Sincronizar();
    }

    private void Duplicar_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null || Tabla.SelectedItem is not Programa original) return;
        var copia = new Programa
        {
            Id = $"P{Vm.Programas.Count + 1}",
            Nombre = original.Nombre + " (copia)",
            TamanioEnPaginas = original.TamanioEnPaginas,
            Prioridad = original.Prioridad,
            Boletos = original.Boletos,
            PorcentajeAsignacion = original.PorcentajeAsignacion,
        };
        copia.CadenaTexto = original.CadenaTexto;
        Vm.Programas.Add(copia);
        Sincronizar();
    }

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null || Tabla.SelectedItem is not Programa seleccionado) return;

        foreach (var p in Vm.Procesos)
        {
            if (!ReferenceEquals(p.Programa, seleccionado)) continue;
            MessageBox.Show(
                "No se puede quitar: el programa todavía tiene instancias en la lista de ejecución.",
                "SPQR Task Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Vm.Programas.Remove(seleccionado);
        Sincronizar();
    }

    /// <summary>Vuelca la colección de la pantalla a la lista enlazada del núcleo.</summary>
    private void Sincronizar()
    {
        if (Vm is null) return;
        Vm.Escenario.Catalogo.Limpiar();
        foreach (var p in Vm.Programas) Vm.Escenario.Catalogo.AgregarAlFinal(p);
    }
}
