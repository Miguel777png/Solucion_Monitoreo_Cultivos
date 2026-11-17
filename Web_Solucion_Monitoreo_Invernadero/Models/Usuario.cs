using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    [Table("usuarios")]
    public class Usuario
    {
        [Key]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es obligatorio")]
        [EmailAddress]
        [StringLength(120)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        [Column("rol")]
        public string Rol { get; set; } = "Operador";

        [Column("state")]
        public bool State { get; set; } = true;

        public virtual ICollection<Planta> PlantasCreadas { get; set; } = new List<Planta>();
        public virtual ICollection<Invernadero> InvernaderosModificados { get; set; } = new List<Invernadero>();
        public virtual ICollection<CalendarioRiego> CalendariosCreados { get; set; } = new List<CalendarioRiego>();
        public virtual ICollection<AuditoriaCambio> Auditorias { get; set; } = new List<AuditoriaCambio>();
    }
}