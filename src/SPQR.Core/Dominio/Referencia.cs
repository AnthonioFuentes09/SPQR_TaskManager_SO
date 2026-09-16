using SPQR.Core.Estructuras;

namespace SPQR.Core.Dominio;

/// <summary>
/// Un acceso a una página del proceso. Se distingue lectura de escritura
/// porque el bit M (modificada) solo se enciende al escribir, y ese bit
/// decide si al desalojar la página hay que mandarla al área de intercambio.
/// </summary>
public readonly record struct Referencia(int Pagina, bool EsEscritura)
{
    public override string ToString() => EsEscritura ? $"{Pagina}w" : Pagina.ToString();

    /// <summary>
    /// Convierte el texto que se escribe en la pantalla de programas.
    /// Formato: «0, 1, 2w, 1, 3» — la «w» (o «e») marca escritura.
    /// </summary>
    public static ListaEnlazada<Referencia> Parsear(string texto)
    {
        var lista = new ListaEnlazada<Referencia>();
        if (string.IsNullOrWhiteSpace(texto)) return lista;

        var separadores = new[] { ',', ' ', ';' };
        foreach (var crudo in texto.Split(separadores, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = crudo.Trim().ToLowerInvariant();
            var escritura = t.EndsWith('w') || t.EndsWith('e');
            if (escritura) t = t[..^1];
            if (int.TryParse(t, out var pagina) && pagina >= 0)
                lista.AgregarAlFinal(new Referencia(pagina, escritura));
        }
        return lista;
    }

    public static string Formatear(ListaEnlazada<Referencia> cadena)
        => string.Join(", ", cadena.Select(r => r.ToString()));
}
