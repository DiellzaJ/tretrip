using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;

namespace TreTrip.Api.Controllers;

[ApiController]
[Route("api/driver/trips")]
[Authorize(Roles = "Driver")]
public class DriverTripsController : ControllerBase
{
    private readonly IDriverTripService _driverTripService;

    public DriverTripsController(
        IDriverTripService driverTripService)
    {
        _driverTripService = driverTripService;
    }

    // Get logged-in Driver's User ID from JWT
    private int? GetDriverUserId()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return int.TryParse(userId, out var id)
            ? id
            : null;
    }

    // Get all trips assigned to the driver
    [HttpGet]
    public async Task<ActionResult<List<TripResponseDto>>>
        GetMyTrips()
    {
        var driverUserId = GetDriverUserId();

        if (!driverUserId.HasValue)
        {
            return Unauthorized();
        }

        var trips = await _driverTripService
            .GetMyTripsAsync(driverUserId.Value);

        return Ok(trips);
    }

    // Start an assigned trip
    [HttpPatch("{id:int}/start")]
    public async Task<IActionResult> StartTrip(int id)
    {
        var driverUserId = GetDriverUserId();

        if (!driverUserId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _driverTripService.StartAsync(
            id,
            driverUserId.Value
        );

        if (result == DriverTripActionResult.NotFound)
        {
            return NotFound(new
            {
                message = "Trip not found or not assigned to you."
            });
        }

        if (result == DriverTripActionResult.TooEarly)
        {
            return Conflict(new
            {
                message = "Trip cannot be started more than 15 minutes early."
            });
        }

        if (result == DriverTripActionResult.InvalidStatus)
        {
            return Conflict(new
            {
                message = "Trip cannot be started in its current state."
            });
        }

        return Ok(new
        {
            message = "Trip started successfully.",
            tripId = id,
            status = "InProgress"
        });
    }

    // Complete an ongoing trip
    [HttpPatch("{id:int}/complete")]
    public async Task<IActionResult> CompleteTrip(int id)
    {
        var driverUserId = GetDriverUserId();

        if (!driverUserId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _driverTripService.CompleteAsync(
            id,
            driverUserId.Value
        );

        if (result == DriverTripActionResult.NotFound)
        {
            return NotFound(new
            {
                message = "Trip not found or not assigned to you."
            });
        }

        if (result == DriverTripActionResult.InvalidStatus)
        {
            return Conflict(new
            {
                message = "Only trips in progress can be completed."
            });
        }

        return Ok(new
        {
            message = "Trip completed successfully.",
            tripId = id,
            status = "Completed"
        });
    }
}