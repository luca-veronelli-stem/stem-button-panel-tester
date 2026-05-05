namespace Core.Models.Communication
{
    // Modella un pacchetto ricevuto dal canale CAN
    public sealed record CanPacket(
        uint ArbitrationId,
        bool IsExtended,
        byte[] Data,
        ulong TimestampMicroseconds);
}
