using SPQR.Core.Dominio;
using SPQR.Core.Estructuras;

namespace SPQR.Core.Metricas;

public sealed class MetricasPlanificador
{
    public int CambiosDeContexto { get; set; }

    public double RetornoPromedio  { get; private set; }
    public double EsperaPromedio   { get; private set; }
    public double RespuestaPromedio { get; private set; }

    public void Calcular(ListaEnlazada<Proceso> procesos)
    {
        int n = 0; double r = 0, e = 0, resp = 0;
        foreach (var p in procesos)
        {
            if (p.TiempoRetorno is null) continue;
            n++;
            r    += p.TiempoRetorno.Value;
            e    += p.TiempoEspera ?? 0;
            resp += p.TiempoRespuesta ?? 0;
        }
        if (n == 0) return;
        RetornoPromedio   = Math.Round(r / n, 2);
        EsperaPromedio    = Math.Round(e / n, 2);
        RespuestaPromedio = Math.Round(resp / n, 2);
    }
}
