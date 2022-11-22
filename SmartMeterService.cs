using Microsoft.Extensions.Hosting;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SmartMeterToMqtt
{
    public class SmartMeterService : IHostedService
    {
        private static IManagedMqttClient _mqttClient;
        private static Settings _settings;
        private readonly IApplicationLifetime _applicationLifetime;

        public SmartMeterService(IApplicationLifetime applicationLifetime)
        {
            _applicationLifetime = applicationLifetime;
        }

        public async Task StartReading()
        {
            try
            {
                var messageBuilder = new MqttClientOptionsBuilder()
               .WithTcpServer(_settings.MqttIp, 1883)
               .WithCleanSession();

                var options = messageBuilder.Build();

                var managedOptions = new ManagedMqttClientOptionsBuilder()
                  .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                  .WithClientOptions(options)
                  .Build();

                _mqttClient = new MqttFactory().CreateManagedMqttClient();
                await _mqttClient.StartAsync(managedOptions);

                Console.WriteLine("Connected to Mqtt broker");

                SerialPort listenerPort = new SerialPort(_settings.ComPort)
                {
                    BaudRate = _settings.BaudRate,
                    Parity = (Parity)Enum.Parse(typeof(Parity), _settings.Parity),
                    StopBits = StopBits.One,
                    DataBits = _settings.DataBits,
                    ReadTimeout = _settings.ReadTimeout,
                    ReceivedBytesThreshold = _settings.ReceivedBytesThreshold,
                };

                if (!listenerPort.IsOpen)
                {
                    listenerPort.Open();
                }

                listenerPort.DataReceived += async (a, b) => await DataReceivedHandler(a, b);
                Console.WriteLine($"Started listening on port {_settings.ComPort}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                _applicationLifetime.StopApplication();
            }
        }

        private static async Task DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort sp = (SerialPort)sender;

                string indata = sp.ReadExisting();

                indata = Regex.Replace(indata, @"\r\n?|\n", "");

                //Console.WriteLine("----------------------------------------");
                //Console.WriteLine(indata);
                await HandleEnergyReading(indata);

            }
            catch (Exception ex)
            {
                Console.WriteLine("DataReceivedHandler ex: " + ex.Message);
                //throw;
            }
        }

        private static async Task HandleEnergyReading(string command)
        {
            //I     T1  1.8.1   stroom daltarief
            //II    T2  1.8.2   stroom normaaltarief
            //III   T3  2.8.1   teruglevering tijdens daltariefuren
            //IV    T4  2.8.2   teruglevering tijdens normaaltariefuren

            string content = null;
            try
            {
                decimal? powerUsedLowReading = GetSubstring(command, "1.8.1(", "*");
                decimal? powerUsedHighReading = GetSubstring(command, "1.8.2(", "*");

                decimal? powerBackLowReading = GetSubstring(command, "2.8.1(", "*");
                decimal? powerBackHighReading = GetSubstring(command, "2.8.2(", "*");

                decimal? powerCurrentReading = GetSubstring(command, "1-0:1.7.0(", "*") * 1000;

                decimal? powerUsedPhase1 = GetSubstring(command, "21.7.0(", "*") * 1000;
                decimal? powerUsedPhase2 = GetSubstring(command, "41.7.0(", "*") * 1000;
                decimal? powerUsedPhase3 = GetSubstring(command, "61.7.0(", "*") * 1000;

                decimal? currentUsedPhase1 = GetSubstring(command, "31.7.0(", "*");
                decimal? currentUsedPhase2 = GetSubstring(command, "51.7.0(", "*");
                decimal? currentUsedPhase3 = GetSubstring(command, "71.7.0(", "*");

                decimal? voltageP1 = GetSubstring(command, "32.7.0(", "*");
                decimal? voltageP2 = GetSubstring(command, "52.7.0(", "*");
                decimal? voltageP3 = GetSubstring(command, "72.7.0(", "*");

                int gasEndIndex = command.IndexOf("*m3");

                decimal? gasReading = null;
                if (gasEndIndex != -1)
                {
                    gasReading = decimal.Parse(command.Substring(gasEndIndex - 9, 9));

                }

                //send MQTT
                content = Newtonsoft.Json.JsonConvert.SerializeObject(new EnergyReading
                {
                    PowerUsedHigh = powerUsedHighReading,
                    PowerUsedLow = powerUsedLowReading,
                    PowerBackHigh = powerBackHighReading,
                    PowerBackLow = powerBackLowReading,
                    PowerCurrent = powerCurrentReading,
                    GasReading = gasReading,
                    PowerUsedPhase1 = powerUsedPhase1,
                    PowerUsedPhase2 = powerUsedPhase2,
                    PowerUsedPhase3 = powerUsedPhase3,
                    CurrentUsedPhase1 = currentUsedPhase1,
                    CurrentUsedPhase2 = currentUsedPhase2,
                    CurrentUsedPhase3 = currentUsedPhase3,
                    VoltagePhase1 = voltageP1,
                    VoltagePhase2 = voltageP2,
                    VoltagePhase3 = voltageP3
                }, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

            }
            catch (Exception e)
            {
                Console.WriteLine("HandleEnergyReading ex: " + e.Message + " " + e.StackTrace);
            }

            if (content == null)
            {
                return;
            }

            var message = new ManagedMqttApplicationMessage()
            {
                ApplicationMessage = new MQTTnet.MqttApplicationMessage()
                {
                    Payload = Encoding.UTF8.GetBytes(content),
                    Topic = _settings.PublishTopic
                }
            };
            await _mqttClient.PublishAsync(message);

        }

        private static decimal? GetSubstring(string info, string start, string end)
        {
            int startIndex = info.IndexOf(start);
            if (startIndex == -1)
            {
                return null;
            }
            int endIndex = info.IndexOf(end, startIndex + start.Length);
            if (endIndex == -1)
            {
                return null;
            }

            return decimal.Parse(info.Substring(startIndex, (endIndex - startIndex)).Replace(start, ""));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            //string reading = "/ISK5\\2M550E-1012  1-3:0.2.8(50) 0-0:1.0.0(200619210451S) 0-0:96.1.1(4530303439303037343837333732303139) 1-0:1.8.1(000016.462*kWh) 1-0:1.8.2(000016.379*kWh) 1-0:2.8.1(000000.000*kWh) 1-0:2.8.2(000000.000*kWh) 0-0:96.14.0(0002) 1-0:1.7.0(00.653*kW) 1-0:2.7.0(00.000*kW) 0-0:96.7.21(00010) 0-0:96.7.9(00003) 1-0:99.97.0(2)(0-0:96.7.19)(190920144206S)(0000000586*s)(190925201547S)(0000452011*s) 1-0:32.32.0(00005) 1-0:32.36.0(00001) 0-0:96.13.0() 1-0:32.7.0(237.9*V) 1-0:31.7.0(003*A) 1-0:21.7.0(00.655*kW) 1-0:22.7.0(00.000*kW) 0-1:24.1.0(003) 0-1:96.1.0(4730303634303032303039373731303230) 0-1:24.2.1(200619210000S)(00001.661*m3) !5788";
            //await HandleEnergyReading(reading);
            //return;

            var environment = Environment.GetEnvironmentVariables();
            _settings = new Settings
            {
                ComPort = (string)environment["ComPort"],
                MqttIp = (string)environment["MqttIp"],
                MqttPort = int.Parse((string)environment["MqttPort"]),
                PublishTopic = (string)environment["PublishTopic"],
                BaudRate = int.Parse((string)environment["BaudRate"]),
                DataBits = int.Parse((string)environment["DataBits"]),
                Parity = (string)environment["Parity"],
                ReadTimeout = int.Parse((string)environment["ReadTimeout"]),
                ReceivedBytesThreshold = int.Parse((string)environment["ReceivedBytesThreshold"]),
            };

            try
            {
                await StartReading();
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to start: " + e.Message);
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
