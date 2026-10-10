
using System.ComponentModel.DataAnnotations;

namespace TreTrip.Api.DTOs;

public class UpdateUserStatusDto
{
    [Required]
    public bool? IsActive { get; set; }
}
