namespace SPQR.Core.Simulacion;

/// <summary>Estado de un marco físico en un instante dado.</summary>
public sealed record FotoMarco
{
    public required int Numero { get; init; }
    public int ProcesoId { get; init; }
    public string Proceso { get; init; } = "";
    public int Pagina { get; init; } = -1;
    public bool BitR { get; init; }
    public bool BitM { get; init; }

    public bool Libre => ProcesoId == 0;
    public string Contenido => Libre ? "libre" : $"P{ProcesoId} · {Pagina}";
    public string Bits => Libre ? "—" : $"R{(BitR ? 1 : 0)} M{(BitM ? 1 : 0)}";
}

/// <summary>
/// Foto completa de la memoria física en un instante. El motor la guarda tick
/// a tick para que la pantalla de emulación pueda retroceder: la interfaz no
/// recalcula nada, solo muestra la foto que le corresponde al instante.
/// </summary>
public sealed record FotoMemoria
{
    public required int Instante { get; init; }
    public required FotoMarco[] Marcos { get; init; }

    /// <summary>Marco que sufrió el desalojo en este instante, o -1.</summary>
    public int MarcoVictima { get; init; } = -1;

    public int EnUso
    {
        get { var n = 0; foreach (var m in Marcos) if (!m.Libre) n++; return n; }
    }
}
