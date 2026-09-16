using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using SPQR.UI.ViewModels;

namespace SPQR.UI.Vistas;

public partial class VistaInforme : UserControl
{
    public VistaInforme() => InitializeComponent();

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
