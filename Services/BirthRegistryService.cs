using BirthRegistry.Data;
using BirthRegistry.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BirthRegistry.Services;

public interface IBirthRegistryService
{
    Task<BirthRecord> RegisterBirthAsync(BirthRecordDto dto);
    Task<BirthRecord?> GetByIdAsync(Guid id);
    Task<BirthRecord?> GetByRegistrationNumberAsync(string registrationNumber);
    Task<(IEnumerable<BirthRecord> Records, int Total)> SearchAsync(string? lastName, DateTime? from, DateTime? to, int page, int pageSize);
    Task<BirthRecord> UpdateStatusAsync(Guid id, string status);
    Task DeleteAsync(Guid id);
}

public class BirthRegistryService(BirthRegistryDbContext db, ILogger<BirthRegistryService> logger) : IBirthRegistryService
{
    public async Task<BirthRecord> RegisterBirthAsync(BirthRecordDto dto)
    {
        logger.LogInformation("Registering birth for {FirstName} {LastName}, DOB: {DOB}",
            dto.ChildFirstName, dto.ChildLastName, dto.DateOfBirth);

        var record = MapFromDto(dto);
        record.RegistrationNumber = GenerateRegistrationNumber();
        record.RegistrationStatus = "Registered";
        record.RegisteredAt = DateTime.UtcNow;

        db.BirthRecords.Add(record);
        await db.SaveChangesAsync();

        logger.LogInformation("Birth registered successfully. Registration number: {RegNo}", record.RegistrationNumber);

        return record;
    }

    public async Task<BirthRecord?> GetByIdAsync(Guid id) =>
        await db.BirthRecords.FindAsync(id);

    public async Task<BirthRecord?> GetByRegistrationNumberAsync(string registrationNumber) =>
        await db.BirthRecords.FirstOrDefaultAsync(r => r.RegistrationNumber == registrationNumber);

    public async Task<(IEnumerable<BirthRecord> Records, int Total)> SearchAsync(
        string? lastName, DateTime? from, DateTime? to, int page, int pageSize)
    {
        var query = db.BirthRecords.AsQueryable();

        if (!string.IsNullOrWhiteSpace(lastName))
            query = query.Where(r => r.ChildLastName.Contains(lastName));

        if (from.HasValue)
            query = query.Where(r => r.DateOfBirth >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.DateOfBirth <= to.Value);

        var total = await query.CountAsync();
        var records = await query
            .OrderByDescending(r => r.RegisteredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (records, total);
    }

    public async Task<BirthRecord> UpdateStatusAsync(Guid id, string status)
    {
        var record = await db.BirthRecords.FindAsync(id)
            ?? throw new KeyNotFoundException($"Record {id} not found");

        record.RegistrationStatus = status;
        await db.SaveChangesAsync();

        logger.LogInformation("Updated status for record {Id} to {Status}", id, status);
        return record;
    }

    public async Task DeleteAsync(Guid id)
    {
        var record = await db.BirthRecords.FindAsync(id)
            ?? throw new KeyNotFoundException($"Record {id} not found");

        db.BirthRecords.Remove(record);
        await db.SaveChangesAsync();

        logger.LogWarning("Deleted birth record {Id} ({RegNo})", id, record.RegistrationNumber);
    }

    private static BirthRecord MapFromDto(BirthRecordDto dto) => new()
    {
        ChildFirstName = dto.ChildFirstName,
        ChildLastName = dto.ChildLastName,
        DateOfBirth = DateTime.Parse(dto.DateOfBirth),
        Gender = dto.Gender,
        HospitalName = dto.HospitalName,
        CityOfBirth = dto.CityOfBirth,
        CountryOfBirth = dto.CountryOfBirth ?? "United Kingdom",
        BirthWeightKg = string.IsNullOrWhiteSpace(dto.BirthWeightKg)
            ? null : decimal.TryParse(dto.BirthWeightKg, out var w) ? w : null,
        FatherFirstName = dto.FatherFirstName,
        FatherLastName = dto.FatherLastName,
        MotherFirstName = dto.MotherFirstName,
        MotherMaidenName = dto.MotherMaidenName,
        ParentAddress = dto.ParentAddress,
        ParentPostcode = dto.ParentPostcode,
        ContactPhone = dto.ContactPhone,
        ContactEmail = dto.ContactEmail,
        Notes = dto.Notes
    };

    private static string GenerateRegistrationNumber()
    {
        var year = DateTime.UtcNow.Year;
        var random = new Random();
        var suffix = random.Next(100000, 999999);
        return $"BR-{year}-{suffix}";
    }
}
