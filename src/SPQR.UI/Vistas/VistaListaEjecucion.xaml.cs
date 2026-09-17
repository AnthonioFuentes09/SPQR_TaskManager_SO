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
        if (Vm is null) return;

        if (Vm.Programas.Count == 0)
        {
            Avisar("No hay programas en el catálogo. Agregá uno en la pantalla «Configuración de programas».");
            return;
        }

        var dialogo = new DialogoInstanciar(Vm.Programas, Vm.Colas, Vm.ProgramaSeleccionado)
        {
            Owner = Window.GetWindow(this),
        };

        if (dialogo.ShowDialog() != true || dialogo.Programa is null) return;

        var proceso = new Proceso
        {
            Id = SiguienteId(),
            Programa = dialogo.Programa,
            Rafaga = dialogo.Rafaga,
            TiempoLlegada = dialogo.Llegada,
            Cola = dialogo.Cola,
        };
        proceso.Reiniciar();
        Vm.Procesos.Add(proceso);
        Tabla.SelectedItem = proceso;
        Vm.Mensaje = $"Agregada la instancia {proceso.Id} de {dialogo.Programa.Nombre}.";
    }

    private void Duplicar_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (Tabla.SelectedItem is not Proceso original)
        {
            Avisar("Seleccioná primero la fila que querés duplicar.");
            return;
        }

        var copia = new Proceso
        {
            Id = SiguienteId(),
            Programa = original.Programa,
            Rafaga = original.Rafaga,
            TiempoLlegada = original.TiempoLlegada,
            Cola = original.Cola,
        };
        copia.Reiniciar();
        Vm.Procesos.Add(copia);
        Tabla.SelectedItem = copia;
    }

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (Tabla.SelectedItem is not Proceso seleccionado)
        {
            Avisar("Seleccioná primero la fila que querés quitar.");
            return;
        }

        Vm.Procesos.Remove(seleccionado);
        Vm.Mensaje = $"Quitada la instancia {seleccionado.Id}.";
    }

    private void AgregarCola_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        var numero = 1;
        foreach (var c in Vm.Colas) if (c.Numero >= numero) numero = c.Numero + 1;

        var regla = new ReglaDeCola { Numero = numero, Etiqueta = $"Cola {numero}", Algoritmo = "Fifo" };
        Vm.Colas.Add(regla);
        TablaColas.SelectedItem = regla;
    }

    private void QuitarCola_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (TablaColas.SelectedItem is not ReglaDeCola regla)
        {
            Avisar("Seleccioná primero la cola que querés quitar.");
            return;
        }

        if (Vm.Colas.Count == 1)
        {
            Avisar("Tiene que quedar al menos una cola: múltiples colas sin colas no puede planificar nada.");
            return;
        }

        // Si algún proceso vive en esa cola, quitarla lo dejaría huérfano.
        var huerfanos = 0;
        foreach (var p in Vm.Procesos) if (p.Cola == regla.Numero) huerfanos++;

        if (huerfanos > 0)
        {
            var respuesta = MessageBox.Show(
                $"Hay {huerfanos} proceso(s) en la cola {regla.Numero}. " +
                "Si la quitás, esos procesos se van a mover a la primera cola que quede. ¿Continuar?",
                "SPQR Task Manager", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (respuesta != MessageBoxResult.Yes) return;
        }

        Vm.Colas.Remove(regla);

        var destino = Vm.Colas[0].Numero;
        foreach (var p in Vm.Procesos) if (p.Cola == regla.Numero) p.Cola = destino;
    }

    private int SiguienteId()
    {
        var max = 0;
        foreach (var p in Vm!.Procesos) if (p.Id > max) max = p.Id;
        return max + 1;
    }

    private void Avisar(string mensaje)
    {
        if (Vm is not null) Vm.Mensaje = mensaje;
        MessageBox.Show(mensaje, "SPQR Task Manager", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
