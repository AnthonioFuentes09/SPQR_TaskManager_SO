using SPQR.Core.Estructuras;

namespace SPQR.Core.Dominio;

/// <summary>
/// Un programa del catálogo. NO es un proceso: un mismo programa puede
/// instanciarse varias veces en la lista de ejecución, cada vez con su
/// propia ráfaga.
/// </summary>
public sealed class Programa : ObjetoObservable
{
    private string _id = "P1";
    private string _nombre = "";
    private int _tamanioEnPaginas = 4;
    private int _prioridad = 1;
    private int _boletos = 10;
    private int _porcentajeAsignacion = 20;
    private ListaEnlazada<Referencia> _cadena = new();

    public required string Id
    {
        get => _id;
        set => Asignar(ref _id, value);
    }

    public required string Nombre
    {
        get => _nombre;
        set => Asignar(ref _nombre, value);
    }

    /// <summary>Cuántas páginas de memoria virtual ocupa. Nunca menos de una.</summary>
    public int TamanioEnPaginas
    {
        get => _tamanioEnPaginas;
        set => Asignar(ref _tamanioEnPaginas, Math.Max(1, value));
    }

    /// <summary>
    /// Secuencia de páginas propias que toca al ejecutarse. Si la ráfaga es
    /// más larga que la cadena, la cadena se recorre en ciclo.
    /// </summary>
    public ListaEnlazada<Referencia> CadenaDeReferencias => _cadena;

    /// <summary>Número menor = más urgente.</summary>
    public int Prioridad
    {
        get => _prioridad;
        set => Asignar(ref _prioridad, Math.Max(1, value));
    }

    /// <summary>Para planificación por sorteo. Al menos uno, o nunca podría ganar.</summary>
    public int Boletos
    {
        get => _boletos;
        set => Asignar(ref _boletos, Math.Max(1, value));
    }

    /// <summary>Para planificación garantizada, en porcentaje (1 a 100).</summary>
    public int PorcentajeAsignacion
    {
        get => _porcentajeAsignacion;
        set => Asignar(ref _porcentajeAsignacion, Math.Clamp(value, 1, 100));
    }

    /// <summary>Texto editable de la cadena, tal como se ve en la pantalla 1.</summary>
    public string CadenaTexto
    {
        get => Referencia.Formatear(_cadena);
        set
        {
            _cadena = Referencia.Parsear(value);
            Notificar();
            Notificar(nameof(CadenaDeReferencias));
            Notificar(nameof(PaginaMasAltaReferenciada));
        }
    }

    /// <summary>
    /// La página más alta que menciona la cadena. Si supera al tamaño del
    /// programa, la cadena está pidiendo una página que el programa no tiene.
    /// </summary>
    public int PaginaMasAltaReferenciada
    {
        get
        {
            var max = -1;
            foreach (var r in _cadena) if (r.Pagina > max) max = r.Pagina;
            return max;
        }
    }

    /// <summary>Devuelve la referencia número <paramref name="indice"/>, en ciclo.</summary>
    public Referencia ReferenciaEn(int indice)
    {
        if (_cadena.EstaVacia)
            return new Referencia(indice % Math.Max(1, TamanioEnPaginas), false);

        var n = indice % _cadena.Cantidad;
        var i = 0;
        foreach (var r in _cadena)
            if (i++ == n) return r;
        return new Referencia(0, false);
    }
}
