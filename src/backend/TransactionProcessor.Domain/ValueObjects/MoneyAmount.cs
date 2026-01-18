namespace TransactionProcessor.Domain.ValueObjects;

/// <summary>
/// Represents a monetary amount with currency context.
/// This is a value object, meaning equality is based on the amount and currency values, not object identity.
/// </summary>
public class MoneyAmount : IEquatable<MoneyAmount>
{
    /// <summary>
    /// The numeric amount of money.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// The currency code (e.g., "BRL" for Brazilian Real).
    /// Default is "BRL" as per project specification.
    /// </summary>
    public string Currency { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MoneyAmount"/> class.
    /// </summary>
    /// <param name="amount">The amount of money. Must be greater than or equal to 0.</param>
    /// <param name="currency">The currency code. Defaults to "BRL".</param>
    /// <exception cref="ArgumentException">Thrown when amount is less than 0.</exception>
    public MoneyAmount(decimal amount, string currency = "BRL")
    {
        if (amount < 0)
        {
            throw new ArgumentException("Money amount cannot be negative.", nameof(amount));
        }

        Amount = amount;
        Currency = currency ?? "BRL";
    }

    /// <summary>
    /// Determines whether two <see cref="MoneyAmount"/> instances are equal based on their amount and currency values.
    /// </summary>
    /// <param name="other">The other <see cref="MoneyAmount"/> to compare.</param>
    /// <returns>true if both the amount and currency are equal; otherwise, false.</returns>
    public bool Equals(MoneyAmount? other)
    {
        if (other is null)
        {
            return false;
        }

        return Amount == other.Amount && Currency == other.Currency;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the objects are equal; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as MoneyAmount);
    }

    /// <summary>
    /// Returns the hash code for this instance based on the amount and currency.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Amount, Currency);
    }

    /// <summary>
    /// Returns a string representation of the money amount.
    /// </summary>
    /// <returns>A string in the format "currency amount" (e.g., "BRL 150.50").</returns>
    public override string ToString()
    {
        return $"{Currency} {Amount:N2}";
    }

    /// <summary>
    /// Implicitly converts a decimal to a <see cref="MoneyAmount"/> with default currency "BRL".
    /// </summary>
    /// <param name="amount">The decimal amount to convert.</param>
    /// <returns>A new <see cref="MoneyAmount"/> instance.</returns>
    public static implicit operator MoneyAmount(decimal amount)
    {
        return new MoneyAmount(amount);
    }

    /// <summary>
    /// Implicitly converts a <see cref="MoneyAmount"/> to its underlying decimal amount.
    /// </summary>
    /// <param name="moneyAmount">The <see cref="MoneyAmount"/> to convert.</param>
    /// <returns>The decimal amount.</returns>
    public static implicit operator decimal(MoneyAmount moneyAmount)
    {
        return moneyAmount?.Amount ?? 0;
    }

    /// <summary>
    /// Determines whether two <see cref="MoneyAmount"/> instances are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if the instances are equal; otherwise, false.</returns>
    public static bool operator ==(MoneyAmount? left, MoneyAmount? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="MoneyAmount"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if the instances are not equal; otherwise, false.</returns>
    public static bool operator !=(MoneyAmount? left, MoneyAmount? right)
    {
        return !(left == right);
    }
}
