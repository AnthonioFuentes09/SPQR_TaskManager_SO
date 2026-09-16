using System.Windows;
using System.Windows.Controls;
using SPQR.Core.Dominio;
using SPQR.UI.ViewModels;

namespace SPQR.UI.Vistas;

public partial class VistaListaEjecucion : UserControl
{
    public VistaListaEjecucion() => InitializeComponent();

    private PrincipalViewModel? Vm => DataContext as PrincipalViewModel;

    private void Instanciar_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null || Vm.Programas.Count == 0) return;

        // Se instancia el programa que esté seleccionado en la pantalla 1, o el
        // primero del catálogo. Instanciar es justamente lo que separa un
        // programa (la plantilla) de un proceso (la ejecución concreta).
        var programa = Vm.Programas[0];
        var proceso = new Proceso
        {
            Id = SiguienteId(),
            Programa = programa,
            Rafaga = 6,
            TiempoLlegada = 0,
            Cola = 1,
        };
        proceso.Reiniciar();
        Vm.Procesos.Add(proceso);
    }

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null || Tabla.SelectedItem is not Proceso seleccionado) return;
        Vm.Procesos.Remove(seleccionado);
    }

    private int SiguienteId()
    {
        var max = 0;
        foreach (var p in Vm!.Procesos) if (p.Id > max) max = p.Id;
        return max + 1;
    }
}
