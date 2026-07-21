namespace POS.Application.Validators;

public static class SaleValidator
{
    public static List<string> ValidatePayment(decimal amountDue, decimal amountPaid)
    {
        var errors = new List<string>();
        if (amountPaid <= 0) errors.Add("المبلغ المدفوع يجب أن يكون أكبر من صفر");
        if (amountPaid < amountDue) errors.Add("المبلغ المدفوع أقل من المبلغ المطلوب");
        return errors;
    }

    public static List<string> ValidateDiscount(decimal discountAmount, decimal subTotal)
    {
        var errors = new List<string>();
        if (discountAmount < 0) errors.Add("مبلغ الخصم يجب أن يكون 0 أو أكبر");
        if (discountAmount > subTotal) errors.Add("مبلغ الخصم يتجاوز المبلغ الإجمالي");
        return errors;
    }
}