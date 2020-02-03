using Microsoft.Extensions.Hosting;
using RJCP.IO.Ports;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mqtt;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SmartMeterToMqtt
{
    public class SmartMeterService : IHostedService
    {
        private static IMqttClient client;

        public async Task StartReading(string smartMeterCommPort, string mqttIp, int mqttPort)
        {
            Console.WriteLine($"Connecting to Mqtt: {mqttIp}{mqttPort}, com: {smartMeterCommPort}");
            var configuration = new MqttConfiguration { Port = mqttPort };

            client = await MqttClient.CreateAsync(mqttIp, configuration);
            await client.ConnectAsync();
            client.Disconnected += async (a, b) => await client.ConnectAsync();

            Console.WriteLine("Connected to Mqtt broker");

            SerialPortStream listenerPort = new SerialPortStream(smartMeterCommPort)
            {
                BaudRate = 9600,
                Parity = Parity.Even,
                StopBits = StopBits.One,
                DataBits = 7,
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true,
                Encoding = System.Text.Encoding.ASCII,

                ReceivedBytesThreshold = 100,
            };

            if (!listenerPort.IsOpen)
            {
                listenerPort.Open();
            }
            listenerPort.DataReceived += async (a, b) => await DataReceivedHandler(a, b);
            Console.WriteLine($"Started listening on port {smartMeterCommPort}");
        }

        private static async Task DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPortStream sp = (SerialPortStream)sender;
                string indata = sp.ReadExisting();
                while (!indata.Contains("!"))
                {
                    indata = indata + sp.ReadExisting();
                    await Task.Delay(25);
                }

                indata = Regex.Replace(indata, @"\r\n?|\n", "");
                await HandleEnergyReading(indata);
            }
            catch (Exception ex)
            {
                Console.WriteLine("DataReceivedHandler ex: " + ex.Message);
            }
        }

        private static async Task HandleEnergyReading(string command)
        {
            //I     T1  1.8.1   stroom daltarief
            //II    T2  1.8.2   stroom normaaltarief
            //III   T3  2.8.1   teruglevering tijdens daltariefuren
            //IV    T4  2.8.2   teruglevering tijdens normaaltariefuren

            try
            {
                decimal powerUsedLowReading = GetSubstring(command, "1-0:1.8.1(", "*");
                decimal powerUsedHighReading = GetSubstring(command, "1-0:1.8.2(", "*");

                decimal powerBackLowReading = GetSubstring(command, "1-0:2.8.1(", "*");
                decimal powerBackHighReading = GetSubstring(command, "1-0:2.8.2(", "*");

                decimal powerCurrentReading = GetSubstring(command, "1-0:1.7.0(", "*") * 1000;
                decimal gasReading = GetSubstring(command, "(m3)(", ")");

                //send MQTT
                string content = Newtonsoft.Json.JsonConvert.SerializeObject(new EnergyReading
                {
                    PowerUsedHigh = powerUsedHighReading,
                    PowerUsedLow = powerUsedLowReading,
                    PowerBackHigh = powerBackHighReading,
                    PowerBackLow = powerBackLowReading,
                    PowerCurrent = powerCurrentReading,
                    GasReading = gasReading
                });

                var message = new MqttApplicationMessage("cramer62/sensors/smartmeter", Encoding.UTF8.GetBytes(content));
                await client.PublishAsync(message, MqttQualityOfService.AtMostOnce);
            }
            catch (Exception e)
            {
                Console.WriteLine("HandleEnergyReading ex: " + e.Message + " " + e.StackTrace);
            }
        }

        private static decimal GetSubstring(string info, string start, string end)
        {
            return decimal.Parse(info.Substring(info.IndexOf(start), (info.IndexOf(end, info.IndexOf(start) + start.Length) - info.IndexOf(start))).Replace(start, ""));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var environment = Environment.GetEnvironmentVariables();
            string comPort = (string)environment["ComPort"];
            string mqttIp = (string)environment["MqttIp"];
            int mqttPort = int.Parse((string)environment["MqttPort"]);

            try
            {
                await StartReading(comPort, mqttIp, mqttPort);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to start: " + e.Message);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    public class EnergyReading
    {
        public decimal GasReading { get; internal set; }
        public decimal PowerCurrent { get; internal set; }
        public decimal PowerUsedHigh { get; internal set; }
        public decimal PowerUsedLow { get; internal set; }
        public decimal PowerBackHigh { get; internal set; }
        public decimal PowerBackLow { get; internal set; }
    }
}
