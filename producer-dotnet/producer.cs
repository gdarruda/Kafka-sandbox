using Confluent.Kafka;
using System;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;


namespace KafkaProducer
{
    class Prediction 
    {
        public string ClientId {get; set;}
        public Dictionary<string, double> Classes {get; set;}
    }

    class Producer 
    {
        static void SendMessage(IProducer<string, string> producer, 
                                int messageId,
                                string topic,
                                int retries = 0) {
            
            if (retries > 2) {
                return;
            }
            
            var rnd = new Random();

            var prediction = new Prediction
                {
                    ClientId = Guid.NewGuid().ToString(),
                    Classes = Enumerable
                              .Range(1, 30)
                              .Select(i => ($"category_{i}", rnd.NextDouble()))
                              .ToDictionary(k => k.Item1, v => v.Item2)
                };

            var predictionJson = JsonSerializer.Serialize(prediction);
            
            try
            {
                producer.Produce(topic, new Message<string, string> {Value = predictionJson},
                (deliveryReport) =>
                {
                    if (deliveryReport.Error.Code != ErrorCode.NoError)
                    {
                        Console.WriteLine($"Failed to deliver message: {deliveryReport.Error.Reason}");
                    }
                    else
                    {
                        Console.WriteLine($"Produced event {messageId}");
                    }
                });

            }
            catch (ProduceException<string, string>)
            {
                Console.WriteLine($"Exponential backoff...");
                Thread.Sleep(Convert.ToInt32(1_000 * Math.Pow(10, retries)));
                SendMessage(producer, messageId, topic, retries+1);
            }
        }

        static void Main(string[] args)
        {

            IConfiguration configuration = new ConfigurationBuilder()
                .AddIniFile(args[0])
                .Build();

            const string topic = "predictions";
            const int numMessages = 100_000;

            using var producer = new ProducerBuilder<string, string>(configuration.AsEnumerable()).Build();

            for (int i = 0; i < numMessages; ++i) {
                var messageId = i;
                // ThreadPool.QueueUserWorkItem((state) => SendMessage(producer, messageId, topic));
                SendMessage(producer, messageId, topic);
            }

            producer.Flush();

        }
    }
}
