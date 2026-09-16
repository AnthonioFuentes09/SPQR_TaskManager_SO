namespace SPQR.Core.Metricas;

/// <summary>
/// Rendimiento de la MMU tal como lo calcula el docente:
/// F = fallos ÷ referencias, y rendimiento = 1 − F, en porcentaje.
/// </summary>
public sealed class MetricasMmu
{
    public int ReferenciasTotales { get; set; }
    public int FallosDePagina { get; set; }
    public int Desalojos { get; set; }
    public int EscriturasADisco { get; set; }

    public double F => ReferenciasTotales == 0 ? 0 : (double)FallosDePagina / ReferenciasTotales;
    public double Rendimiento => 1 - F;
    public double RendimientoPorcentual => Math.Round(Rendimiento * 100, 2);
}
