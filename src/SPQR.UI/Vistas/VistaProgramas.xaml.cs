using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SPQR.Core.Dominio;
using SPQR.Core.Persistencia;
using SPQR.UI.ViewModels;

namespace SPQR.UI.Vistas;

public partial class VistaProgramas : UserControl
{
    public VistaProgramas() => InitializeComponent();

    private PrincipalViewModel? Vm => DataContext as PrincipalViewModel;

    // ---------- catálogo ----------

    private void Agregar_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        var nuevo = new Programa
        {
            Id = SiguienteId(),
            Nombre = "Programa nuevo",
            TamanioEnPaginas = 4,
        };
        nuevo.CadenaTexto = "0, 1, 2w, 1";
        Vm.Programas.Add(nuevo);
        Tabla.SelectedItem = nuevo;
        Vm.Mensaje = $"Agregado el programa {nuevo.Id}.";
    }

    private void Duplicar_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (Tabla.SelectedItem is not Programa original)
        {
            Avisar("Seleccioná primero el programa que querés duplicar.");
            return;
        }

        var copia = new Programa
        {
            Id = SiguienteId(),
            Nombre = original.Nombre + " (copia)",
            TamanioEnPaginas = original.TamanioEnPaginas,
            Prioridad = original.Prioridad,
            Boletos = original.Boletos,
            PorcentajeAsignacion = original.PorcentajeAsignacion,
        };
        copia.CadenaTexto = original.CadenaTexto;
        Vm.Programas.Add(copia);
        Tabla.SelectedItem = copia;
    }

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (Tabla.SelectedItem is not Programa seleccionado)
        {
            Avisar("Seleccioná primero el programa que querés quitar.");
            return;
        }

        var instancias = 0;
        foreach (var p in Vm.Procesos) if (ReferenceEquals(p.Programa, seleccionado)) instancias++;

        if (instancias > 0)
        {
            Avisar($"No se puede quitar «{seleccionado.Nombre}»: tiene {instancias} instancia(s) en la " +
                   "lista de ejecución. Quitalas primero desde la pantalla 2.");
            return;
        }

        if (Vm.Programas.Count == 1)
        {
            Avisar("Tiene que quedar al menos un programa en el catálogo.");
            return;
        }

        Vm.Programas.Remove(seleccionado);
        Vm.Mensaje = $"Quitado el programa {seleccionado.Id}.";
    }

    /// <summary>
    /// El siguiente identificador sale del número MÁS ALTO en uso, no de la
    /// cantidad de programas. Contar produce choques: con P1…P5 y P3 quitado,
    /// el conteo baja a 4 y el siguiente «P5» pisaría al P5 que sigue vivo.
    /// </summary>
    private string SiguienteId()
    {
        var mayor = 0;
        foreach (var p in Vm!.Programas)
        {
            if (p.Id.Length < 2 || char.ToUpperInvariant(p.Id[0]) != 'P') continue;
            if (int.TryParse(p.Id[1..], out var n) && n > mayor) mayor = n;
        }
        return $"P{mayor + 1}";
    }

    // ---------- archivo ----------

    private void Abrir_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        var dialogo = new OpenFileDialog
        {
            Title = "Abrir escenario",
            Filter = Vm.FiltroDelAlmacen,
            DefaultExt = Vm.ExtensionDelAlmacen,
        };
        if (dialogo.ShowDialog() != true) return;

        try
        {
            Vm.AbrirDesde(dialogo.FileName);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Avisar($"No se pudo abrir el archivo.\n\n{ex.Message}");
        }
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (string.IsNullOrWhiteSpace(Vm.ArchivoActual)) { GuardarComo_Click(sender, e); return; }
        Escribir(Vm.ArchivoActual);
    }

    private void GuardarComo_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        var dialogo = new SaveFileDialog
        {
            Title = "Guardar escenario",
            Filter = Vm.FiltroDelAlmacen,
            DefaultExt = Vm.ExtensionDelAlmacen,
            FileName = string.IsNullOrWhiteSpace(Vm.ArchivoActual)
                ? "escenario" + Vm.ExtensionDelAlmacen
                : Path.GetFileName(Vm.ArchivoActual),
        };
        if (dialogo.ShowDialog() != true) return;

        Escribir(dialogo.FileName);
    }

    private void Escribir(string ruta)
    {
        try
        {
            Vm!.GuardarEn(ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Avisar($"No se pudo guardar el archivo.\n\n{ex.Message}");
        }
    }

    private void ExportarJson_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        var dialogo = new SaveFileDialog
        {
            Title = "Exportar escenario a JSON",
            Filter = AlmacenJson.FiltroDeArchivo,
            DefaultExt = AlmacenJson.Extension,
            FileName = "escenario.json",
        };
        if (dialogo.ShowDialog() != true) return;

        try { Vm.ExportarJson(dialogo.FileName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Avisar($"No se pudo exportar.\n\n{ex.Message}");
        }
    }

    private void ImportarJson_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        var dialogo = new OpenFileDialog
        {
            Title = "Importar escenario desde JSON",
            Filter = AlmacenJson.FiltroDeArchivo,
            DefaultExt = AlmacenJson.Extension,
        };
        if (dialogo.ShowDialog() != true) return;

        try { Vm.ImportarJson(dialogo.FileName); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Avisar($"No se pudo importar.\n\n{ex.Message}");
        }
    }

    private void Ejemplo_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        var respuesta = MessageBox.Show(
            "Se va a descartar el escenario actual y cargar el de ejemplo. ¿Continuar?",
            "SPQR Task Manager", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (respuesta == MessageBoxResult.Yes) Vm.NuevoEscenarioDeEjemplo();
    }

    private void Avisar(string mensaje)
    {
        if (Vm is not null) Vm.Mensaje = mensaje.Replace("\n", " ");
        MessageBox.Show(mensaje, "SPQR Task Manager", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
