using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace acc
{
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
            // 使用 MQTT TCP 传输
            var mqttSettings = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSettings };

            _moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            _moduleClient.SetConnectionStatusChangesHandler((status, reason) =>
                _logger.LogWarning("Connection changed: Status: {status} Reason: {reason}", status, reason));

            // 监听 input1 消息
            await _moduleClient.SetInputMessageHandlerAsync("input1", async (message, userContext) =>
            {
                var msgBytes = message.GetBytes();
                var msgString = Encoding.UTF8.GetString(msgBytes);
                _logger.LogInformation("Received message on input1: {msg}", msgString);
                return MessageResponse.Completed;
            }, null);

            await _moduleClient.OpenAsync(cancellationToken);
            _logger.LogInformation("IoT Hub module client initialized.");

            var rnd = new Random();

            // 主循环模拟加速度数据
            while (!cancellationToken.IsCancellationRequested)
            {
                double x = Math.Round(rnd.NextDouble() * 2 - 1, 3);
                double y = Math.Round(rnd.NextDouble() * 2 - 1, 3);
                double z = Math.Round(9.8 + (rnd.NextDouble() * 0.2 - 0.1), 3);

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

    public class Program
    {
        public static async Task Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services => services.AddHostedService<ModuleBackgroundService>())
                .Build();

            await host.RunAsync();
        }
    }
}
