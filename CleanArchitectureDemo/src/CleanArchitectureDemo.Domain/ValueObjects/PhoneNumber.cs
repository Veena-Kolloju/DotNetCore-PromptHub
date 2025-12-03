using System.Text.RegularExpressions;

namespace CleanArchitectureDemo.Domain.ValueObjects;

public class PhoneNumber : ValueObject
{
    public string Value { get; private set; }

    private PhoneNumber() { }

    public PhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Phone number cannot be empty", nameof(value));

        if (!IsValidPhoneNumber(value))
            throw new ArgumentException("Invalid phone number format", nameof(value));

        Value = value;
    }

    private static bool IsValidPhoneNumber(string phone)
    {
        return Regex.IsMatch(phone, @"^\+?[1-9]\d{1,14}$");
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static implicit operator string(PhoneNumber phone) => phone.Value;
    public static explicit operator PhoneNumber(string phone) => new(phone);
}