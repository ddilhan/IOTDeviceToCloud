using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using Microsoft.Azure.Devices.Client;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;

namespace DeviceToCloud
{
    public class Program
    {
        private static IConfiguration _config;
        private static string _connectionString;
        private static DeviceClient _deviceClient;
        private static readonly TransportType s_transportType = TransportType.Mqtt;
        private static int _estimatedDuration;
        private static int _telemetryInterval;

        async static Task Main(string[] args)
        {
            Console.WriteLine("IoT Hub - Simulated device.");

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsetting.json", optional: false);

            _config = builder.Build();

            _connectionString = _config.GetSection("IOTHubConnectionString").Value;
            _estimatedDuration = Convert.ToInt32(_config.GetSection("ExecutionDurationInSeconds").Value);
            _telemetryInterval = Convert.ToInt32(_config.GetSection("ExecutionIntervalInSeconds").Value);

            //This sample accepts the device connection string as a parameter, if present
            ValidateConnectionString(_connectionString);

            // Connect to the IoT hub using the MQTT protocol
            _deviceClient = DeviceClient.CreateFromConnectionString(_connectionString, s_transportType);

            // Run the telemetry loop
            await SendDeviceToCloudMessagesAsync();
        }

        private static void ValidateConnectionString(string connectionString)
        {
            if (connectionString != null)
            {
                try
                {
                    var cs = IotHubConnectionStringBuilder.Create(connectionString);
                    _connectionString = cs.ToString();
                }
                catch (Exception)
                {
                    Console.WriteLine($"Error: Cannot recognize as connection string.");
                    Environment.Exit(1);
                }
            }
            else
            {
                try
                {
                }
                catch (Exception)
                {
                    Console.WriteLine("This sample needs a device connection string to run. Program.cs can be edited to specify it, or it can be included on the appsetting.json.");
                    Environment.Exit(1);
                }
            }
        }
        // Async method to send simulated telemetry
        private static async Task SendDeviceToCloudMessagesAsync()
        {
            // Initial telemetry values
            double minTemperature = 20;
            double minHumidity = 60;
            var rand = new Random();
            var currentDuration = 0;

            while (_estimatedDuration != currentDuration) 
            {
                double currentTemperature = minTemperature + rand.NextDouble() * 15;
                double currentHumidity = minHumidity + rand.NextDouble() * 20;

                // Create JSON message
                string messageBody = JsonSerializer.Serialize(
                    new
                    {
                        temperature = currentTemperature,
                        humidity = currentHumidity,
                    });
                using var message = new Message(Encoding.ASCII.GetBytes(messageBody))
                {
                    ContentType = "application/json",
                    ContentEncoding = "utf-8",
                };

                // Add a custom application property to the message.
                // An IoT hub can filter on these properties without access to the message body.
                message.Properties.Add("temperatureAlert", (currentTemperature > 30) ? "true" : "false");

                // Send the telemetry message
                await _deviceClient.SendEventAsync(message);

                Console.WriteLine($"Iteration: {currentDuration/1000}");
                Console.WriteLine($"{DateTime.Now} > Sending message: {messageBody}");
                currentDuration += 1000;
                try
                {
                    await Task.Delay(_telemetryInterval);
                }
                catch (TaskCanceledException)
                {
                    // User canceled
                    return;
                }
            }
        }
    }
}
