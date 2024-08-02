using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Task_Server
{
    public partial class MainWindow : Window
    {
        private bool isLampOn = false;
        private TcpListener _tcpListener;
        private string currentText = "Default";
        private List<TcpClient> _connectedClients = new List<TcpClient>();

        public MainWindow()
        {
            InitializeComponent();
            StartServer();
        }

        private void LampButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleLamp();
        }

        private async void TextButton_Click(object sender, RoutedEventArgs e)
        {
            var random = new Random();
            int randomNumber = random.Next(0, 100);
            currentText = $"Random number: {randomNumber}";
            TextBox.Text = currentText;

            // Notify clients about the new text
            await NotifyClientsAsync($"TEXT:{currentText}");
        }

        private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            currentText = TextBox.Text;
            // Notify clients about the new text
            _ = NotifyClientsAsync($"TEXT:{currentText}");
        }

        private void ToggleLamp()
        {
            isLampOn = !isLampOn;
            if (isLampOn)
            {
                LampCanvas.Background = Brushes.Yellow;
                LampButton.Content = "LAMP IS ON";
            }
            else
            {
                LampCanvas.Background = Brushes.Gray;
                LampButton.Content = "LAMP IS OFF";
            }

            // Notify clients about the lamp status
            _ = NotifyClientsAsync($"LAMP:{(isLampOn ? "ON" : "OFF")}");
        }

        private async Task NotifyClientsAsync(string message)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            foreach (var client in _connectedClients.ToArray()) // Listeyi kopyalayarak işle
            {
                try
                {
                    var networkStream = client.GetStream();
                    await networkStream.WriteAsync(messageBytes, 0, messageBytes.Length);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error notifying client: {ex.Message}");
                    _connectedClients.Remove(client); // Hatalı istemciyi listeden çıkar
                }
            }
        }


        private async void StartServer()
        {
            _tcpListener = new TcpListener(IPAddress.Any, 5000);
            _tcpListener.Start();

            while (true)
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                _ = HandleClientAsync(tcpClient); // Handle each client connection asynchronously
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            // Listeye ekle
            _connectedClients.Add(client);

            using (var networkStream = client.GetStream())
            {
                byte[] buffer = new byte[1024];
                int bytesRead;

                // Yeni istemci bağlandığında mevcut durumu gönder
                string initialLampStatus = $"LAMP:{(isLampOn ? "ON" : "OFF")}";
                byte[] initialLampStatusBytes = Encoding.UTF8.GetBytes(initialLampStatus);
                await networkStream.WriteAsync(initialLampStatusBytes, 0, initialLampStatusBytes.Length);

                string initialText = $"TEXT:{currentText}";
                byte[] initialTextBytes = Encoding.UTF8.GetBytes(initialText);
                await networkStream.WriteAsync(initialTextBytes, 0, initialTextBytes.Length);

                while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (message == "TOGGLE")
                    {
                        ToggleLamp();
                        string response = isLampOn ? "Lamp is On" : "Lamp is Off";
                        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                        await networkStream.WriteAsync(responseBytes, 0, responseBytes.Length);

                        // Tüm istemcilere lamba durumu gönder
                        await NotifyClientsAsync($"LAMP:{(isLampOn ? "ON" : "OFF")}");
                    }
                    else if (message.StartsWith("TEXT"))
                    {
                        // Text güncellemesi
                        string textMessage = currentText; // Sunucudaki mevcut text
                        byte[] responseBytes = Encoding.UTF8.GetBytes(textMessage);
                        await networkStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    }
                }
            }

            // Bağlantı kesildiğinde istemciyi listeden çıkar
            _connectedClients.Remove(client);
        }

    }
}
