using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    /// <summary>
    /// Representa un cambio realizado por un usuario.
    /// </summary>
    [Table("auditoria_cambios")]
    public class AuditoriaCambio
    {
        /// <summary>Identificador del registro.</summary>
        [Key]
        [Column("id_auditoria")]
        public int IdAuditoria { get; set; }

        /// <summary>Usuario que realizó el cambio.</summary>
        [Required]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        /// <summary>Acción realizada.</summary>
        [Required]
        [StringLength(80)]
        [Column("accion")]
        public string Accion { get; set; } = string.Empty;

        /// <summary>Entidad que fue modificada.</summary>
        [Required]
        [StringLength(50)]
        [Column("entidad_afectada")]
        public string EntidadAfectada { get; set; } = string.Empty;

        /// <summary>Descripción del cambio.</summary>
        [Required]
        [StringLength(255)]
        [Column("detalle")]
        public string Detalle { get; set; } = string.Empty;

        /// <summary>Fecha y hora del cambio.</summary>
        [Required]
        [Column("fecha_hora")]
        public DateTime FechaHora { get; set; } = DateTime.Now;

        /// <summary>Datos del usuario relacionado.</summary>
        [ForeignKey(nameof(IdUsuario))]
        public virtual Usuario? Usuario { get; set; }
    }
}
