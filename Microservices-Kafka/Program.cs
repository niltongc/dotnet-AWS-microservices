using System.Text.Json.Serialization;
using Confluent.Kafka;
using Confluent.Kafka.Admin;


public class Program
{
    public class UserCreated
    {
        public string Name { get; set; }
    }

    public static void Main(string[] args)
    {
        const string server = "127.0.0.1:9092";
        const string topicName = "kafka-test";

        var adminClient = new AdminClientBuilder(
            new AdminClientConfig
            {
                BootstrapServers = server,
                SecurityProtocol = SecurityProtocol.Plaintext
            }
        ).Build();

        var metaData = adminClient.GetMetadata(TimeSpan.FromMinutes(1));

        if (metaData.Topics.All(x => x.Topic != topicName))
        {
            adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification
                {
                    Name = topicName,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                }
            }).Wait();
        }

        // var producer = new ProducerBuilder<int, String>(new ProducerConfig
        // {
        //     BootstrapServers = server,
        //     SecurityProtocol = SecurityProtocol.Plaintext
        // })
        // .Build();

        // var myEvent = new UserCreated
        // {
        //     Name = "John Doe"
        // };

        // var serializeObject = JsonConvert.SerializeObject(myEvent);

        // producer.Produce(topicName, new Message<int, string>
        // {
        //     Key = 100,
        //     Value = serializeObject
        // });


        var consumer = new ConsumerBuilder<int, String>(new ConsumerConfig
        {
            BootstrapServers = server,
            SecurityProtocol = SecurityProtocol.Plaintext,
            GroupId = "Group"
        }).Build();

        consumer.Subscribe(topicName);
        var result = consumer.Consume(TimeSpan.FromSeconds(30));
        
    }
}




