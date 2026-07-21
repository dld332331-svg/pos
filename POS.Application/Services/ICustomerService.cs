using POS.Application.DTOs;

namespace POS.Application.Services;

public interface ICustomerService
{
    Task<List<CustomerDto>> GetCustomersAsync(string? search = null);
    Task<CustomerDto> CreateCustomerAsync(string name, string? phone, string? email);
    Task<CustomerDto> UpdateCustomerAsync(Guid id, string name, string? phone, string? email, string? address = null, string? notes = null);
    Task<List<SaleSummaryDto>> GetCustomerOrderHistoryAsync(Guid customerId);
    Task DeleteCustomerAsync(Guid id);
}