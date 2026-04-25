using System.ComponentModel.DataAnnotations;

namespace QualityDocAPI.DTOs
{
    public class CrearUsuarioDTO
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [RegularExpression(
            @"^(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{12,}$",
            ErrorMessage = "La contraseña debe tener al menos 12 caracteres, una mayúscula y un carácter especial.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debes asignar un rol al usuario.")]
        public int IdRol { get; set; }

        [Required(ErrorMessage = "Debes asignar un área al usuario.")]
        public int IdArea { get; set; }
    }
}