namespace SmartMeterToMqtt
{
    public struct Settings
    {
        public string PublishTopic { get; set; }
        public int MqttPort { get; set; }
        public string MqttIp { get; set; }
        public string ComPort { get; set; }

        public int BaudRate { get; set; }
        public string Parity { get; set; }
        public int DataBits { get; set; }

        public int ReceivedBytesThreshold { get; set; }
        public int ReadTimeout { get; set; }
    }
}
