namespace POS.Application.DTOs;

public record KitchenOrderDto(
    string OrderNumber,
    DateTime OrderTime,
    string TableOrType,
    string Station,
    bool IsPriority,
    string? Notes,
    List<KitchenItemDto> Items);

public record KitchenItemDto(
    string Name,
    decimal Quantity,
    string? ModifierSummary);
