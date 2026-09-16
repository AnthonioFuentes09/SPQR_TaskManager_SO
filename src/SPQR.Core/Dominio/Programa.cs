using SPQR.Core.Estructuras;

namespace SPQR.Core.Dominio;

/// <summary>
/// Un programa del catálogo. NO es un proceso: un mismo programa puede
/// instanciarse varias veces en la lista de ejecución, cada vez con su
/// propia ráfaga.
/// </summary>
public sealed class Programa
{
    public required string Id { get; init; }
    public required string Nombre { get; set; }

    /// <summary>Cuántas páginas de memoria virtual ocupa.</summary>
    public int TamanioEnPaginas { get; set; } = 4;

    /// <summary>
    /// Secuencia de páginas propias que toca al ejecutarse. Si la ráfaga es
    /// más larga que la cadena, la cadena se recorre en ciclo.
    /// </summary>
    public ListaEnlazada<Referencia> CadenaDeReferencias { get; private set; } = new();

    public int Prioridad { get; set; } = 1;

    /// <summary>Para planificación por sorteo.</summary>
    public int Boletos { get; set; } = 10;

    /// <summary>Para planificación garantizada, en porcentaje.</summary>
    public int PorcentajeAsignacion { get; set; } = 20;

    /// <summary>Texto editable de la cadena, tal como se ve en la pantalla 1.</summary>
    public string CadenaTexto
    {
        get => Referencia.Formatear(CadenaDeReferencias);
        set => CadenaDeReferencias = Referencia.Parsear(value);
    }

    /// <summary>Devuelve la referencia número <paramref name="indice"/>, en ciclo.</summary>
    public Referencia ReferenciaEn(int indice)
    {
        if (CadenaDeReferencias.EstaVacia)
            return new Referencia(indice % Math.Max(1, TamanioEnPaginas), false);

        var n = indice % CadenaDeReferencias.Cantidad;
        var i = 0;
        foreach (var r in CadenaDeReferencias)
            if (i++ == n) return r;
        return new Referencia(0, false);
    }
}
