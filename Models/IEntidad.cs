namespace GestionEscuelaEspana.Models
{
    /// <summary>Contrato mínimo de toda entidad persistida con clave numérica autoincremental.</summary>
    public interface IEntidad
    {
        int Id { get; set; }
    }
}
