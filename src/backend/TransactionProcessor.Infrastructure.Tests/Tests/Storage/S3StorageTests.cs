using FluentAssertions;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace TransactionProcessor.Infrastructure.Tests.Tests.Storage;

/// <summary>
/// S3 storage integration tests using LocalStack.
/// Tests file upload, download, and deletion operations.
/// </summary>
public class S3StorageTests : IntegrationTestBase
{
    [Fact]
    public async Task UploadFile_Should_Store_Object_In_S3()
    {
        // Arrange
        var fileName = "test-cnab-file.txt";
        var fileContent = "test content for CNAB file"u8.ToArray();
        var key = $"cnab/{Guid.NewGuid()}/{fileName}";

        // Act
        await S3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = S3BucketName,
            Key = key,
            InputStream = new MemoryStream(fileContent)
        });

        // Assert
        var response = await S3Client.GetObjectAsync(S3BucketName, key);
        response.Should().NotBeNull();
        response.ResponseStream.Should().NotBeNull();
    }

    [Fact]
    public async Task DownloadFile_Should_Return_File_Content()
    {
        // Arrange
        var fileName = "test-document.txt";
        var fileContent = "expected content"u8.ToArray();
        var key = $"cnab/{Guid.NewGuid()}/{fileName}";

        await S3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = S3BucketName,
            Key = key,
            InputStream = new MemoryStream(fileContent)
        });

        // Act
        var response = await S3Client.GetObjectAsync(S3BucketName, key);
        using var reader = new StreamReader(response.ResponseStream);
        var content = await reader.ReadToEndAsync();

        // Assert
        content.Should().Be("expected content");
    }

    [Fact]
    public async Task DeleteFile_Should_Remove_Object_From_S3()
    {
        // Arrange
        var key = $"cnab/{Guid.NewGuid()}/test-file.txt";
        await S3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = S3BucketName,
            Key = key,
            InputStream = new MemoryStream("content"u8.ToArray())
        });

        // Act
        await S3Client.DeleteObjectAsync(S3BucketName, key);

        // Assert
        var ex = await Assert.ThrowsAsync<Amazon.S3.AmazonS3Exception>(async () =>
        {
            await S3Client.GetObjectAsync(S3BucketName, key);
        });

        ex.ErrorCode.Should().Be("NoSuchKey");
    }
}
