namespace SmartMeterToMqtt
{
    public class EnergyReading
    {
        public decimal? GasReading { get; internal set; }
        public decimal? PowerCurrent { get; internal set; }
        public decimal? PowerUsedHigh { get; internal set; }
        public decimal? PowerUsedLow { get; internal set; }
        public decimal? PowerBackHigh { get; internal set; }
        public decimal? PowerBackLow { get; internal set; }
        public decimal? PowerUsedPhase1 { get; internal set; }
        public decimal? PowerUsedPhase2 { get; internal set; }
        public decimal? PowerUsedPhase3 { get; internal set; }
        public decimal? CurrentUsedPhase1 { get; internal set; }
        public decimal? CurrentUsedPhase2 { get; internal set; }
        public decimal? CurrentUsedPhase3 { get; internal set; }
        public decimal? VoltagePhase1 { get; internal set; }
        public decimal? VoltagePhase2 { get; internal set; }
        public decimal? VoltagePhase3 { get; internal set; }


    }
}
