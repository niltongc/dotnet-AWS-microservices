using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SNSEvents;
using HotelCreatedEventHandler.Models;
using Nest;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace HotelCreatedEventHandler
{
    public class HotelCreatedEventHandler
    {
        public async Task Handler(SNSEvent snsEvent)
        {
            var dbClient = new AmazonDynamoDBClient();
            var table = Table.LoadTable(dbClient, "hotel-created-events-ids");

            var host = Environment.GetEnvironmentVariable("host");
            var userName = Environment.GetEnvironmentVariable("userName");
            var password = Environment.GetEnvironmentVariable("password");
            var indexName = Environment.GetEnvironmentVariable("indexName");

            var connSetting = new ConnectionSettings(new Uri(host));
            connSetting.BasicAuthentication(userName, password);
            connSetting.DefaultIndex(indexName);
            connSetting.DefaultMappingFor<Hotel>(m => m.IdProperty(p => p.Id));

            var esClient = new Nest.ElasticClient(connSetting);

            if (!(await esClient.Indices.ExistsAsync(indexName)).Exists)
            {
                await esClient.Indices.CreateAsync(indexName);
            }

            foreach (var eventRecord in snsEvent.Records)
            {
                var eventId = eventRecord.Sns.MessageId;
                var foundItem = await table.GetItemAsync(eventId);
                if(foundItem == null)
                {
                    await table.PutItemAsync(new Document
                    {
                        ["eventId"] = eventId
                    });
                }

                var hotel = JsonSerializer.Deserialize<Hotel>(eventRecord.Sns.Message);

                await esClient.IndexDocumentAsync<Hotel>(hotel);
               
            }

           
        }
    }

     
}
