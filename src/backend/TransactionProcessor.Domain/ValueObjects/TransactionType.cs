namespace TransactionProcessor.Domain.ValueObjects;

/// <summary>
/// Represents a CNAB transaction type with its characteristics.
/// This is a value object that defines the fixed set of transaction types (1-9) used in CNAB file processing.
/// 
/// CNAB Specification Reference:
/// - Type 1: Débito (Debit) - Removes money from store balance
/// - Type 2: Boleto (Debit) - Removes money from store balance
/// - Type 3: Financiamento (Debit) - Removes money from store balance
/// - Type 4: Crédito (Credit) - Adds money to store balance
/// - Type 5: Recebimento Aluguel (Credit) - Adds money to store balance
/// - Type 6: Recebimento Taxas (Credit) - Adds money to store balance
/// - Type 7: Recebimento Custas (Credit) - Adds money to store balance
/// - Type 8: Outras Receitas (Credit) - Adds money to store balance
/// - Type 9: Transferência (Credit) - Adds money to store balance
/// </summary>
public class TransactionType : IEquatable<TransactionType>
{
    /// <summary>
    /// The transaction type code (1-9).
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// The human-readable name of the transaction type.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Indicates whether this transaction type represents a credit (adds to balance) or debit (removes from balance).
    /// true for credit (types 4, 5, 6, 7, 8, 9), false for debit (types 1, 2, 3).
    /// </summary>
    public bool IsCredit { get; }

    /// <summary>
    /// The sign multiplier used for balance calculations.
    /// +1 for credit transactions (adds to balance), -1 for debit transactions (subtracts from balance).
    /// </summary>
    public int Sign => IsCredit ? 1 : -1;

    /// <summary>
    /// Static instance for CNAB Type 1 - Débito (Debit).
    /// </summary>
    public static readonly TransactionType Type1 = new(1, "Débito", isCredit: false);

    /// <summary>
    /// Static instance for CNAB Type 2 - Boleto (Debit).
    /// </summary>
    public static readonly TransactionType Type2 = new(2, "Boleto", isCredit: false);

    /// <summary>
    /// Static instance for CNAB Type 3 - Financiamento (Debit).
    /// </summary>
    public static readonly TransactionType Type3 = new(3, "Financiamento", isCredit: false);

    /// <summary>
    /// Static instance for CNAB Type 4 - Crédito (Credit).
    /// </summary>
    public static readonly TransactionType Type4 = new(4, "Crédito", isCredit: true);

    /// <summary>
    /// Static instance for CNAB Type 5 - Recebimento Aluguel (Credit).
    /// </summary>
    public static readonly TransactionType Type5 = new(5, "Recebimento Aluguel", isCredit: true);

    /// <summary>
    /// Static instance for CNAB Type 6 - Recebimento Taxas (Credit).
    /// </summary>
    public static readonly TransactionType Type6 = new(6, "Recebimento Taxas", isCredit: true);

    /// <summary>
    /// Static instance for CNAB Type 7 - Recebimento Custas (Credit).
    /// </summary>
    public static readonly TransactionType Type7 = new(7, "Recebimento Custas", isCredit: true);

    /// <summary>
    /// Static instance for CNAB Type 8 - Outras Receitas (Credit).
    /// </summary>
    public static readonly TransactionType Type8 = new(8, "Outras Receitas", isCredit: true);

    /// <summary>
    /// Static instance for CNAB Type 9 - Transferência (Credit).
    /// </summary>
    public static readonly TransactionType Type9 = new(9, "Transferência", isCredit: true);

    /// <summary>
    /// Private constructor to enforce use of static instances.
    /// </summary>
    /// <param name="code">The transaction type code.</param>
    /// <param name="name">The human-readable name.</param>
    /// <param name="isCredit">Whether this is a credit (true) or debit (false) transaction.</param>
    private TransactionType(int code, string name, bool isCredit)
    {
        Code = code;
        Name = name;
        IsCredit = isCredit;
    }

    /// <summary>
    /// Retrieves the <see cref="TransactionType"/> for the specified CNAB transaction code.
    /// </summary>
    /// <param name="code">The CNAB transaction code (1-9).</param>
    /// <returns>The corresponding <see cref="TransactionType"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the code is not between 1 and 9.</exception>
    public static TransactionType FromCode(int code)
    {
        return code switch
        {
            1 => Type1,
            2 => Type2,
            3 => Type3,
            4 => Type4,
            5 => Type5,
            6 => Type6,
            7 => Type7,
            8 => Type8,
            9 => Type9,
            _ => throw new ArgumentException($"Invalid transaction type code: {code}. Code must be between 1 and 9.", nameof(code))
        };
    }

    /// <summary>
    /// Returns all available transaction type instances.
    /// </summary>
    /// <returns>An array containing all nine transaction type instances.</returns>
    public static TransactionType[] GetAllTypes()
    {
        return [Type1, Type2, Type3, Type4, Type5, Type6, Type7, Type8, Type9];
    }

    /// <summary>
    /// Determines whether two <see cref="TransactionType"/> instances are equal based on their code.
    /// </summary>
    /// <param name="other">The other <see cref="TransactionType"/> to compare.</param>
    /// <returns>true if both instances have the same code; otherwise, false.</returns>
    public bool Equals(TransactionType? other)
    {
        return other is not null && Code == other.Code;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the objects are equal; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as TransactionType);
    }

    /// <summary>
    /// Returns the hash code for this instance based on the code.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return Code.GetHashCode();
    }

    /// <summary>
    /// Returns a string representation of the transaction type.
    /// </summary>
    /// <returns>A string in the format "Code: name" (e.g., "Code: 4 (Crédito)").</returns>
    public override string ToString()
    {
        return $"Code: {Code} ({Name})";
    }

    /// <summary>
    /// Determines whether two <see cref="TransactionType"/> instances are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if the instances are equal; otherwise, false.</returns>
    public static bool operator ==(TransactionType? left, TransactionType? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="TransactionType"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if the instances are not equal; otherwise, false.</returns>
    public static bool operator !=(TransactionType? left, TransactionType? right)
    {
        return !(left == right);
    }
}
