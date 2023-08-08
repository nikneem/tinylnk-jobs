﻿using Azure.Messaging.ServiceBus;
using Azure;
using System.Diagnostics;
using System.Text;
using Azure.Data.Tables;
using Newtonsoft.Json;
using TinyLink.Core.Commands.CommandMessages;
using TinyLink.Jobs.HitsProcessor.Entities;

const string sourceQueueName = "hits";

const string storageTableName = "hits";
const string partitionKey = "hit";


static async Task Main()
{
    Console.WriteLine("Starting the hits processor job");

    var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnection");
    var storageAccountConnection = Environment.GetEnvironmentVariable("StorageAccountConnection");

    var serviceBusClient = new ServiceBusClient(serviceBusConnectionString);
    var receiver = serviceBusClient.CreateReceiver(sourceQueueName);

    Console.WriteLine("Receiving message from service bus");
    var receivedMessage = await receiver.ReceiveMessageAsync();

    if (receivedMessage != null)
    {
        Console.WriteLine("Got a message from the service bus");
        var payloadString = Encoding.UTF8.GetString(receivedMessage.Body);
        var payload = JsonConvert.DeserializeObject<ProcessHitCommand>(payloadString);
        if (payload != null)
        {
            Console.WriteLine("Deserialized to a descent payload");

            Activity.Current?.AddTag("ShortCode", payload.ShortCode);
            Activity.Current?.AddTag("CreatedOn", payload.CreatedOn.ToString());

            var voteEntity = new ShortLinkHitEntity
            {
                PartitionKey = partitionKey,
                RowKey = Guid.NewGuid().ToString(),
                ShortCode = payload.ShortCode,
                Timestamp = payload.CreatedOn,
                ETag = ETag.All
            };

            Console.WriteLine("Created entity instance");
            var client = new TableClient(storageAccountConnection, storageTableName);
            Console.WriteLine("Saving entity in table storage");
            await client.UpsertEntityAsync(voteEntity);

            Console.WriteLine("Completing original message in service bus");
            await receiver.CompleteMessageAsync(receivedMessage);
            Console.WriteLine("All good, process complete");
        }
    }
}

await Main();