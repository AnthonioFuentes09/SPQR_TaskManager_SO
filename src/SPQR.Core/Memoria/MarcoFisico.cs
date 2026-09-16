using SPQR.Core.Dominio;

namespace SPQR.Core.Memoria;

/// <summary>Un marco de la memoria física y qué página aloja.</summary>
public sealed class MarcoFisico
{
    public required int Numero { get; init; }
    public Proceso? Duenio { get; set; }
    public int Pagina { get; set; } = -1;
    public bool Libre => Duenio is null;

    public void Liberar() { Duenio = null; Pagina = -1; }
}
