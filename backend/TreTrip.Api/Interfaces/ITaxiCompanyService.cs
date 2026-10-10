
using TreTrip.Api.DTOs;

namespace TreTrip.Api.Interfaces;

public interface ITaxiCompanyService
{
    Task<TaxiCompanyResponseDto> CreateAsync(
        CreateTaxiCompanyDto request);

    Task<List<TaxiCompanyResponseDto>> GetAllAsync();
}
