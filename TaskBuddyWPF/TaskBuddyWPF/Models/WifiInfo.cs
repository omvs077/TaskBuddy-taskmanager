using System.ComponentModel;

namespace TaskBuddyWPF.Models
{
    public class WifiInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public bool IsConnected { get; set; }
        public string AdapterName { get; set; } = string.Empty;
        public string SSID { get; set; } = string.Empty;
        public string ConnectionType { get; set; } = string.Empty;
        public string IPv4Address { get; set; } = string.Empty;
        public string IPv6Address { get; set; } = string.Empty;

        private int _signalQuality;
        public int SignalQuality
        {
            get => _signalQuality;
            set { if (_signalQuality != value) { _signalQuality = value; Notify(nameof(SignalQuality)); } }
        }

        private double _sendKbps;
        public double SendKbps
        {
            get => _sendKbps;
            set { if (_sendKbps != value) { _sendKbps = value; Notify(nameof(SendKbps)); } }
        }

        private double _receiveKbps;
        public double ReceiveKbps
        {
            get => _receiveKbps;
            set { if (_receiveKbps != value) { _receiveKbps = value; Notify(nameof(ReceiveKbps)); } }
        }
    }
}
