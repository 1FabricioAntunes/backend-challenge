using Amazon.SQS.Model;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace TransactionProcessor.Infrastructure.Tests.Tests.Messaging;

/// <summary>
/// SQS messaging integration tests using LocalStack.
/// Tests message publishing, consuming, and queue management.
/// </summary>
public class SQSMessagingTests : IntegrationTestBase
{
    [Fact]
    public async Task SendMessage_Should_Add_Message_To_Queue()
    {
        // Arrange
        var messageBody = "{\"fileId\": \"123e4567-e89b-12d3-a456-426614174000\"}";

        // Act
        var response = await SqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = QueueUrl,
            MessageBody = messageBody
        });

        // Assert
        response.Should().NotBeNull();
        response.MessageId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReceiveMessage_Should_Get_Message_From_Queue()
    {
        // Arrange
        var messageBody = "test message content";
        await SqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = QueueUrl,
            MessageBody = messageBody
        });

        // Act
        var response = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = QueueUrl,
            MaxNumberOfMessages = 1
        });

        // Assert
        response.Messages.Should().HaveCount(1);
        response.Messages[0].Body.Should().Be(messageBody);
    }

    [Fact]
    public async Task DeleteMessage_Should_Remove_From_Queue()
    {
        // Arrange
        var messageBody = "message to delete";
        await SqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = QueueUrl,
            MessageBody = messageBody
        });

        var receiveResponse = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = QueueUrl,
            MaxNumberOfMessages = 1
        });

        var message = receiveResponse.Messages[0];

        // Act
        await SqsClient.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = QueueUrl,
            ReceiptHandle = message.ReceiptHandle
        });

        // Assert
        var verifyResponse = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = QueueUrl,
            MaxNumberOfMessages = 1
        });

        verifyResponse.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQueueAttributes_Should_Return_Message_Count()
    {
        // Arrange
        var messageCount = 3;
        for (int i = 0; i < messageCount; i++)
        {
            await SqsClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = QueueUrl,
                MessageBody = $"message {i}"
            });
        }

        // Act
        var attributes = await SqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = QueueUrl,
            AttributeNames = new List<string> { "All" }
        });

        // Assert
        attributes.Attributes.Should().ContainKey("ApproximateNumberOfMessages");
        var count = int.Parse(attributes.Attributes["ApproximateNumberOfMessages"]);
        count.Should().Be(messageCount);
    }
}
