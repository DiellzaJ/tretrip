using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;

namespace TreTrip.Api.Controllers;

[ApiController]
[Route("api/trips")]
[Authorize(Roles = "Scheduler")]
public class TripsController : ControllerBase
{
    private readonly ITripService _tripService;

    public TripsController(ITripService tripService)
    {
        _tripService = tripService;
    }

    // Get Scheduler ID from JWT
    private int? GetSchedulerId()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return int.TryParse(userId, out var id)
            ? id
            : null;
    }

    // Create a new trip
    [HttpPost]
    public async Task<ActionResult<TripResponseDto>> CreateTrip(
        [FromBody] CreateTripDto request)
    {
        var schedulerId = GetSchedulerId();

        if (!schedulerId.HasValue)
            return Unauthorized();

        try
        {
            var trip = await _tripService.CreateAsync(
                schedulerId.Value,
                request);

            return CreatedAtAction(
                nameof(GetById),
                new { id = trip.Id },
                trip);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // Get trips belonging to logged-in Scheduler
    [HttpGet]
    public async Task<ActionResult<List<TripResponseDto>>>
        GetMyTrips()
    {
        var schedulerId = GetSchedulerId();

        if (!schedulerId.HasValue)
            return Unauthorized();

        var trips = await _tripService
            .GetForSchedulerAsync(schedulerId.Value);

        return Ok(trips);
    }

    // Get a specific trip
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TripResponseDto>> GetById(
        int id)
    {
        var schedulerId = GetSchedulerId();

        if (!schedulerId.HasValue)
            return Unauthorized();

        var trip = await _tripService.GetByIdAsync(
            id,
            schedulerId.Value);

        if (trip == null)
        {
            return NotFound(new
            {
                message = "Trip not found."
            });
        }

        return Ok(trip);
    }

    // Cancel a trip created by the logged-in Scheduler
    [HttpPatch("{id:int}/cancel")]
    public async Task<IActionResult> CancelTrip(int id)
    {
        var schedulerId = GetSchedulerId();

        if (!schedulerId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _tripService.CancelAsync(
            id,
            schedulerId.Value
        );

        if (result == CancelTripResult.NotFound)
        {
            return NotFound(new
            {
                message = "Trip not found."
            });
        }

        if (result == CancelTripResult.InvalidStatus)
        {
            return Conflict(new
            {
                message = "This trip cannot be cancelled."
            });
        }

        return Ok(new
        {
            message = "Trip cancelled successfully.",
            tripId = id,
            status = "Cancelled"
        });
    }

}