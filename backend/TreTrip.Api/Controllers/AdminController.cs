
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;

namespace TreTrip.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ITaxiCompanyService _taxiCompanyService;
    private readonly IDriverService _driverService;
    private readonly IVehicleService _vehicleService;



    public AdminController(
        IAdminService adminService,
        ITaxiCompanyService taxiCompanyService,
        IDriverService driverService,
        IVehicleService vehicleService)
    {
        _adminService = adminService;
        _taxiCompanyService = taxiCompanyService;
        _driverService = driverService;
        _vehicleService = vehicleService;
    }



    // Test Admin authorization
    [HttpGet("test")]
    public IActionResult TestAdminAccess()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        return Ok(new
        {
            Message = "Admin access granted",
            UserId = userId,
            Role = "Admin"
        });
    }

    // Create an employee
    [HttpPost("employees")]
    public async Task<ActionResult<UserResponseDto>>
        CreateEmployee([FromBody] CreateEmployeeDto request)
    {
        try
        {
            var employee = await _adminService
                .CreateEmployeeAsync(request);

            return StatusCode(201, employee);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
            when (ex.Message ==
                  "A user with this email already exists.")
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // Get all users
    [HttpGet("users")]
    public async Task<ActionResult<List<UserResponseDto>>>
        GetUsers()
    {
        var users = await _adminService.GetUsersAsync();

        return Ok(users);
    }

    // Activate or deactivate a user
    [HttpPatch("users/{userId:int}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        int userId,
        [FromBody] UpdateUserStatusDto request)
    {
        var success = await _adminService
            .SetUserActiveStatusAsync(
                userId,
                request.IsActive!.Value
            );

        if (!success)
        {
            return NotFound(new
            {
                message = "User not found or cannot be modified."
            });
        }

        return Ok(new
        {
            message = "User status updated successfully.",
            userId,
            isActive = request.IsActive.Value
        });
    }

        // Create a taxi company
        [HttpPost("taxi-companies")]
        public async Task<ActionResult<TaxiCompanyResponseDto>>
            CreateTaxiCompany(
                [FromBody] CreateTaxiCompanyDto request)
        {
            try
            {
                var company = await _taxiCompanyService
                    .CreateAsync(request);

                return StatusCode(201, company);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Get all taxi companies
        [HttpGet("taxi-companies")]
        public async Task<ActionResult<List<TaxiCompanyResponseDto>>>
            GetTaxiCompanies()
        {
            var companies = await _taxiCompanyService
                .GetAllAsync();

            return Ok(companies);
        }


    // Create a new driver
    [HttpPost("drivers")]
    public async Task<ActionResult<DriverResponseDto>> CreateDriver(
        [FromBody] CreateDriverDto request)
    {
        try
        {
            var driver = await _driverService.CreateAsync(request);

            return StatusCode(201, driver);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
            when (ex.Message ==
                "A user with this email already exists." ||
                ex.Message ==
                "A driver with this license already exists.")
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
            when (ex.Message ==
                "Taxi company is inactive or contract is not valid.")
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // Get all drivers
    [HttpGet("drivers")]
    public async Task<ActionResult<List<DriverResponseDto>>>
        GetDrivers()
    {
        var drivers = await _driverService.GetAllAsync();

        return Ok(drivers);
    }

    // Update driver availability
    [HttpPatch("drivers/{driverId:int}/availability")]
    public async Task<IActionResult> UpdateDriverAvailability(
        int driverId,
        [FromBody] UpdateDriverAvailabilityDto request)
    {
        var success = await _driverService.SetAvailabilityAsync(
            driverId,
            request.IsAvailable!.Value
        );

        if (!success)
        {
            return BadRequest(new
            {
                message = "Driver not found or availability change is not allowed."
            });
        }

        return Ok(new
        {
            message = "Driver availability updated successfully.",
            driverId,
            isAvailable = request.IsAvailable.Value
        });
    }


    // Create a new vehicle
    [HttpPost("vehicles")]
    public async Task<ActionResult<VehicleResponseDto>> CreateVehicle(
        [FromBody] CreateVehicleDto request)
    {
        try
        {
            var vehicle = await _vehicleService.CreateAsync(request);

            return StatusCode(201, vehicle);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
            when (ex.Message ==
                "A vehicle with this plate number already exists.")
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
            when (ex.Message ==
                "Taxi company is inactive or contract is not valid.")
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // Get all registered vehicles
    [HttpGet("vehicles")]
    public async Task<ActionResult<List<VehicleResponseDto>>> GetVehicles()
    {
        var vehicles = await _vehicleService.GetAllAsync();

        return Ok(vehicles);
    }

    // Update vehicle availability
    [HttpPatch("vehicles/{vehicleId:int}/availability")]
    public async Task<IActionResult> UpdateVehicleAvailability(
        int vehicleId,
        [FromBody] UpdateVehicleAvailabilityDto request)
    {
        var success = await _vehicleService.SetAvailabilityAsync(
            vehicleId,
            request.IsAvailable!.Value
        );

        if (!success)
        {
            return BadRequest(new
            {
                message = "Vehicle not found or availability change is not allowed."
            });
        }

        return Ok(new
        {
            message = "Vehicle availability updated successfully.",
            vehicleId,
            isAvailable = request.IsAvailable.Value
        });
    }

    }
