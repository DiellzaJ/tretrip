using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreTrip.Api.Data;
using TreTrip.Api.DTOs;

namespace TreTrip.Api.Controllers;

[ApiController]
[Route("api/admin/trips")]
[Authorize(Roles = "Admin")]
public class AdminTripsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminTripsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminTripResponseDto>>> GetTrips()
    {
        // Read-only endpoint for the small/demo-sized dataset used in this MVP.
        // For a large fleet, add server pagination and aggregate endpoints.
        var trips = await _db.Trips
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Select(t => new AdminTripResponseDto
            {
                Id = t.Id,
                SchedulerId = t.SchedulerId,
                SchedulerName = t.Scheduler.FullName,
                SchedulerEmail = t.Scheduler.Email,
                DriverName = t.Driver != null ? t.Driver.User.FullName : null,
                VehiclePlateNumber = t.Vehicle != null ? t.Vehicle.PlateNumber : null,
                PickupAddress = t.PickupAddress,
                DestinationAddress = t.DestinationAddress,
                DistanceKm = t.DistanceKm,
                EstimatedDurationMinutes = t.EstimatedDurationMinutes,
                Price = t.Price,
                ScheduledAt = t.ScheduledAt,
                CreatedAt = t.CreatedAt,
                CompletedAt = t.CompletedAt,
                Status = t.Status
            })
            .ToListAsync();

        return Ok(trips);
    }
}
