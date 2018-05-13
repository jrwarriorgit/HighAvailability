using Common;
using Configuration;
using Domain;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AddToCosmos
{
    class Program
    {
        static void Main(string[] args)
        {
            var sourceQueue = QueueClient.CreateFromConnectionString(InternalConfiguration.QueueConnectionString, "04ToCosmosDb");
            DocumentDBRepository<Cfdi>.Initialize();
            
            var count = 0;
            do
            {
                try
                {
                    var files = sourceQueue.ReceiveBatch(1000);
                    count = files.Count();
                    Console.WriteLine(count);
                    if (count > 0)
                    {
                        Parallel.ForEach(files, (currentFile) =>
                        {
                            try
                            {
                                Cfdi cfdi = currentFile.GetBody<Cfdi>();
                                
                                DocumentDBRepository<Cfdi>.CreateItemAsync(cfdi).GetAwaiter().GetResult();
                                currentFile.Complete();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex);
                                currentFile.Abandon();
                            }
                        });



                    }

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
