using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    /// <summary>
    /// Representa una cuenta que puede ingresar al sistema.
    /// </summary>
    [Table("usuarios")]
    public class Usuario
    {
        /// <summary>Identificador del usuario.</summary>
        [Key]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        /// <summary>Nombre del usuario.</summary>
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Correo usado para iniciar sesión.</summary>
        [Required(ErrorMessage = "El correo electrónico es obligatorio")]
        [EmailAddress]
        [StringLength(120)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>Contraseña guardada por el sistema.</summary>
        [Required]
        [StringLength(255)]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>Rol asignado al usuario.</summary>
        [Required]
        [StringLength(30)]
        [Column("rol")]
        public string Rol { get; set; } = "Operador";

        /// <summary>Indica si la cuenta está activa.</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        /// <summary>Plantas creadas por el usuario.</summary>
        public virtual ICollection<Planta> PlantasCreadas { get; set; } = new List<Planta>();
        /// <summary>Invernaderos modificados por el usuario.</summary>
        public virtual ICollection<Invernadero> InvernaderosModificados { get; set; } = new List<Invernadero>();
        /// <summary>Horarios creados por el usuario.</summary>
        public virtual ICollection<CalendarioRiego> CalendariosCreados { get; set; } = new List<CalendarioRiego>();
        /// <summary>Registros de auditoría del usuario.</summary>
        public virtual ICollection<AuditoriaCambio> Auditorias { get; set; } = new List<AuditoriaCambio>();
    }
}
