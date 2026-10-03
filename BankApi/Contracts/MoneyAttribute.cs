using System.ComponentModel.DataAnnotations;

namespace BankApi.Contracts;

/// <summary>Monetary amount: positive (or zero when allowed), at most two decimal places.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class MoneyAttribute : ValidationAttribute
{
    private const decimal Max = 1_000_000_000_000m;

    public bool AllowZero { get; set; }

    public override bool IsValid(object? value)
    {
        if (value is not decimal amount)
            return true;

        if (amount < 0 || amount > Max || (amount == 0 && !AllowZero))
            return false;

        return decimal.Round(amount, 2) == amount;
    }

    public override string FormatErrorMessage(string name) =>
        $"{name} must be {(AllowZero ? "zero or greater" : "greater than zero")}, " +
        $"not exceed {Max:0} and have at most 2 decimal places.";
}
