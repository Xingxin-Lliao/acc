using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using System.Text;

namespace acc;

internal class ModuleBackgroundService : BackgroundService
    {
        private ModuleClient? _moduleClient;
        private readonly ILogger<ModuleBackgroundService> _logger;

        public ModuleBackgroundService(ILogger<ModuleBackgroundService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // 设置 MQTT TCP 传输方式
            var mqttSettings = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSettings };

            // 初始化 ModuleClient
            _moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            _moduleClient.SetConnectionStatusChangesHandler((status, reason) =>
                _logger.LogWarning("Connection changed: Status: {status} Reason: {reason}", status, reason));

            await _moduleClient.OpenAsync(cancellationToken);
            _logger.LogInformation("IoT Hub module client initialized.");

            var rnd = new Random();

            // 主循环 - 模拟加速度数据
            while (!cancellationToken.IsCancellationRequested)
            {
                double x = Math.Round(rnd.NextDouble() * 2 - 1, 3);               // -1g ~ 1g
                double y = Math.Round(rnd.NextDouble() * 2 - 1, 3);
                double z = Math.Round(9.8 + (rnd.NextDouble() * 0.2 - 0.1), 3);   // 模拟 z 方向重力抖动

                var messageBody = $@"{{
    ""acceleration"": {{
        ""x"": {x},
        ""y"": {y},
        ""z"": {z}
    }},
    ""timestamp"": ""{DateTime.UtcNow:o}""
}}";

                var message = new Message(Encoding.UTF8.GetBytes(messageBody));

                await _moduleClient.SendEventAsync("output1", message, cancellationToken);

                _logger.LogInformation("Sent accelerometer message: {messageBody}", messageBody);

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

