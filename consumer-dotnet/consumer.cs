using Confluent.Kafka;
using System;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.DataModel;


namespace KafkaConsumer {

    [DynamoDBTable("Prediction")]
    class Prediction
    {
        public string ClientId {get; set;}
        public Dictionary<string, double> Classes {get; set;}

    }
    

    class Dynamo {
        
        Table table;
        
        DynamoDBContext context;

        AmazonDynamoDBClient client;

        public Dynamo(AmazonDynamoDBClient client) {
            
            context = new DynamoDBContext(client);

        }

        // private Document CreateDocument(string content) {
            
        //     var prediction = JsonSerializer.Deserialize<Prediction>(content);

        //     var doc = new Document();
        //     doc["ClientId"] = prediction.ClientId;
    
        //     foreach (var item in prediction.Classes)
        //     {
        //         doc[item.Key] = item.Value;
        //     }

        //     return doc;
        // }

        // public void SaveMessage(string content) {
        //     table.PutItemAsync(CreateDocument(content));
        // }

        public void SaveMessageBatch(LinkedList<string> content) {
             
            var batch = context.CreateBatchWrite<Prediction>();
            
            batch.AddPutItems(
                content
                .Select(item => JsonSerializer.Deserialize<Prediction>(item)));

            batch.ExecuteAsync();
        }
    }
    class Consumer {

        static void SaveMessage(Table table, string content) 
        {
            var prediction = JsonSerializer.Deserialize<Prediction>(content);

            var doc = new Document();
            doc["client_id"] = prediction.ClientId;
    
            foreach (var item in prediction.Classes)
            {
                doc[item.Key] = item.Value;
            }

            table.PutItemAsync(doc);
            Console.WriteLine("Message Saved: " + doc["client_id"]);
        }
        
        static void Main(string[] args)
        {
            if (args.Length != 1) {
                Console.WriteLine("Please provide the configuration file path as a command line argument");
            }

            IConfiguration configuration = new ConfigurationBuilder()
                .AddIniFile(args[0])
                .Build();

            configuration["group.id"] = "kafka-dotnet-getting-started";
            configuration["auto.offset.reset"] = "earliest";
            configuration["enable.auto.commit"] = "false";
	        configuration["max.poll.interval.ms"] = "3600000";

            const string topic = "predictions";

            CancellationTokenSource cts = new CancellationTokenSource();
            
            Console.CancelKeyPress += (_, e) => {
                e.Cancel = true; // prevent the process from terminating.
                cts.Cancel();
            };

            AmazonDynamoDBConfig clientConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = "http://localhost:8000"
            };

            using var consumer = new ConsumerBuilder<string, string>(
                configuration.AsEnumerable()).Build();

            consumer.Subscribe(topic);
            
            var tasks = new LinkedList<Task>();
            var messages = new LinkedList<string>();

            try
            {
                while (true)
                {
                    var cr = consumer.Consume(1_000);
                    var run = false;

                    if (cr != null)
                        // tasks.AddLast(new Task(() => dynamo.SaveMessage(cr.Message.Value)));
                        messages.AddLast(cr.Message.Value);
                    else
                        run = true;
                    
                    run = run || messages.Count >= 10_000;

                    if (run) {
                        
                        if (messages.Count == 0) continue;

                        var dynamo = new Dynamo(new AmazonDynamoDBClient(clientConfig));

                        var start = DateTime.Now.ToString("O");
                        Console.WriteLine($"""Entrou ({start})": {messages.Count})""");

                        dynamo.SaveMessageBatch(messages);
                        messages.Clear();

                        var end = DateTime.Now.ToString("O");
                        Console.WriteLine($"""Terminou ({end})""");

                        // var tasksToRun = tasks.ToArray();
                        // tasks = new LinkedList<Task>();

                        // foreach(Task task in tasksToRun){task.Start();};
                        // Task.WaitAll(tasksToRun);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Ctrl-C was pressed.
            }
            finally
            {
                consumer.Close();
            }
        }
    }   
}
