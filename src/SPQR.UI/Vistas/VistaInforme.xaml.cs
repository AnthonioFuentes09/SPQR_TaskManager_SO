using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using SPQR.Core.Persistencia;
using SPQR.UI.ViewModels;

namespace SPQR.UI.Vistas;

public partial class VistaInforme : UserControl
{
    public VistaInforme() => InitializeComponent();

    /// <summary>Muestra la bitácora que quedó guardada de una corrida vieja.</summary>
    private void Historial_DobleClic(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is not PrincipalViewModel vm) return;
        if (sender is not DataGrid grilla || grilla.SelectedItem is not ResumenDeCorrida corrida) return;

        var bitacora = vm.BitacoraDe(corrida.Id);
        if (bitacora.Count == 0)
        {
            MessageBox.Show("Esa corrida no tiene bitácora guardada.", "SPQR Task Manager",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var texto = new StringBuilder();
        texto.AppendLine($"Corrida #{corrida.Id} · {corrida.Fecha}");
        texto.AppendLine($"{corrida.AlgoritmoPlanificacion} + {corrida.AlgoritmoPaginacion} · " +
                         $"rendimiento {corrida.RendimientoPorcentual} %");
        texto.AppendLine();

        // Las primeras líneas alcanzan para reconocerla; el CSV lleva todo.
        foreach (var (instante, proceso, detalle) in bitacora.Take(40))
            texto.AppendLine($"t={instante,-4} {proceso,-18} {detalle}");

        if (bitacora.Count > 40)
            texto.AppendLine($"… y {bitacora.Count - 40} eventos más.");

        MessageBox.Show(texto.ToString(), $"Bitácora de la corrida #{corrida.Id}",
            MessageBoxButton.OK, MessageBoxImage.None);
    }

    private void ExportarLog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PrincipalViewModel vm || vm.Log.Count == 0)
        {
            MessageBox.Show("Todavía no hay una corrida para exportar.", "SPQR Task Manager",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ruta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            $"spqr_log_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("instante;proceso;detalle");
        foreach (var fila in vm.Log)
            sb.AppendLine($"{fila.Instante};{fila.Proceso};{fila.Detalle.Replace(';', ',')}");

        File.WriteAllText(ruta, sb.ToString(), Encoding.UTF8);
        vm.Mensaje = $"Log exportado a {ruta}";
    }
}
