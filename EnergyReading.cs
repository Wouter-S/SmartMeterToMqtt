namespace SmartMeterToMqtt
{
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
