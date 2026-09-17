using System.Collections.ObjectModel;
using System.Windows;
using SPQR.Core.Dominio;

namespace SPQR.UI.Vistas;

/// <summary>
/// Diálogo para agregar una instancia a la lista de ejecución.
///
/// Antes, el botón «instanciar» tomaba siempre el primer programa del
/// catálogo y le ponía valores fijos, con lo cual instanciar el quinto
/// programa era imposible desde la interfaz. Acá se elige qué programa y con
/// qué valores, que es lo que el botón decía hacer.
/// </summary>
public partial class DialogoInstanciar : Window
{
    public Programa? Programa { get; private set; }
    public int Rafaga { get; private set; }
    public int Llegada { get; private set; }
    public int Cola { get; private set; } = 1;

    public DialogoInstanciar(
        ObservableCollection<Programa> catalogo,
        ObservableCollection<ReglaDeCola> colas,
        Programa? preseleccionado)
    {
        InitializeComponent();

        CampoPrograma.ItemsSource = catalogo;
        CampoPrograma.SelectedItem = preseleccionado ?? (catalogo.Count > 0 ? catalogo[0] : null);

        CampoCola.ItemsSource = colas;
        CampoCola.SelectedItem = colas.Count > 0 ? colas[0] : null;
    }

    private void Agregar_Click(object sender, RoutedEventArgs e)
    {
        if (CampoPrograma.SelectedItem is not Programa programa)
        {
            Reclamar("Elegí un programa del catálogo.");
            return;
        }

        if (!int.TryParse(CampoRafaga.Text.Trim(), out var rafaga) || rafaga < 1)
        {
            Reclamar("La ráfaga tiene que ser un número entero de 1 o más: es el tiempo de CPU que el proceso necesita.");
            return;
        }

        if (!int.TryParse(CampoLlegada.Text.Trim(), out var llegada) || llegada < 0)
        {
            Reclamar("El tiempo de llegada tiene que ser un número entero de 0 o más.");
            return;
        }

        Programa = programa;
        Rafaga = rafaga;
        Llegada = llegada;
        Cola = CampoCola.SelectedItem is ReglaDeCola regla ? regla.Numero : 1;

        DialogResult = true;
        Close();
    }

    private void Reclamar(string mensaje)
    {
        Aviso.Text = mensaje;
        Aviso.Visibility = Visibility.Visible;
    }
}
