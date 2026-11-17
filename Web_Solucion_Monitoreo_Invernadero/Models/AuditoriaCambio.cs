using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    [Table("auditoria_cambios")]
    public class AuditoriaCambio
    {
        [Key]
        [Column("id_auditoria")]
        public int IdAuditoria { get; set; }

        [Required]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Required]
        [StringLength(80)]
        [Column("accion")]
        public string Accion { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Column("entidad_afectada")]
        public string EntidadAfectada { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        [Column("detalle")]
        public string Detalle { get; set; } = string.Empty;

        [Required]
        [Column("fecha_hora")]
        public DateTime FechaHora { get; set; } = DateTime.Now;

        [ForeignKey(nameof(IdUsuario))]
        public virtual Usuario? Usuario { get; set; }
    }
}