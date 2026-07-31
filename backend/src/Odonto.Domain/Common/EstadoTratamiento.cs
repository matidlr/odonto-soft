namespace Odonto.Domain.Common;

// Distingue si un evento del odontograma ya se hizo o todavía está
// planificado (para armar un plan de tratamiento y después ir marcando
// qué se fue completando).
public enum EstadoTratamiento
{
    Planificado,
    Realizado
}
