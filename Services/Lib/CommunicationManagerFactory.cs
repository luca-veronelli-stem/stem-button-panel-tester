using Communication;
using Core.Enums;
using Core.Interfaces.Communication;

namespace Services.Lib
{
    /// <summary>
    /// Classe factory per la creazione di istanze di ICommunicationManager in base al canale di comunicazione specificato.
    /// </summary>
    public class CommunicationManagerFactory : ICommunicationManagerFactory
    {
        private readonly Func<CanCommunicationManager> _canManagerFactory;

        public CommunicationManagerFactory(Func<CanCommunicationManager> canManagerFactory)
        {
            _canManagerFactory = canManagerFactory ?? throw new ArgumentNullException(nameof(canManagerFactory));
        }

        // Crea un'istanza di ICommunicationManager in base al canale di comunicazione specificato.
        public ICommunicationManager Create(CommunicationChannel channel)
        {
            return channel switch
            {
                CommunicationChannel.Can => _canManagerFactory(),
                _ => throw new NotSupportedException($"Canale {channel} non supportato.")
            };
        }
    }
}
