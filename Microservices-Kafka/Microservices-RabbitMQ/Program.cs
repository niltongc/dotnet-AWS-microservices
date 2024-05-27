using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

class Program
{
    private static void Main(string[] args)
    {
        const string url = "amqps://admin:Senha@1231212_b-2aa373e5-773f-4ea3-b6fb-b164128316d8.mq.us-east-1.amazonaws.com:5671";
        const string queueName = "my-test-queue";


        var model = new {Name = "John Doe"};
        var serializedModel = JsonConvert.SerializeObject(model);
        var bytes = Encoding.UTF8.GetBytes(serializedModel);


        Console.Clear();

        var connectionFactory = new RabbitMQ.Client.ConnectionFactory()
        {
            Uri = new Uri(url)
        };

        var connection = connectionFactory.CreateConnection();
        var channel = connection.CreateModel();
        channel.QueueDeclare(queueName, false, true, false, null);
        channel.BasicPublish("",queueName, false, null, bytes);

        var consumer = new RabbitMQ.Client.Events.EventingBasicConsumer(channel);

        consumer.Received += (sender, e) =>
        {
            var stringMessage = Encoding.UTF8.GetString(e.Body.ToArray());
            Console.WriteLine(stringMessage);
        };

        channel.BasicConsume(queueName, true, "", true, true, null, consumer);
        Console.WriteLine("Hello, World!");
    }
}