namespace TransactionProcessor.Infrastructure.Secrets;

/// <summary>
/// Marks a property as containing sensitive data that should never be logged
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class SensitiveAttribute : Attribute
{
}

/// <summary>
/// Configuration for database secrets.
/// </summary>
public class DatabaseSecrets
{
    /// <summary>
    /// PostgreSQL connection string (hierarchical name: TransactionProcessor/Database/ConnectionString)
    /// </summary>
    [Sensitive]
    public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for AWS S3 bucket secrets.
/// </summary>
public class AwsS3Secrets
{
    /// <summary>
    /// S3 bucket name (hierarchical name: TransactionProcessor/AWS/S3/BucketName)
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// AWS access key ID for S3 (hierarchical name: TransactionProcessor/AWS/S3/AccessKeyId)
    /// </summary>
    [Sensitive]
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// AWS secret access key for S3 (hierarchical name: TransactionProcessor/AWS/S3/SecretAccessKey)
    /// </summary>
    [Sensitive]
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>
    /// AWS region for S3 (hierarchical name: TransactionProcessor/AWS/S3/Region)
    /// </summary>
    public string Region { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for AWS SQS queue secrets.
/// </summary>
public class AwsSqsSecrets
{
    /// <summary>
    /// SQS queue URL (hierarchical name: TransactionProcessor/AWS/SQS/QueueUrl)
    /// </summary>
    public string QueueUrl { get; set; } = string.Empty;

    /// <summary>
    /// SQS Dead Letter Queue URL (hierarchical name: TransactionProcessor/AWS/SQS/DlqUrl)
    /// </summary>
    public string DlqUrl { get; set; } = string.Empty;

    /// <summary>
    /// AWS region for SQS (hierarchical name: TransactionProcessor/AWS/SQS/Region)
    /// </summary>
    public string Region { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for OAuth2 secrets (production only).
/// </summary>
public class OAuthSecrets
{
    /// <summary>
    /// OAuth client ID (hierarchical name: TransactionProcessor/OAuth/ClientId)
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth client secret (hierarchical name: TransactionProcessor/OAuth/ClientSecret)
    /// Production only - not used in development with LocalStack Cognito
    /// </summary>
    [Sensitive]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// OAuth authority/issuer URL (hierarchical name: TransactionProcessor/OAuth/Authority)
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// OAuth audience (hierarchical name: TransactionProcessor/OAuth/Audience)
    /// </summary>
    public string Audience { get; set; } = string.Empty;
}

/// <summary>
/// Container for all application secrets.
/// Secrets are loaded at startup and cached in memory.
/// </summary>
public class AppSecrets
{
    /// <summary>
    /// Database connection secrets
    /// </summary>
    public DatabaseSecrets Database { get; set; } = new();

    /// <summary>
    /// AWS S3 bucket secrets
    /// </summary>
    public AwsS3Secrets S3 { get; set; } = new();

    /// <summary>
    /// AWS SQS queue secrets
    /// </summary>
    public AwsSqsSecrets SQS { get; set; } = new();

    /// <summary>
    /// OAuth2 secrets (production only)
    /// </summary>
    public OAuthSecrets OAuth { get; set; } = new();

    /// <summary>
    /// Validates that all required secrets are present and non-empty.
    /// Throws an exception if any required secret is missing.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when required secrets are missing</exception>
    public void Validate()
    {
        var missingSecrets = new List<string>();

        if (string.IsNullOrWhiteSpace(Database.ConnectionString))
            missingSecrets.Add("Database.ConnectionString");

        if (string.IsNullOrWhiteSpace(S3.BucketName))
            missingSecrets.Add("S3.BucketName");

        if (string.IsNullOrWhiteSpace(S3.AccessKeyId))
            missingSecrets.Add("S3.AccessKeyId");

        if (string.IsNullOrWhiteSpace(S3.SecretAccessKey))
            missingSecrets.Add("S3.SecretAccessKey");

        if (string.IsNullOrWhiteSpace(SQS.QueueUrl))
            missingSecrets.Add("SQS.QueueUrl");

        if (string.IsNullOrWhiteSpace(SQS.DlqUrl))
            missingSecrets.Add("SQS.DlqUrl");

        if (missingSecrets.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing required secrets: {string.Join(", ", missingSecrets)}. " +
                "Ensure all secrets are configured in AWS Secrets Manager or LocalStack.");
        }
    }
}
