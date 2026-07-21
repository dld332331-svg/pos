using POS.Application.DTOs;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.Services.Implementations;

public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public CustomerService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<List<CustomerDto>> GetCustomersAsync(string? search = null)
    {
        var customers = await _unitOfWork.Customers.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            customers = customers
                .Where(c =>
                    c.Name.ToLower().Contains(term) ||
                    (c.Phone?.ToLower().Contains(term) ?? false))
                .ToList();
        }

        return customers
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CustomerDto(
                c.Id,
                c.Name,
                c.Phone,
                c.Email,
                c.Address,
                c.Notes,
                c.Balance))
            .ToList();
    }

    public async Task<CustomerDto> CreateCustomerAsync(string name, string? phone, string? email)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("اسم العميل مطلوب");

        var customer = new Customer
        {
            Name = ArabicName.Create(name),
            Phone = phone,
            Email = email,
            Balance = 0,
            IsActive = true
        };

        await _unitOfWork.Customers.AddAsync(customer);
        await _unitOfWork.SaveChangesAsync();

        return new CustomerDto(customer.Id, customer.Name, customer.Phone, customer.Email, customer.Address, customer.Notes, customer.Balance);
    }

    public async Task<CustomerDto> UpdateCustomerAsync(Guid id, string name, string? phone, string? email, string? address = null, string? notes = null)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(id)
            ?? throw new InvalidOperationException("العميل غير موجود");

        var beforeValue = $"Name={customer.Name},Phone={customer.Phone},Email={customer.Email}";

        customer.Name = ArabicName.Create(name);
        customer.Phone = phone;
        customer.Email = email;
        customer.Address = address;
        customer.Notes = notes;
        customer.MarkAsModified();

        await _unitOfWork.Customers.UpdateAsync(customer);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(null, POS.Domain.Enums.AuditActionType.SettingChanged, "Customer", id,
            beforeValue, $"Name={name},Phone={phone},Email={email},Address={address}", "Customer updated");

        return new CustomerDto(customer.Id, customer.Name, customer.Phone, customer.Email, customer.Address, customer.Notes, customer.Balance);
    }

    public async Task DeleteCustomerAsync(Guid id)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(id)
            ?? throw new InvalidOperationException("العميل غير موجود");

        customer.IsActive = false;
        customer.MarkAsModified();
        await _unitOfWork.Customers.UpdateAsync(customer);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<List<SaleSummaryDto>> GetCustomerOrderHistoryAsync(Guid customerId)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId)
            ?? throw new InvalidOperationException("العميل غير موجود");

        var sales = (await _unitOfWork.Sales.FindAsync(s => s.CustomerId == customerId))
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        return sales.Select(s => new SaleSummaryDto(
            s.Id,
            s.InvoiceNumber,
            s.SubTotal,
            s.TaxAmount,
            s.DiscountAmount,
            s.TotalAmount,
            s.Status.ToString(),
            s.CreatedAt)).ToList();
    }
}