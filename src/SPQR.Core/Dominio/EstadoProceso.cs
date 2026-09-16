namespace SPQR.Core.Dominio;

/// <summary>Estados del proceso. La letra es la que se pinta en la cuadrícula.</summary>
public enum EstadoProceso
{
    Nuevo,       // aún no llega
    Listo,       // en cola, esperando CPU
    Ejecutando,  // E
    Bloqueado,   // B — resolviendo un fallo de página
    Finalizado   // F
}
