namespace OrderBook.Domain.Shared;

public readonly record struct InstrumentCode(Instrument Value)
{
    public static InstrumentCode Vibranium => new(Instrument.VIBRANIUM);
}
