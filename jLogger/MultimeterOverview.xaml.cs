using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Interop;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using MahApps.Metro.Controls;
using MahApps;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.Charts;
using Microsoft.Research.DynamicDataDisplay.Common;
using Microsoft.Research.DynamicDataDisplay.DataSources;

namespace jLogger
{
    /// <summary>
    /// Interaction logic for MultimeterOverview.xaml
    /// </summary>
    public partial class MultimeterOverview : UserControl, INotifyPropertyChanged
    {
        private string meterName;
        public string MeterName { get { return meterName; } set { meterName = value; OnPropertyChanged("MeterName"); } }
        private int goodPackets;
        public int GoodPackets { get { return goodPackets; } set { goodPackets = value; OnPropertyChanged("GoodPackets"); } }
        private int invalidPackets;
        public int InvalidPackets { get { return invalidPackets; } set { invalidPackets = value; OnPropertyChanged("InvalidPackets"); } }
        private TimeSpan elapsedTime;
        public TimeSpan ElapsedTime { get { return elapsedTime; } set { elapsedTime = value; OnPropertyChanged("ElapsedTime"); } }
        private double maxValue;
        public double MaxValue { get { return maxValue; } set { maxValue = value; OnPropertyChanged("MaxValue"); } }
        private double minValue;
        public double MinValue { get { return minValue; } set { minValue = value; OnPropertyChanged("MinValue"); } }
        private double currentValue;
        public double CurrentValue { get { return currentValue; } set { currentValue = value; OnPropertyChanged("CurrentValue"); } }
        private string currentUnits;
        public string CurrentUnits { get { return currentUnits; } set { currentUnits = value; OnPropertyChanged("CurrentUnits"); } }
        private string currentStatus;
        public string CurrentStatus { get { return currentStatus; } set { currentStatus = value; OnPropertyChanged("CurrentStatus"); } }

        public Multimeter multimeter;
        public MainWindow program;

        public MultimeterOverview()
        {
            InitializeComponent();
        }

        public MultimeterOverview(MainWindow reference, Multimeter multi)
        {
            InitializeComponent();
            program = reference;
            multimeter = multi;

            MeterName = "Unknown";
            GoodPackets = 0;
            InvalidPackets = 0;
            ElapsedTime = new TimeSpan(0, 0, 0);
            MaxValue = 0;
            MinValue = 0;
            CurrentValue = 0;
            CurrentUnits = "Unknown";
            CurrentStatus = "Idle";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(String prop)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }
    }
}
