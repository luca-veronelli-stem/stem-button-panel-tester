using Core.Enums;

namespace Core.Interfaces.Communication
{
    public interface ICommunicationManagerFactory
    {
        ICommunicationManager Create(CommunicationChannel channel);
    }
}
