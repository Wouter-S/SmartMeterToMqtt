using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
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
        private static Settings _settings;

        public async Task StartReading()
        {
            Console.WriteLine($"Connecting to Mqtt: {JsonConvert.SerializeObject(_settings)}");
            var configuration = new MqttConfiguration { Port = _settings.MqttPort };

            client = await MqttClient.CreateAsync(_settings.MqttIp, configuration);
            await client.ConnectAsync();
            client.Disconnected += async (a, b) => await client.ConnectAsync();

            Console.WriteLine("Connected to Mqtt broker");

            SerialPortStream listenerPort = new SerialPortStream(_settings.ComPort)
            {
                BaudRate = _settings.BaudRate,
                Parity = (Parity)Enum.Parse(typeof(Parity), _settings.Parity),
                StopBits = StopBits.One,
                DataBits = _settings.DataBits,
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true,
                Encoding = System.Text.Encoding.ASCII,

                ReceivedBytesThreshold = 1,
            };

            if (!listenerPort.IsOpen)
            {
                listenerPort.Open();
            }
            listenerPort.DataReceived += async (a, b) => await DataReceivedHandler(a, b);
            Console.WriteLine($"Started listening on port {_settings.ComPort}");
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
                    await Task.Delay(1);
                }
                Console.WriteLine(indata);

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
                decimal powerUsedLowReading = GetSubstring(command, "1.8.1(", "*");
                decimal powerUsedHighReading = GetSubstring(command, "1.8.2(", "*");

                decimal powerBackLowReading = GetSubstring(command, "2.8.1(", "*");
                decimal powerBackHighReading = GetSubstring(command, "2.8.2(", "*");

                decimal powerCurrentReading = GetSubstring(command, "1.7.0(", "*") * 1000;
                decimal gasReading = 0;
                try
                {
                    gasReading = GetSubstring(command, "(m3)(", ")");
                }
                catch { }
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

                var message = new MqttApplicationMessage(_settings.PublishTopic, Encoding.UTF8.GetBytes(content));
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
            _settings = new Settings
            {
                ComPort = (string)environment["ComPort"],
                MqttIp = (string)environment["MqttIp"],
                MqttPort = int.Parse((string)environment["MqttPort"]),
                PublishTopic = (string)environment["PublishTopic"]
            };

            try
            {
                await StartReading();
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
}
