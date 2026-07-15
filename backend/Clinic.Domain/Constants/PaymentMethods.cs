namespace Clinic.Domain.Constants;

/// <summary>How a clinic paid — closed set so reporting stays clean.</summary>
public static class PaymentMethods
{
    public const string Upi = "Upi";
    public const string BankTransfer = "BankTransfer";
    public const string Cash = "Cash";
    public const string Card = "Card";
    public const string Other = "Other";

    public static readonly string[] All = [Upi, BankTransfer, Cash, Card, Other];
}
