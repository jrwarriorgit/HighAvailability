using Configuration;
using Domain;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Common;

namespace ValidaRFC
{
    class Program
    {

        private static Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            return ConnectionMultiplexer.Connect(InternalConfiguration.RedisConnectionString);
        });

        public static ConnectionMultiplexer Connection
        {
            get
            {
                return lazyConnection.Value;
            }
        }

        static void Main(string[] args)
        {
            ServicePointManager.DefaultConnectionLimit = 50; //(Or More)
            ThreadPool.SetMinThreads(50, 50);
            var sourceQueue = QueueClient.CreateFromConnectionString(InternalConfiguration.QueueConnectionString, "02ConsumerToValidaRFC");
            var destinationQueue = QueueClient.CreateFromConnectionString(InternalConfiguration.QueueConnectionString, "03ValidaRFCToSigner");

            //DocumentDBRepository<Cfdi>.Initialize();

            IDatabase cache = null;
            if (InternalConfiguration.EnableRedisCache)
            {
                Console.WriteLine("Redis Cache enabled");
                 cache = Connection.GetDatabase();
            }

            var count = 0;
            do
            {
                try
                {
                    Stopwatch swProcess = Stopwatch.StartNew();

                    var files = sourceQueue.ReceiveBatch(1000);
                    count = files.Count();
                    Console.WriteLine(count);
                    if (count > 0)
                    {

                        Parallel.ForEach(files, (currentFile) =>
                        {
                            try
                            {
                                CfdiFile file = currentFile.GetBody<CfdiFile>();
                                var cfdi = new Cfdi();
                                if (file.Storage == "inline")
                                {
                                    cfdi = new Cfdi(file.FileContent,file.Guid);
                                }
                                else
                                {

                                    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(file.Storage);
                                    CloudBlockBlob blob = new CloudBlockBlob(new Uri(file.FileContent), storageAccount.Credentials);

                                    var enableTableStorage = false;
                                    if (enableTableStorage)
                                    {
                                        CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
                                        CloudTable table = tableClient.GetTableReference("cfdi");
                                        TableOperation retrieveOperation = TableOperation.Retrieve<CfdiEntity>("none", file.Guid);
                                        TableResult retrievedResult = table.Execute(retrieveOperation);

                                        if (retrievedResult.Result != null)
                                        {
                                            var xml = ((CfdiEntity)retrievedResult.Result).Xml;
                                            cfdi = new Cfdi(xml,file.Guid);
                                        }
                                    }
                                    else
                                    {

                                        using (var stream = blob.OpenRead())
                                        {
                                            cfdi = new Cfdi(stream,file.Guid);
                                        }

                                    }
                                }
                                if (InternalConfiguration.EnableRedisCache)
                                {
                                    Stopwatch sw = Stopwatch.StartNew();
                                    cfdi.ValidaRfcEmision(cache.StringGet(cfdi.RfcEmisor));
                                    cfdi.ValidaRfcReceptor(cache.StringGet(cfdi.RfcReceptor));
                                    cfdi.ValidationTimeSpend = sw.ElapsedMilliseconds;
                                }
                                //Guarda en Cosmos DB
                                //DocumentDBRepository<Cfdi>.CreateItemAsync(cfdi).GetAwaiter().GetResult();
                                //Fin de guardado en cosmos
                                destinationQueue.Send(new BrokeredMessage(new Tuple<CfdiFile, Cfdi>(file, cfdi))
                                { SessionId = file.Guid });

                                currentFile.Complete();

                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex);
                                currentFile.Abandon();
                            }
                        }
                        );
                    }
                    if (swProcess.ElapsedMilliseconds > 1000) Console.WriteLine($"-> [{count} / {swProcess.ElapsedMilliseconds / 1000}] = {count / (swProcess.ElapsedMilliseconds / 1000)} x segundo");
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                if (count == 0)
                    Thread.Sleep(1000);
            } while (true);
        }

    }
}
