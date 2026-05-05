using Core.Interfaces.Data;
using Core.Interfaces.Services;
using GUI.Windows.Presenters;
using GUI.Windows.Views;

using Microsoft.Extensions.DependencyInjection;

namespace GUI.Windows
{
    public partial class Form1 : Form
    {
        private readonly IServiceProvider _serviceProvider;
        private ButtonPanelTestPresenter? _presenter;

        public Form1(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
            Text = "Stem Button Panel Tester";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1450, 850);
            AutoScroll = true;

            // Load icon from embedded resource bytes
            byte[] iconBytes = Properties.Resources.Ztem;
            if (iconBytes != null && iconBytes.Length > 0)
            {
                using var ms = new MemoryStream(iconBytes);
                Icon = new Icon(ms);
            }

            SetupControls();
        }

        private void SetupControls()
        {
            IButtonPanelTestService service = _serviceProvider.GetRequiredService<IButtonPanelTestService>();
            IProtocolRepositoryFactory repositoryFactory = _serviceProvider.GetRequiredService<IProtocolRepositoryFactory>();

            var view = new ButtonPanelTestUserControl();
            _presenter = new ButtonPanelTestPresenter(view, service, repositoryFactory);

            view.Location = new Point(0, 0);
            Controls.Add(view);
        }
    }
}
