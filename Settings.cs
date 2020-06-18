namespace SmartMeterToMqtt
{
    public struct Settings
    {
        public string PublishTopic;
        public int MqttPort;
        public string MqttIp;
        public string ComPort;

        public int BaudRate;
        public string Parity;
        public int DataBits;
    }
}
