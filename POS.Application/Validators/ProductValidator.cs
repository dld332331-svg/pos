namespace POS.Application.Validators;

using POS.Application.DTOs;

public static class ProductValidator
{
    public static List<string> ValidateCreate(CreateProductRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.ArabicName)) errors.Add("اسم المنتج بالعربية مطلوب");
        if (request.ArabicName?.Length > 200) errors.Add("اسم المنتج يجب ألا يتجاوز 200 حرف");
        if (request.Price < 0) errors.Add("سعر البيع يجب أن يكون 0 أو أكبر");
        if (request.Cost < 0) errors.Add("التكلفة يجب أن تكون 0 أو أكبر");
        if (request.TaxRate is < 0 or > 100) errors.Add("نسبة الضريبة يجب أن تكون بين 0 و 100");
        if (request.MinStock < 0) errors.Add("الحد الأدنى للمخزون يجب أن يكون 0 أو أكبر");
        return errors;
    }
}