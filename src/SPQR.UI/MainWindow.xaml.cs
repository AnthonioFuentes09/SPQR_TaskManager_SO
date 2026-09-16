using System.ComponentModel;
using System.Windows;
using SPQR.UI.ViewModels;

namespace SPQR.UI;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public PrincipalViewModel Vm { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = Vm;

        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PrincipalViewModel.Pantalla))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModuloActual)));
        };
    }

    /// <summary>
    /// Qué módulo del equipo es dueño de la pantalla que se está viendo.
    /// Es el mismo reparto que documenta docs/MODULOS.md.
    /// </summary>
    public string ModuloActual => Vm.Pantalla switch
    {
        0 or 1 => "Módulo 1 · Procesos y planificación",
        2      => "Módulo 2 · Memoria y paginación",
        3 or 4 => "Módulo 3 · Emulación y visualización",
        _      => "Transversal · los tres módulos",
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Salir_Click(object sender, RoutedEventArgs e) => Close();
}
