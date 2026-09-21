using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SPQR.Core.Dominio;

namespace SPQR.UI.Vistas;

/// <summary>
/// Diálogo para agregar una instancia a la lista de ejecución.
///
/// Antes, el botón «instanciar» tomaba siempre el primer programa del
/// catálogo y le ponía valores fijos, con lo cual instanciar el quinto
/// programa era imposible desde la interfaz. Acá se elige qué programa y con
/// qué valores, que es lo que el botón decía hacer.
///
/// La ráfaga arranca igualada al largo de la cadena de referencias del
/// programa elegido. No es un detalle cosmético: cada tick de CPU dispara
/// exactamente un acceso a memoria, así que para resolver un ejercicio de
/// clase la ráfaga tiene que ser el largo de la cadena; si es más corta, las
/// últimas referencias no se simulan. Si el usuario escribe un valor propio,
/// el diálogo lo respeta y deja de sugerir.
/// </summary>
public partial class DialogoInstanciar : Window
{
    private bool _rafagaEditadaAMano;
    private bool _escribiendoYo;

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

        SugerirRafaga();
    }

    private void Programa_Cambio(object sender, SelectionChangedEventArgs e) => SugerirRafaga();

    private void Rafaga_Cambio(object sender, TextChangedEventArgs e)
    {
        // Durante InitializeComponent el Text="6" del XAML dispara este evento;
        // eso no es el usuario escribiendo, así que no cuenta como edición.
        if (PistaCadena is null) return;

        if (!_escribiendoYo) _rafagaEditadaAMano = true;
        DescribirCadena();
    }

    /// <summary>Iguala la ráfaga al largo de la cadena, salvo que el usuario ya la haya tocado.</summary>
    private void SugerirRafaga()
    {
        if (PistaCadena is null) return;   // todavía no terminó InitializeComponent

        if (CampoPrograma.SelectedItem is Programa programa && !_rafagaEditadaAMano)
        {
            var largo = programa.CadenaDeReferencias.Cantidad;
            if (largo > 0)
            {
                _escribiendoYo = true;
                CampoRafaga.Text = largo.ToString();
                _escribiendoYo = false;
            }
        }

        DescribirCadena();
    }

    /// <summary>Dice en una línea qué va a pasar con la cadena para la ráfaga escrita.</summary>
    private void DescribirCadena()
    {
        if (PistaCadena is null) return;

        if (CampoPrograma.SelectedItem is not Programa programa)
        {
            PistaCadena.Text = "";
            return;
        }

        var largo = programa.CadenaDeReferencias.Cantidad;
        if (largo == 0)
        {
            PistaCadena.Text = $"«{programa.Nombre}» no tiene cadena: va a recorrer sus " +
                               $"{programa.TamanioEnPaginas} páginas en orden, en ciclo.";
            return;
        }

        if (!int.TryParse(CampoRafaga.Text.Trim(), out var rafaga) || rafaga < 1)
        {
            PistaCadena.Text = $"La cadena de «{programa.Nombre}» tiene {largo} referencias.";
            return;
        }

        PistaCadena.Text = rafaga == largo
            ? $"La ráfaga coincide con las {largo} referencias de la cadena: se simulan todas, una vez."
            : rafaga < largo
                ? $"Ojo: la cadena tiene {largo} referencias y la ráfaga es {rafaga}. Las últimas " +
                  $"{largo - rafaga} no se van a simular. Para un ejercicio, poné {largo}."
                : $"La ráfaga ({rafaga}) es más larga que la cadena ({largo}): al terminarla vuelve a " +
                  "empezar desde la primera referencia.";
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
