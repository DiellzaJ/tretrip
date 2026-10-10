
using Microsoft.EntityFrameworkCore;
using TreTrip.Api.Data;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;
using TreTrip.Api.Models;

namespace TreTrip.Api.Services;

public class TaxiCompanyService : ITaxiCompanyService
{
    private readonly AppDbContext _db;

    public TaxiCompanyService(AppDbContext db)
    {
        _db = db;
    }

    // Create a new taxi company
    public async Task<TaxiCompanyResponseDto> CreateAsync(
        CreateTaxiCompanyDto request)
    {
        var name = request.Name.Trim();
        var phone = request.Phone.Trim();

        // Validate required information
        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException(
                "Company name and phone are required.");
        }

        if (!request.ContractStartDate.HasValue)
        {
            throw new ArgumentException(
                "Contract start date is required.");
        }

        var startDate = request.ContractStartDate.Value;
        var endDate = request.ContractEndDate;

        // PostgreSQL timestamps require UTC
        if (startDate.Kind != DateTimeKind.Utc ||
            (endDate.HasValue &&
             endDate.Value.Kind != DateTimeKind.Utc))
        {
            throw new ArgumentException(
                "Contract dates must be in UTC.");
        }

        // Validate contract dates
        if (endDate.HasValue && endDate.Value < startDate)
        {
            throw new ArgumentException(
                "Contract end date cannot be before start date.");
        }

        // Create company
        var company = new TaxiCompany
        {
            Name = name,
            Phone = phone,
            ContractStartDate = startDate,
            ContractEndDate = endDate,
            IsActive = true
        };

        _db.TaxiCompanies.Add(company);
        await _db.SaveChangesAsync();

        return new TaxiCompanyResponseDto
        {
            Id = company.Id,
            Name = company.Name,
            Phone = company.Phone,
            IsActive = company.IsActive,
            ContractStartDate = company.ContractStartDate,
            ContractEndDate = company.ContractEndDate
        };
    }

    // Get all taxi companies
    public async Task<List<TaxiCompanyResponseDto>> GetAllAsync()
    {
        return await _db.TaxiCompanies
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new TaxiCompanyResponseDto
            {
                Id = c.Id,
                Name = c.Name,
                Phone = c.Phone,
                IsActive = c.IsActive,
                ContractStartDate = c.ContractStartDate,
                ContractEndDate = c.ContractEndDate
            })
            .ToListAsync();
    }
}
