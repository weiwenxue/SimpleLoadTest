using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SimpleLoadTest
{
    class Program
    {
        static readonly string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.198 Safari/537.36";
        private static HttpClientHandler clientHandler = new HttpClientHandler();

        private static string target_url = "";
        private static int run_duration_seconds = 0;
        private static int concurrent_requests = 0;
        private static string test_summary = "";

        private static string test_info_file_path;

        static void Main(string[] args)
        {
            if (args.Length == 0 || ParseInput(args) == false)
            {
                GetHelp();
                return;
            }

            WaitHandle[] waitHandles = new WaitHandle[concurrent_requests];
            Logger.Init("Time, Thread ID, Request #, HttpStatusCode, Response Time (ms), Response Size, Exception");
            test_info_file_path = Logger.log_file_path.Replace(".csv", "_test_info.txt");
            Thread.Sleep(500);
            WriteTestInfo();

            for (int i = 0; i < concurrent_requests; i++)
            {
                var handle = new EventWaitHandle(false, EventResetMode.ManualReset);
                Thread thread = new Thread(() => { RunTest(i, run_duration_seconds, target_url); handle.Set(); });
                waitHandles[i] = handle;
                thread.Start();
                Console.WriteLine($"Concurrent Test ID: {i} running...");
                Thread.Sleep(5);
            }

            Console.WriteLine("Run duration countdown (%): ");
            for (int i = run_duration_seconds; i >= 0 ; i--)
            {
                Console.Write("\r{0}%   ", i * 100 / run_duration_seconds);
                Thread.Sleep(1000);
            }
            Console.WriteLine();
            WaitHandle.WaitAll(waitHandles);
            WriteToFile($"{DateTime.Now} Load test completed");
            Console.WriteLine($"Test summary file:");
            Console.WriteLine($"{test_info_file_path}");
            Console.WriteLine($"Test result file:");
            Console.WriteLine($"{Logger.log_file_path}"); 
            Thread.Sleep(3000);
            Console.WriteLine("Exit...");
        }

        static void RunTest(int id, int runTimeSeconds, string url)
        {
            int count = 1;
            DateTime endTime = DateTime.Now.AddSeconds(runTimeSeconds);
            while (DateTime.Now < endTime)
            {
                List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>();
                headers.Add(new KeyValuePair<string, string>("user-agent", UserAgent));
                DateTime start = DateTime.Now;
                HttpStatusCode status = HttpStatusCode.UnprocessableEntity;
                string response = "";
                string exception = "OK";
                try
                {
                    status = Get(url, out response, headers);
                }
                catch (Exception ex)
                {
                    exception = ex.Message;
                }
                TimeSpan latency = DateTime.Now - start;
                Logger.Log($"{id}, {count++}, {status}, {latency.TotalMilliseconds}, {response.Length}, {exception}");
            }
        }

        private static HttpStatusCode Get(string url, out string response, List<KeyValuePair<string, string>> headers = null)
        {
            HttpStatusCode status = HttpStatusCode.BadRequest;
            response = null;
            using (HttpClient client = new HttpClient(clientHandler, false))
            {
                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> kv in headers)
                    {
                        client.DefaultRequestHeaders.Add(kv.Key, kv.Value);
                    }
                }

                HttpResponseMessage respMsg = client.GetAsync(url).Result;
                response = respMsg.Content.ReadAsStringAsync().Result;
                status = respMsg.StatusCode;
            }
            return status;
        }

        private static void GetHelp()
        {
            Console.WriteLine("Usage: ");
            Console.WriteLine("--target-url <https://the.url.to.test>");
            Console.WriteLine("--run-duration-seconds <seconds to run>");
            Console.WriteLine("--concurrent-requests <how many concurrent tests to run>");
            Console.WriteLine("--test-summary <OPTIONAL, description of this test.>");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("--target-url https://www.google.com --run-duration-seconds 10 --concurrent-requests 5 --test-summary \"test google with 5 requests for 10s\"");
            Console.WriteLine();
            Console.WriteLine("NOTES: This tool sends concurrent HTTP requests to the target URL from the same IP address.  If the target system throttles on IP address, the test will be limited.");
        }

        private static bool ParseInput(string[] args)
        {
            if (args.Length < 6)
            {
                Console.WriteLine("Not sufficient inputs.");
                return false;
            }
            int count = 0;
            while (count < args.Length)
            {
                switch (args[count])
                {
                    case "--help":
                        return false;
                    case "--target-url":
                        target_url = args[++count];
                        Regex regex = new Regex("^(ht|f)tp(s?)\\:\\/\\/[0-9a-zA-Z]([-.\\w]*[0-9a-zA-Z])*(:(0-9)*)*(\\/?)([a-zA-Z0-9\\-\\.\\?\\,\\'\\/\\\\\\+&amp;%\\$#_]*)?$");
                        if (!regex.IsMatch(target_url))
                        {
                            Console.WriteLine($"Invalid input: {target_url}");
                            return false;
                        }
                        break;
                    case "--run-duration-seconds":
                        try
                        {
                            run_duration_seconds = int.Parse(args[++count]);
                            if (run_duration_seconds < 1)
                            {
                                Console.WriteLine("Input [--run-duration-seconds] cannot be smaller than 1.");
                                return false;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Invalid input: {args[count]}");
                            return false;
                        }
                        break;
                    case "--concurrent-requests":
                        try
                        {
                            concurrent_requests = int.Parse(args[++count]);
                            if (concurrent_requests < 1)
                            {
                                Console.WriteLine("Input [--concurrent-requests] cannot be smaller than 1.");
                                return false;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Invalid input: {args[count]}");
                            return false;
                        }
                        break;
                    case "--test-summary":
                        test_summary = args[++count];
                        if (test_summary.Length > 1024)
                        {
                            Console.WriteLine("--test-summary is too long.");
                            return false;
                        }
                        break;
                    default:
                        Console.WriteLine($"Unknown inputs: {args[count]}");
                        return false;
                }
                count++;
            }
            if (target_url.Length == 0 || run_duration_seconds == 0 || concurrent_requests == 0)
            {
                Console.WriteLine("Not sufficient inputs.");
                return false;
            }
            return true;
        }

        private static void WriteTestInfo()
        {
            WriteToFile($"Test result file:");
            WriteToFile($"{Logger.log_file_path}");
            WriteToFile($"Test summary: {test_summary}");
            WriteToFile($"{DateTime.Now} Load test starting...");
            WriteToFile($"Target URL: {target_url}");
            WriteToFile($"Run duration (s): {run_duration_seconds}");
            WriteToFile($"Concurrent requests: {concurrent_requests}");
        }

        private static void WriteToFile(string content)
        {
            StreamWriter sw = new StreamWriter(test_info_file_path, true);
            sw.WriteLine(content);
            sw.Close();
            Console.WriteLine(content);
        }
    }
}
