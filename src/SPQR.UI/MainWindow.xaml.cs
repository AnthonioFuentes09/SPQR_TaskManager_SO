using System.ComponentModel;
using System.Text;
using System.Windows;
using SPQR.Core.Dominio;
using SPQR.Persistence;
using SPQR.UI.ViewModels;

namespace SPQR.UI;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    /// <summary>
    /// El único lugar del programa donde se elige QUIÉN guarda: acá se decide
    /// que es SQLite. El resto de la interfaz solo conoce la interfaz IAlmacen.
    /// </summary>
    public PrincipalViewModel Vm { get; } = new(new SqliteEscenarioStore());

    public MainWindow()
    {
        InitializeComponent();
        DataContext = Vm;

        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PrincipalViewModel.Pantalla))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModuloActual)));
        };

        // El núcleo revisa el escenario y devuelve los problemas; mostrarlos es
        // trabajo de la ventana. Antes, un dato inválido llegaba hasta el motor
        // y volvía como el texto crudo de una excepción en la barra de estado.
        Vm.AvisarSiHayProblemas = MostrarProblemas;
    }

    private void MostrarProblemas(List<Problema> problemas)
    {
        if (problemas.Count == 0) return;

        var hayErrores = Validador.HayErrores(problemas);
        var texto = new StringBuilder();

        texto.AppendLine(hayErrores
            ? "El escenario tiene errores y no se puede simular hasta corregirlos:"
            : "El escenario se simuló, pero conviene revisar esto:");
        texto.AppendLine();

        foreach (var p in problemas)
            texto.AppendLine($"• {p.Donde} — {p.Mensaje}");

        MessageBox.Show(texto.ToString(), "Revisión del escenario",
            MessageBoxButton.OK,
            hayErrores ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    /// <summary>
    /// Qué módulo del equipo es dueño de la pantalla que se está viendo.
    /// Es el mismo reparto que documenta docs/MODULOS.md.
    /// </summary>
    public string ModuloActual => Vm.Pantalla switch
    {
        0 or 1 => "Módulo 1 · Procesos y planificación",
        2      => "Módulo 2 · Memoria y paginación",
        _      => "Módulo 3 · Emulación y visualización",
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Salir_Click(object sender, RoutedEventArgs e) => Close();
}
