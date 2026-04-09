using System.ComponentModel.DataAnnotations;

namespace BirthRegistry.Models;

public class BirthRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string ChildFirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ChildLastName { get; set; } = string.Empty;

    [Required]
    public DateTime DateOfBirth { get; set; }

    [Required]
    public string Gender { get; set; } = string.Empty;

    // Birth details
    [MaxLength(200)]
    public string? HospitalName { get; set; }

    [MaxLength(100)]
    public string? CityOfBirth { get; set; }

    [MaxLength(100)]
    public string? CountryOfBirth { get; set; } = "United Kingdom";

    public decimal? BirthWeightKg { get; set; }

    // Parent details
    [Required, MaxLength(100)]
    public string FatherFirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FatherLastName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string MotherFirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string MotherMaidenName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ParentAddress { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ParentPostcode { get; set; }

    [MaxLength(20)]
    public string? ContactPhone { get; set; }

    [MaxLength(200)]
    public string? ContactEmail { get; set; }

    // Registry metadata
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    [MaxLength(50)]
    public string RegistrationStatus { get; set; } = "Pending";

    public string? RegistrationNumber { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class BirthRecordDto
{
    public Guid? Id { get; set; }
    public string ChildFirstName { get; set; } = string.Empty;
    public string ChildLastName { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string? HospitalName { get; set; }
    public string? CityOfBirth { get; set; }
    public string? CountryOfBirth { get; set; }
    public string? BirthWeightKg { get; set; }
    public string FatherFirstName { get; set; } = string.Empty;
    public string FatherLastName { get; set; } = string.Empty;
    public string MotherFirstName { get; set; } = string.Empty;
    public string MotherMaidenName { get; set; } = string.Empty;
    public string ParentAddress { get; set; } = string.Empty;
    public string? ParentPostcode { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? Notes { get; set; }
    public string RegistrationStatus { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public DateTime? RegisteredAt { get; set; }
}
