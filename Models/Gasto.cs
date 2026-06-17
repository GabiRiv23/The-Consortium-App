using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TheConsortiumApp.Models
{
    public class Gasto
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El concepto o descripción del gasto es obligatorio")]
        [StringLength(200, ErrorMessage = "La descripción no puede superar los 200 caracteres")]
        public string Concepto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar la categoría del gasto")]
        public CategoriaGasto Categoria { get; set; }

        [Required(ErrorMessage = "El monto es obligatorio")]
        [Column(TypeName = "decimal(18, 2)")]
        [Range(0.01, 99999999.99, ErrorMessage = "El monto debe ser mayor a 0")]
        public decimal Monto { get; set; }

        [Required(ErrorMessage = "La fecha de registro es obligatoria")]
        [DataType(DataType.Date)]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        [Required]
        public int ConsorcioId { get; set; }

        [ForeignKey("ConsorcioId")]
        public virtual Consorcio? Consorcio { get; set; }
    }
}
