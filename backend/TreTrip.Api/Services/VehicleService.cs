using Microsoft.EntityFrameworkCore;
using TreTrip.Api.Data;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;
using TreTrip.Api.Models;

namespace TreTrip.Api.Services;

public class VehicleService : IVehicleService
{
    private readonly AppDbContext _db;

    public VehicleService(AppDbContext db)
    {
        _db = db;
    }

    // Create a new vehicle
    public async Task<VehicleResponseDto> CreateAsync(
        CreateVehicleDto request)
    {
        var plateNumber = request.PlateNumber
            .Trim().ToUpperInvariant();

        var model = request.Model.Trim();

        // Validate input
        if (string.IsNullOrWhiteSpace(plateNumber) ||
            string.IsNullOrWhiteSpace(model) ||
            request.TaxiCompanyId <= 0)
        {
            throw new ArgumentException(
                "Invalid vehicle information.");
        }

        // Check duplicate plate number
        var plateExists = await _db.Vehicles
            .AnyAsync(v =>
                v.PlateNumber.ToUpper() == plateNumber);

        if (plateExists)
        {
            throw new InvalidOperationException(
                "A vehicle with this plate number already exists.");
        }

        // Find taxi company
        var company = await _db.TaxiCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.Id == request.TaxiCompanyId);

        if (company == null)
        {
            throw new KeyNotFoundException(
                "Taxi company not found.");
        }

        var now = DateTime.UtcNow;

        // Validate company contract
        if (!company.IsActive ||
            company.ContractStartDate > now ||
            (company.ContractEndDate.HasValue &&
             company.ContractEndDate.Value < now))
        {
            throw new InvalidOperationException(
                "Taxi company is inactive or contract is not valid.");
        }

        // Create vehicle
        var vehicle = new Vehicle
        {
            PlateNumber = plateNumber,
            Model = model,
            TaxiCompanyId = company.Id,
            IsActive = true,
            IsAvailable = false
        };

        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync();

        return new VehicleResponseDto
        {
            Id = vehicle.Id,
            PlateNumber = vehicle.PlateNumber,
            Model = vehicle.Model,
            TaxiCompanyId = company.Id,
            TaxiCompanyName = company.Name,
            IsActive = vehicle.IsActive,
            IsAvailable = vehicle.IsAvailable
        };
    }

    // Get all registered vehicles
    public async Task<List<VehicleResponseDto>> GetAllAsync()
    {
        return await _db.Vehicles
            .AsNoTracking()
            .OrderBy(v => v.Id)
            .Select(v => new VehicleResponseDto
            {
                Id = v.Id,
                PlateNumber = v.PlateNumber,
                Model = v.Model,
                TaxiCompanyId = v.TaxiCompanyId,
                TaxiCompanyName = v.TaxiCompany.Name,
                IsActive = v.IsActive,
                IsAvailable = v.IsAvailable
            })
            .ToListAsync();
    }

    // Update vehicle availability
    public async Task<bool> SetAvailabilityAsync(
        int vehicleId,
        bool isAvailable)
    {
        var vehicle = await _db.Vehicles
            .Include(v => v.TaxiCompany)
            .FirstOrDefaultAsync(v => v.Id == vehicleId);

        if (vehicle == null)
        {
            return false;
        }

        // Validate before making vehicle available
        if (isAvailable)
        {
            var now = DateTime.UtcNow;
            var company = vehicle.TaxiCompany;

            if (!vehicle.IsActive ||
                !company.IsActive ||
                company.ContractStartDate > now ||
                (company.ContractEndDate.HasValue &&
                 company.ContractEndDate.Value < now))
            {
                return false;
            }
        }

        vehicle.IsAvailable = isAvailable;

        await _db.SaveChangesAsync();

        return true;
    }
}