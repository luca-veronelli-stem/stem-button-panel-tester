namespace Core.Interfaces.Data
{
    public interface IProtocolRepository
    {
        ushort GetCommand(string commandName);
        ushort GetVariable(string variableName);
        byte[] GetValue(string valueName);
    }
}
