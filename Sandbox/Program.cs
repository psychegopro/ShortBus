﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShortBus.Persistence;
using ShortBus;
using System.Threading;
using System.Collections.Concurrent;
using System.Net;
using System.IO;
using ShortBus.Default;
using System.Transactions;

namespace Sandbox {



    public class txTest : System.Transactions.IEnlistmentNotification {

        public txTest() { }

        public void SaveChanges() {

            Transaction currentTx = Transaction.Current;
            if (currentTx != null) {
                Console.WriteLine("Enlisting");
                currentTx.EnlistVolatile(this, EnlistmentOptions.None);
            }

            Console.Write("Saving");
        }

        void IEnlistmentNotification.Commit(Enlistment enlistment) {
            Console.WriteLine("committed");
            enlistment.Done();
        }

        void IEnlistmentNotification.InDoubt(Enlistment enlistment) {
            Console.WriteLine("In Doubt");
            enlistment.Done();
        }

        void IEnlistmentNotification.Prepare(PreparingEnlistment preparingEnlistment) {
            
            Console.WriteLine("Preparing");
            preparingEnlistment.Prepared();
            
        

        }

        void IEnlistmentNotification.Rollback(Enlistment enlistment) {
            Console.WriteLine("rolling back");
            enlistment.Done();
        }
    }



    class Program {


        static void Main(string[] args) {



            //TestSerialization();
            TestPublish();
            //TestIPGet();
            //TestMultiThread();
            //TestTx();
            //TestSerialization();

            //for (int i = 0; i < 1000; i++) {
            //    Console.WriteLine(i);
            //    Task t = TestWebPublisher();
            //    t.Wait();
            //}


            //TestPersistConfig();

            Console.ReadKey();

        }

        private static void TestIPGet() {
            Console.WriteLine("IP: {0}", ShortBus.Util.Util.GetLocalIP());
        }

        private static void TestTx() {
            txTest t = new txTest();
            using (System.Transactions.TransactionScope scope = new TransactionScope(TransactionScopeOption.Required)) {

                
                t.SaveChanges();
                scope.Complete();

                
            }
        }

        //private static void TestPersistConfig() {

        //    IPersist s = new MongoPersist(new MongoPersistSettings() { ConnectionString = @"mongodb://127.0.0.1:27017", DB = "SandBox", Collection = "publish" });
        //    bool result = s.DBExists;
        //    result = s.CollectionExists;
        //}

        private static async Task TestWebPublisher() {
            

            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(new Uri(@"http://localhost/publisher/message/helloworld"));
                //req.ContentType = @"application/x-www-form-urlencoded";
                req.ContentType = "Application/Json";
                req.Method = "Get";


            //string postData = string.Format("&xmls={0}", request);
            //req.ContentLength = postData.Length;

            //Task<Stream> reqTask = req.GetRequestStreamAsync();
            //reqTask.Wait();

            //using (Stream reqStream = await req.GetRequestStreamAsync())
            //using (StreamWriter sw = new StreamWriter(reqStream, System.Text.Encoding.ASCII))
            //{
            //    sw.Write(postData);

            //    sw.Flush();
            //    sw.Close();
            //    reqStream.Close();

            //    this.FileLogger.SaveFile(postData, "LastGetTokenRequest.xml");
            //    DonlenTrace.Trace(System.Diagnostics.TraceEventType.Information, DonlenTrace.TraceLogType.GetTokenRequest, null, "Request New Token");


            //}
               
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                string responseText = null;
                //Task<WebResponse> resTask = req.GetResponseAsync();
                //resTask.Wait();
                // WebResponse response = req.GetResponse();
                //using (WebResponse response = resTask.Result)
                using (WebResponse response = await req.GetResponseAsync())
                using (Stream resStream = response.GetResponseStream())
                using (StreamReader rdr = new StreamReader(resStream))
                {
                    responseText = rdr.ReadToEnd();

          
                    rdr.Close();
                    response.Close();
                }

                Console.WriteLine(responseText);

        }



        private static ConcurrentQueue<int> messages = null;
        private static int threadId = 1;
        private static object lockObj = new object();
        private static ManualResetEvent mre = new ManualResetEvent(false);
        private static int numOfThreads = 8;
        private static int? SyncGet() {

            int? toReturn = null;
            lock(lockObj) {
                int fromQueue = 0;
                bool dequeued = messages.TryDequeue(out fromQueue);
               
                if (dequeued) {
                    Console.WriteLine("Item Dequeued: {0} - {1} left", dequeued, messages.Count());
                    toReturn = fromQueue;
                } else {
                    Console.WriteLine("nohting left");
                }
            }
            return toReturn;
        }

        private static void TestMultiThread() {
            
            

            messages = new ConcurrentQueue<int>();
            Enumerable.Range(0,1000).ToList().ForEach(i => {
                messages.Enqueue(i);
            });

            for (int i = 1; i <= numOfThreads; i++) {

                Thread t = new Thread(Program.SendMessage);
                t.Name = "Thread_" + i.ToString();
                t.Start(i);
                
            }

            mre.Set();  

            bool unload = false;
            bool c = true;
            while (c) { 
                
                string fromUser = Console.ReadLine();
                if (fromUser.Equals("x", StringComparison.OrdinalIgnoreCase)) {
                    unload = true;
                    c = false;
                } else {

                    for (int i = 0; i < int.Parse(fromUser); i++) {
                        messages.Enqueue(i);
                    }

                    mre.Set();

                }
            }

            if (unload) {
                AppDomain.Unload(AppDomain.CurrentDomain);
            }


        }

        private static void SendMessage(object data) {

            int myThreadId = (int)data;
            int r = new Random(myThreadId).Next(2000);
            //Console.WriteLine("Thread {0} Waiting", myThreadId);
            while (mre.WaitOne()) {
                mre.Reset();
                //Console.WriteLine("Thread {0} going", myThreadId);
                int newThreadId = myThreadId + 1;
                if (newThreadId == (numOfThreads + 1)) { newThreadId = 1; }

                //if current thread is mine, proceed and set to next thread
                if (myThreadId == Interlocked.CompareExchange(ref threadId, myThreadId, newThreadId)) {

                    int? queued = SyncGet();
                    
                    //if we found a message, reset so next thread can look
                    //otherwise, don't reset... this will effectively stop all threads until soemthing else
                    //notifies that there is something to process.
                    if (queued.HasValue) {
                        //we have our value, so pass on to next thread
                        mre.Set();
                        
                        //simulate work
                        //Thread.Sleep();

                        Console.WriteLine("{0} processed queue item {1}", myThreadId, queued.Value);

                    }
                } else {
                    //err, not my turn, pass on to next.
                    mre.Set();
                }
            }


        }


        private static System.Timers.Timer messageSendTimer = new System.Timers.Timer();
        private static int counter = 0;

        private static void TestPublish() {



            //            new MongoPersist(new MongoPersistSettings() {
            //                Collection = "source", ConnectionString = @"mongodb://127.0.0.1:27017", DB = Bus.ApplicationName
            //            })
            //                 , new MongoPersist(new MongoPersistSettings() {
            //    Collection = "config", ConnectionString = @"mongodb://127.0.0.1:27017", DB = Bus.ApplicationName
            //}))


            Bus.Configure
             .PersistTo(new MongoPersistProvider(@"mongodb://127.0.0.1:27017", MongoDataBaseName.UseExisting("ShortBus")))
             .MaxThreads(1)
             
             .MyEndPoint(new ShortBus.Configuration.EndPoint() {
                 EndPointAddress = @"http://localhost:9872"
                 , EndPointType = ShortBus.Publish.EndPointTypeOptions.Source
                 , Name = Bus.ApplicationName
             })

             .RegisterEndpoint("Default", new RESTEndPoint(new RESTSettings(@"http://localhost:9873", ShortBus.Publish.EndPointTypeOptions.Agent)))
             .RouteMessage<ShortBus.TestMessage>("Default", false);

            Bus.OnStarted += onStarted;
            Bus.OnProcessing += onProcessing;
            Bus.OnStalled += onStalled;
            Bus.OnThreadStarted += onThreadStarted;
            Bus.Start();
            //Bus.SendMessageDirect<TestMessage>(new TestMessage() { Property = string.Format("Thread {0}, Iteration {1}", 0, 0) });

            for (int i = 1; i <= 4; i++) {

                Task t = null;
                if (t == null || t.IsCompleted) {
                    //t = new Thread(PublishNext);

                    //capture i during the loop, since publishnext is running from the facotry, it may begin
                    //after i has been incremeneted.
                    int q = i;

                    t = Task.Run(() => {
                        for (int g = 1; g <= 5; g++) {
                            Bus.SendMessage<TestMessage>(new TestMessage() { Property = string.Format("Thread {0}, Iteration {1}", q, g) });
                        }
                        Console.WriteLine("Done {0}", q);
                    });


                }



            }




                //messageSendTimer.AutoReset = false;
                //messageSendTimer.Enabled = true;
                //messageSendTimer.Interval = 1000;
                //messageSendTimer.Elapsed += MessageSendTimer_Elapsed;
                //messageSendTimer.Start();

                Console.WriteLine("Press any key to stop: ");
            Console.ReadKey();


        }

        private static void MessageSendTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            for (int z = 0; z < 20; z++) {
                using (TransactionScope scope = new TransactionScope(TransactionScopeOption.Required)) {



                    Bus.SendMessage<ShortBus.TestMessage>(new TestMessage() { Property = counter.ToString() });
                    counter += 1;
                    scope.Complete();

                }
            }


        }

        private static void onThreadStarted(object sender, EventArgs args) {
            Console.WriteLine("thread Started");
        }

        private static void onStalled(object sender, EventArgs args) {
            Console.WriteLine("Stalled");
        }

        private static void onProcessing(object sender, EventArgs args) {
            Console.WriteLine("Processing");
        }

        static void onStarted(object caller, EventArgs args) {
            Console.WriteLine("Bus Started");
        }


        static void TestSerialization() {
            TestMessage a = new TestMessage() {
                Property = "hello"
            };
            var settings = new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All };
            string serialized = JsonConvert.SerializeObject(a, settings);

            
            

            JObject jo = JObject.Parse(serialized);
            Console.WriteLine(jo["$type"]);

            Type ofJo = Type.GetType(jo["$type"].ToString());
            var deserialized = JsonConvert.DeserializeObject(serialized, ofJo);

            
            

            Console.WriteLine(serialized);

        }
    }
}
