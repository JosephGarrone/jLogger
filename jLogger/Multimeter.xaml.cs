using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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
using MahApps.Metro.Controls;
using MahApps;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.Charts;
using Microsoft.Research.DynamicDataDisplay.Common;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;
using Microsoft.Win32.SafeHandles;

namespace jLogger
{
    /// <summary>
    /// Interaction logic for Multimeter.xaml
    /// </summary>
    public partial class Multimeter : UserControl, INotifyPropertyChanged
    {
        public DataHandler handler;
        private Thread exportData = null;
        private Thread loadDevices = null;
        private delegate void WriteConsolasDelegate(String msg, String level);

        public MainWindow program;
        public TabItem tab;
        public TabControl control;
        public LineGraph graph;

        public int VendorId = 0;
        public int ProductId = 0;
        public int Rev = 0;
        public UsbRegistry Device = null;

        public bool canLog = false;
        public bool TableAutoScroll { get; set; }
        public bool GraphAutoScroll { get; set; }
        public bool ConsoleAutoScroll { get; set; }
        public double TimingInterval { get; set; }
        public int AverageInterval { get; set; }
        public int AutoSave { get; set; }
        private string displayUnits;
        private string displayValue;
        public string DisplayUnits { get { return displayUnits; } set { displayUnits = value; OnPropertyChanged("DisplayUnits"); } }
        public string DisplayValue { get { return displayValue; } set { displayValue = value; OnPropertyChanged("DisplayValue"); } }
        private string dataName;
        public string DataName { get { return dataName; } set { dataName = value; handler.SetMeter(value); } }

        public double HiLimit { get; set; }
        public double HiRelease { get; set; }
        public double LoLimit { get; set; }
        public double LoRelease { get; set; }
        public int Alarm { get; set; }
        public int Sound { get; set; }
        public bool HiAlarm = false;
        public bool LoAlarm = false;

        public int Index;
        public MultimeterOverview overview;

        public string saveLocation = "";

        public Multimeter()
        {
            InitializeComponent();
        }

        public Multimeter(MainWindow refer, TabItem t, TabControl tc, int index)
        {
            InitializeComponent();

            program = refer;
            tab = t;
            control = tc;

            DataContext = this;
            
            Index = index;

            TableAutoScroll = true;
            ConsoleAutoScroll = true;
            GraphAutoScroll = true;
            TimingInterval = 2;
            AverageInterval = 20;
            AutoSave = 1;
            Alarm = 0;
            Sound = 0;
            DisplayValue = "00.000";
            DisplayUnits = "Unknown";

            handler = new DataHandler(this);
            handler.InitialiseTable();

            overview = new MultimeterOverview(program, this);
            program.OverviewList.Children.Add(overview);

            overview.MeterName = handler.currentMeter;
            tab.Header = handler.currentMeter;

            loadDevices = new Thread(new ThreadStart(LoadDevices));
            loadDevices.IsBackground = true;
            loadDevices.Start();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(String prop)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }

        public void TextBoxValueChanged(object sender, TextCompositionEventArgs e)
        {
            e.Handled = TestForInt(e.Text);
        }

        public void DoubleValueChanged(object sender, TextCompositionEventArgs e)
        {
            e.Handled = TestForDouble(e.Text);
        }
        
        public bool TestForInt(string t)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("[^0-9]+");
            return regex.IsMatch(t);
        }

        public bool TestForDouble(string t)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("[^0-9.-]+");
            return regex.IsMatch(t);
        }

        public void Close()
        {
            handler.StopLogging();
            if (loadDevices != null && loadDevices.IsAlive)
            {
                loadDevices.Abort();
                loadDevices.Join();
            }
            if (exportData != null && exportData.IsAlive)
            {
                exportData.Abort();
                exportData.Join();
            }
            handler.totalLogTimer.Stop();
            handler.totalStopWatch.Stop();
        }

        public void LoadDevices()
        {
            UsbRegDeviceList devices = UsbDevice.AllDevices;
            for (int i = 0; i < devices.Count; i++)
            {
                if (loadDevices.ThreadState == System.Threading.ThreadState.AbortRequested)
                {
                    break;
                }
                else
                {
                    UsbRegistry dev = devices[i];
                    string name = "";
                    if (dev.Vid == 0x1A86 && dev.Pid == 0xE008)
                    {
                        name = "Tenma 72-7730A";
                    }
                    else if (dev.Vid == 0x04FA && dev.Pid == 0x2490)
                    {
                        name = "Tenma 72-7730";
                    }
                    else
                    {
                        name = dev.Name;
                    }

                    string content = name + " [" + dev.Vid.ToString() + ":" + dev.Pid.ToString() + "]";

                    Application.Current.Dispatcher.BeginInvoke(new ThreadStart(() =>
                    {
                        AddDevice(content, dev.Vid, dev.Pid, dev.Rev, dev);
                    }));
                }
            }
        }

        public void AddDevice(string content, int vid, int pid, int rev, UsbRegistry dev)
        {
            object[] o = new Object[] { vid, pid, rev, dev };
            if (AvailableDevices.Items.Contains(LoadingDevices))
            {
                LoadingDevices.Content = "Please select a device.";
            }

            ComboBoxItem item = new ComboBoxItem();
            item.Content = content;
            item.Tag = o;
            AvailableDevices.Items.Insert(AvailableDevices.Items.Count - 1, item);
        }

        public void WriteConsolas(String output, String level = "Log")
        {
            consolas.AppendText("[" + DateTime.Now.ToString("HH:mm:ss.fff tt") + "] <" + level + "> " + output + "\r");
            if (ConsoleAutoScroll)
            {
                consolas.ScrollToEnd();
            }
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (handler.IsLogging)
            {
                handler.StopLogging();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            program = (MainWindow) MainWindow.GetWindow(this);
        }

        public void ChangeDisplay(string value, string units)
        {
            DisplayValue = value;
            DisplayUnits = units;
        }

        private void TogglePolling(object sender, RoutedEventArgs e)
        {
            if (canLog)
            {
                Button s = sender as Button;
                if (!handler.IsRecording)
                {
                    if ((AutoSave == 0 && saveLocation != "") || AutoSave == 1)
                    {
                        handler.StartRecording();
                    }
                    else
                    {
                        new WPFMessageBox(program, "No save file selected", "Please select a save file location or disable autosave before you start logging.").Display();
                    }
                }
                else
                {
                    handler.StopRecording();
                }
            }
            else
            {
                new WPFMessageBox(program, "No device selected", "Please select a device.").Display();
            }
            ChangePollingButton();
        }

        public void ChangePollingButton()
        {
            if (handler.IsRecording)
            {
                TogglePollingButton.Content = "Stop";
                TogglePollingButton.ToolTip = "Stop logging";
            }
            else
            {
                TogglePollingButton.Content = "Start";
                TogglePollingButton.ToolTip = "Start logging";
            }
        }

        private void RestartPolling(object sender, RoutedEventArgs e)
        {
            if (canLog)
            {              
                TogglePollingButton.Content = "Start";
                TogglePollingButton.ToolTip = "Start logging";
                SendRestart();
            }
            else
            {
                new WPFMessageBox(program, "No device selected", "Please select a device.").Display();
            }
        }

        public void SendRestart()
        {
            if (handler.IsRecording)
            {
                handler.StopRecording();
            }
            if (handler.IsLogging)
            {
                handler.StopLogging();
            }
            dynamicChart.Children.Remove(handler.graph);
            logTable.Items.Clear();

            handler = new DataHandler(this);

            handler.StartLogging();
        }

        private void RemoveMultimeter(object sender, RoutedEventArgs e)
        {
            Button s = sender as Button;
            if (handler.IsLogging)
            {
                handler.StopLogging();
            }

            if (exportData != null && exportData.IsAlive)
            {
                exportData.Abort();
                exportData.Join();
            }

            control.SelectedIndex = control.SelectedIndex - 1;
            control.Items.Remove(tab);
            program.OverviewList.Children.Remove(overview);
            program.RemoveGraph(graph, Index);
        }

        private void ReloadDevices()
        {
            if (loadDevices.IsAlive)
            {
                loadDevices.Abort();
                loadDevices.Join();
            }

            while(AvailableDevices.Items.Count > 2)
            {
                AvailableDevices.Items.RemoveAt(1);
            }

            AvailableDevices.SelectedIndex = 0;

            loadDevices = new Thread(new ThreadStart(LoadDevices));
            loadDevices.IsBackground = true;
            loadDevices.Start();
        }

        private void ExportDataToCSV(object sender, RoutedEventArgs e)
        {
            if (logTable.Items.Count > 0)
            {
                MarshalSaveData();
            }
            else
            {
                new WPFMessageBox(program, "Unable to export data", "You must have logged some data before you can export it.").Display();
            }
        }

        public void MarshalSaveData(bool autosave = false)
        {
            LogData[] items = new LogData[logTable.Items.Count];
            logTable.Items.CopyTo(items, 0);
            string filename = saveLocation;

            string[] columns = new string[handler.Columns];
            for (int i = 0; i < logTable.Columns.Count; i++)
            {
                columns[i] = logTable.Columns[i].Header.ToString();
            }

            exportData = new Thread(() => AsyncWriteDataToFile(columns, items, filename, autosave));
            exportData.IsBackground = true;
            exportData.Start();
        }

        private void AsyncWriteDataToFile(string[] columns, LogData[] items, string filename, bool autosave)
        {
            if (File.Exists(filename))
            {
                try
                {
                    File.Copy(filename, filename + ".old", true);
                    File.Delete(filename);
                }
                catch (Exception e)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => { new WPFMessageBox(program, "Error!", "Unable to save data to the file specified. " + e.Message).Display(); }));
                }
            }
            StreamWriter sw = new StreamWriter(filename);
            string headings = "";
            for (int j = 0; j < columns.Length; j++)
            {
                if (exportData.ThreadState == System.Threading.ThreadState.AbortRequested)
                {
                    break;
                }
                string c = columns[j];
                if (j < columns.Count() - 1)
                {
                    headings += "\"" + c + "\"" + ",";
                }
                else
                {
                    headings += "\"" + c + "\"";
                }
            }
            sw.WriteLine(headings);

            foreach (LogData l in items)
            {
                if (exportData.ThreadState == System.Threading.ThreadState.AbortRequested)
                {
                    break;
                }

                string log = "\"" + l.PacketNumber.ToString() + "\"" + "," + "\"" + l.PacketTime.ToString() + "\"" + "," + "\"" + l.Value.ToString() + "\"" + "," + "\"" + l.Unit.ToString() + "\"" + "," + "\"" + l.Average.ToString() + "\"" + "," + "\"" + l.Aggregate.ToString() + "\"" + "," + "\"" + l.Message.ToString() + "\"";
                sw.WriteLine(log);
            }
            sw.Flush();
            sw.Close();
            sw.Dispose();
            if (File.Exists(filename) && File.Exists(filename + ".old"))
            {
                try
                {
                    File.Delete(filename + ".old");
                    if (!autosave)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => { new WPFMessageBox(program, "Success!", "All data saved successfully!").Display(); }));
                    }
                }
                catch (Exception e)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => { new WPFMessageBox(program, "Error!", "Unable to save data to the file specified. " + e.Message).Display(); }));
                }
            }
            else
            {
                if (!autosave)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => { new WPFMessageBox(program, "Error!", "Unable to save data to the file specified. Please try again.").Display(); }));
                }
            }

        }

        private void ChangeDevice(object sender, SelectionChangedEventArgs e)
        {
            if (AvailableDevices.SelectedItem != null && AvailableDevices.SelectedIndex != 0 && AvailableDevices.SelectedIndex != AvailableDevices.Items.Count - 1)
            {
                object[] tag = (AvailableDevices.SelectedItem as ComboBoxItem).Tag as object[];
                VendorId = (int)tag[0];
                ProductId = (int)tag[1];
                Rev = (int)tag[2];
                Device = (UsbRegistry)tag[3];
                canLog = true;
                string name = (AvailableDevices.SelectedItem as ComboBoxItem).Content.ToString().Substring(0, (AvailableDevices.SelectedItem as ComboBoxItem).Content.ToString().IndexOf("[") - 1);
                if (handler.IsLogging)
                {
                    handler.StopLogging();
                }
                if (handler.IsRecording)
                {
                    handler.StopRecording();
                }
                handler.StartLogging();
            }
            if (AvailableDevices.SelectedIndex == 0 && handler != null)
            {
                canLog = false;
                if (handler.IsRecording)
                {
                    handler.StopRecording();
                }
                if (handler.IsLogging)
                {
                    handler.StopLogging();
                }
                ChangePollingButton();
            }
            else if (AvailableDevices.SelectedIndex == AvailableDevices.Items.Count - 1)
            {
                if (handler.IsRecording)
                {
                    handler.StopRecording();
                }
                if (handler.IsLogging)
                {
                    handler.StopLogging();
                }
                ReloadDevices();
                ChangePollingButton();
            }
        }

        private void ChooseSaveLocation(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = handler.currentMeter.Replace("<", "(").Replace(">", ")"); // Default file name
            dlg.InitialDirectory = FileSaveLocation.Text;
            dlg.DefaultExt = ".csv"; // Default file extension
            dlg.Filter = "Text documents (.csv)|*.csv|All files (*.*)|*.*"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                FileSaveLocation.Text = dlg.FileName;
                FileSaveLocation.ToolTip = dlg.FileName;
                saveLocation = dlg.FileName;

            }
        }

        public string CheckForAlarms(double value)
        {
            string message = "";
            if (Alarm == 0)
            {
                if (value >= HiLimit && !HiAlarm)
                {
                    HiAlarm = true;
                    message += "Hi alarm triggered. ";
                    AlarmDisplay.Text = "HI ALARM";
                    AlarmDisplay.Background = Brushes.OrangeRed;
                }
                if (value <= HiRelease && HiAlarm)
                {
                    HiAlarm = false;
                    message += "Hi alarm released. ";
                    AlarmDisplay.Text = "NORMAL";
                    AlarmDisplay.Background = Brushes.LightGreen;
                }
                if (value <= LoLimit && !LoAlarm)
                {
                    LoAlarm = true;
                    message += "Lo alarm triggered. ";
                    AlarmDisplay.Text = "LO ALARM";
                    AlarmDisplay.Background = Brushes.Blue;
                }
                else if (value >= LoRelease && LoAlarm)
                {
                    LoAlarm = false;
                    message += "Lo alarm released. ";
                    AlarmDisplay.Text = "NORMAL";
                    AlarmDisplay.Background = Brushes.LightGreen;
                }
                if (value <= HiLimit && !HiAlarm && value >= LoLimit && !LoAlarm)
                {
                    AlarmDisplay.Text = "NORMAL";
                    AlarmDisplay.Background = Brushes.LightGreen;
                }
            }
            else
            {
                AlarmDisplay.Text = "NORMAL";
                AlarmDisplay.Background = Brushes.LightGreen;
            }
            return message;
        }
    }

    public class DataHandler
    {
        Stopwatch s = null;
        Stopwatch g = null; 

        public Stopwatch totalStopWatch;
        public DispatcherTimer totalLogTimer;

        public DateTime avgStart;
        public double average = 0;
        public double aggregate = 0;
        public double averageCount = 0;

        public int Columns = 0;

        Packet p = null;
        bool isData = false;
        bool isTiming = false;
        bool isGapTiming = false;
        bool isGoodData = false;
        long timeFromLastPacket = 0;

        private double minX = 0;
        private double maxX = 10;
        private double minY = 0;
        private double maxY = 10;

        public double MaximumValid = 0;
        public double MinimumValid = 0;
        public int GoodPackets = 0;
        public int InvalidPackets = 0;
        public int ConsecutiveErrors = 0;

        private Multimeter multimeter = null;

        public List<Packet> packets = new List<Packet>();
        public List<LogData> data = new List<LogData>();
        public ObservableDataSource<DatePoint> loggedValues;
        public LineGraph graph;
        
        private delegate void NoArgDelegate();
        private delegate void OneArgDelegate(byte[] array);
        private delegate void WriteConsolasDelegate(String msg, String level);

        public Thread poller = null;
        public bool IsLogging = false;
        public bool IsRecording = false;
        private bool stopRequested = false;
        private bool fixRequested = false;
        public bool IsInitialising = true;

        public string currentMeter;

        public DataHandler(Multimeter refer)
        {
            s = new Stopwatch();
            g = new Stopwatch();
            multimeter = refer;
            currentMeter = "DMM " + (multimeter.Index + 1).ToString();

            totalStopWatch = new Stopwatch();
            totalLogTimer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, delegate
                {
                    multimeter.overview.ElapsedTimeDisplay.Text = String.Format("{0:hh\\:mm\\:ss}", totalStopWatch.Elapsed);
                }, Application.Current.Dispatcher);

            HorizontalDateTimeAxis axis = multimeter.dynamicChart.HorizontalAxis as HorizontalDateTimeAxis;
            loggedValues = new ObservableDataSource<DatePoint>();
            loggedValues.SetXMapping(p => axis.ConvertToDouble(p.X));
            loggedValues.SetYMapping(p => p.Y);
            graph = multimeter.dynamicChart.AddLineGraph(loggedValues, Colors.Blue, 2, currentMeter);
        }

        public void SetMeter(string name)
        {
            if (name != "")
            {
                currentMeter = "DMM " + (multimeter.Index + 1).ToString() + " <" + name + ">";
            }
            else
            {
                currentMeter = "DMM " + (multimeter.Index + 1).ToString();
            }
            multimeter.tab.Header = currentMeter;
            multimeter.dynamicChart.Children.Remove(graph);
            multimeter.program.UpdateName(currentMeter, multimeter.graph, multimeter.Index);
            graph = multimeter.dynamicChart.AddLineGraph(loggedValues, Colors.Blue, 2, currentMeter);
            multimeter.overview.MeterName = currentMeter;
        }

        public void InitialiseTable()
        {
            AddLogTableColumn("ID", "PacketNumber");
            AddLogTableColumn("Time", "PacketTime");
            AddLogTableColumn("Value", "Value");
            AddLogTableColumn("Unit", "Unit");
            AddLogTableColumn("Average", "Average");
            AddLogTableColumn("Aggregate", "Aggregate");
            AddLogTableColumn("Message", "Message");
            (multimeter.logTable.Columns[1] as DataGridTextColumn).Binding.StringFormat = "yy/MM/dd HH:mm:ss.fff";
        }
        
        public void AddChartValues(DateTime x, double y)
        {
            HorizontalDateTimeAxis axis = multimeter.dynamicChart.HorizontalAxis as HorizontalDateTimeAxis;

            minX = axis.ConvertToDouble(x.AddSeconds(-8 * multimeter.TimingInterval));
            maxX = axis.ConvertToDouble(x.AddSeconds(4 * multimeter.TimingInterval));
            maxY = 2 * y;

            loggedValues.AppendAsync(Application.Current.Dispatcher, new DatePoint(x, y));
            if (multimeter.GraphAutoScroll)
            {
                multimeter.dynamicChart.Viewport.Visible = new Microsoft.Research.DynamicDataDisplay.Common.DataRect(new Point(minX, 0), new Point(maxX, maxY));
            }

            multimeter.program.AddChartValues(x, y, multimeter.Index);
        }

        public void AddLogTableColumn(string header, string binding)
        {
            DataGridTextColumn column = new DataGridTextColumn();
            column.Header = header;
            column.Binding = new Binding(binding);
            multimeter.logTable.Columns.Add(column);
            Columns++;
        }

        public void AddDataToTable(LogData data)
        {
            multimeter.logTable.Items.Add(data);
            if (multimeter.TableAutoScroll)
            {
                multimeter.logTable.ScrollIntoView(data);
            }
        }

        public void RequestFix()
        {
            fixRequested = true;
        }

        public void CloseFix()
        {
            fixRequested = false;
        }

        public void StartLogging()
        {
            if (poller != null && poller.IsAlive)
            {
                StopPoller();
                poller.Join();
            }
            stopRequested = false;
            poller = new Thread(new ThreadStart(PollUSBDevice));
            poller.IsBackground = true;
            poller.Name = multimeter.VendorId.ToString() + ":" + multimeter.ProductId.ToString();
            poller.Start();
            IsLogging = true;
        }

        public void StopLogging()
        {
            if (poller != null && poller.IsAlive)
            {
                StopPoller();
                poller.Join();
                IsLogging = false;
            }
        }

        public void StartRecording()
        {
            multimeter.WriteConsolas("Started recording.");
            IsRecording = true;
            multimeter.AvailableDevices.IsReadOnly = true;
            multimeter.overview.CurrentStatus = "Logging";
            totalLogTimer.Start();
            totalStopWatch.Start();
        }

        public void StopRecording()
        {
            multimeter.WriteConsolas("Stopped recording.");
            IsRecording = false;
            multimeter.AvailableDevices.IsReadOnly = false;
            multimeter.overview.CurrentStatus = "Idle";
            totalLogTimer.Stop();
            totalStopWatch.Stop();
            multimeter.DisplayUnits = "Unknown";
            multimeter.DisplayValue = "00.000";
        }

        public void StopPoller()
        {
            stopRequested = true;
        }

        private void PollUSBDevice()
        {
            ErrorCode ec = ErrorCode.None;

            UsbDevice MyUsbDevice = null;

            UsbDeviceFinder MyUsbFinder = new UsbDeviceFinder(multimeter.VendorId, multimeter.ProductId);

            UsbEndpointReader reader = null;

            try
            {
                MyUsbDevice = UsbDevice.OpenUsbDevice(MyUsbFinder);

                UsbEndpointInfo endpointInfo = null;
                UsbInterfaceInfo usbInterfaceInfo = null;

                if (UsbEndpointBase.LookupEndpointInfo(MyUsbDevice.Configs[0], UsbConstants.ENDPOINT_DIR_MASK, out usbInterfaceInfo, out endpointInfo) == false)
                {
                    MyUsbDevice.Close();
                    MyUsbDevice = null;
                }

                if (MyUsbDevice == null)
                {
                    Application.Current.Dispatcher.BeginInvoke(
                            new WriteConsolasDelegate(multimeter.WriteConsolas),
                            new object[] { "Can't find multimeter device!", "Error" });
                }

                IUsbDevice wholeUsbDevice = MyUsbDevice as IUsbDevice;
                if (!ReferenceEquals(wholeUsbDevice, null))
                {
                    wholeUsbDevice.SetConfiguration(1);
                    wholeUsbDevice.SetConfiguration(0);
                    wholeUsbDevice.SetConfiguration(1);

                    wholeUsbDevice.ClaimInterface(usbInterfaceInfo.Descriptor.InterfaceID);
                }

                UsbSetupPacket packetSettings = new UsbSetupPacket(0x21, 0x09, 0x0300, 0x0000, 0x0005);

                byte[] buffer = { 0x60, 0x09, 0x00, 0x00, 0x03 };
                int transferred = 0;

                MyUsbDevice.ControlTransfer(ref packetSettings, buffer, 5, out transferred);

                transferred = 0;
                MyUsbDevice.ControlTransfer(ref packetSettings, buffer, 5, out transferred);
                
                try
                {
                    reader = MyUsbDevice.OpenEndpointReader((ReadEndpointID)endpointInfo.Descriptor.EndpointID, 0, (EndpointType)(endpointInfo.Descriptor.Attributes & 0x3));

                }
                catch (Exception e)
                {
                    Application.Current.Dispatcher.BeginInvoke(
                            new WriteConsolasDelegate(multimeter.WriteConsolas),
                            new object[] { e.Message, "Error" });
                }


                Application.Current.Dispatcher.BeginInvoke(
                        new WriteConsolasDelegate(multimeter.WriteConsolas),
                        new object[] { String.Format("Starting read from device <{0}:{1}>.", multimeter.VendorId.ToString(), multimeter.ProductId.ToString()), "Log" });

                while (ec == ErrorCode.None && !stopRequested)
                {
                    if (fixRequested)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            CloseFix();
                        }));
                        transferred = 0;
                        MyUsbDevice.ControlTransfer(ref packetSettings, buffer, 5, out transferred);
                        transferred = 0;
                        MyUsbDevice.ControlTransfer(ref packetSettings, buffer, 5, out transferred);
                        transferred = 0;
                        MyUsbDevice.ControlTransfer(ref packetSettings, buffer, 5, out transferred);

                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            multimeter.WriteConsolas("Fix attempt ended.");
                        }));
                    }

                    byte[] readBuffer = new byte[8];
                    int bytesRead;
                    ec = reader.Read(readBuffer, 100, out bytesRead);

                    byte[] cloneBuffer = readBuffer;

                    Application.Current.Dispatcher.BeginInvoke(
                            new OneArgDelegate(HandleData),
                            new object[] { cloneBuffer }); 
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Application.Current.Dispatcher.BeginInvoke(
                            new WriteConsolasDelegate(multimeter.WriteConsolas),
                            new object[] { ex.Message, "Error" });
                }
                catch (Exception e)
                {

                }
            }
            finally
            {
                Application.Current.Dispatcher.BeginInvoke(
                            new WriteConsolasDelegate(multimeter.WriteConsolas),
                            new object[] { "Stopping read. " + ec.ToString(), "Log" });


                if (reader != null)
                {
                    reader.Abort();
                }
                if (MyUsbDevice != null)
                {
                    if (MyUsbDevice.IsOpen)
                    {
                        IUsbDevice wholeUsbDevice = MyUsbDevice as IUsbDevice;
                        if (!ReferenceEquals(wholeUsbDevice, null))
                        {
                            wholeUsbDevice.ReleaseInterface(0);
                        }

                        MyUsbDevice.Close();
                    }
                    MyUsbDevice = null;
                    try
                    {
                        UsbDevice.Exit();
                    }
                    catch (Exception e)
                    {
                        Application.Current.Dispatcher.BeginInvoke(
                            new WriteConsolasDelegate(multimeter.WriteConsolas),
                            new object[] { e.Message, "Error" });
                    }
                }
            }
        }

        public void HandleData(byte[] buffer)
        {
            if (isGoodData)
            {
                if (buffer[0] == 240)
                {
                    if (!isGapTiming)
                    {
                        isGapTiming = true;
                        g.Reset();
                        g.Start();
                    }

                    if (isData)
                    {
                        long elapsed = 0;
                        if (isTiming)
                        {
                            elapsed = s.ElapsedMilliseconds;
                            isTiming = false;
                        }
                        p.Finalise(elapsed, timeFromLastPacket);
                        p = new Packet(multimeter, this);
                        packets.Add(p);
                        isData = false;
                    }
                }
                else
                {
                    if (isGapTiming)
                    {
                        g.Stop();
                        timeFromLastPacket = g.ElapsedMilliseconds;
                        isGapTiming = false;
                    }
                    if (isData == false)
                    {
                        isData = true;
                    }

                    if (isData)
                    {
                        if (p == null)
                        {
                            p = new Packet(multimeter, this);
                            packets.Add(p);
                        }

                        for (int i = 0; i < buffer[0] - 240; i++)
                        {
                            p.AddData(buffer[i + 1]);
                        }
                        //multimeter.WriteConsolas(BitConverter.ToString(buffer).Replace("-", " "));
                    }

                    if (isTiming)
                    {
                        s.Stop();
                        s.Reset();
                        isTiming = false;
                    }

                    if (!isTiming)
                    {
                        s.Start();
                        isTiming = true;
                    }
                }
            }
            else if (buffer[0] == 240)
            {
                isGoodData = true;

                if (!isGapTiming)
                {
                    isGapTiming = true;
                    g.Reset();
                    g.Start();
                }
            }
        }
    }

    public class Packet
    {
        int one = 0;
        int two = 0;
        int three = 0;
        int four = 0;
        int five = 0;
        int range = 0;
        int type = 0;

        string prefix = "";
        string value = "";
        string suffix = "";
        string packetTime = "";
        string lastPacket = "";
        string voltageType = "";
        string mode = "";


        byte[] data = new byte[64];
        int count = 0;

        private LogData logData;

        private Multimeter multimeter = null;
        private DataHandler handler = null;

        private delegate void WriteConsolasDelegate(String msg, String level);

        static string[][] suffixes = new string[][]
        {
            new string [] {"mV AC", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown"}, //mvac
            new string [] {"Unknown", "V DC", "V DC", "V DC", "V DC", "Unknown", "Unknown", "Unknown"}, //vdc
            new string [] {"Unknown", "V AC", "V AC", "V AC", "V AC", "Unknown", "Unknown", "Unknown"}, //vac
            new string [] {"mV DC", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown"}, //mvdc
            new string [] {"Unknown", "Ohms", "kOhms", "kOhms", "kOhms", "MOhms", "MOhms", "MOhms"}, //ohms
            new string [] {"HFE", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown"}, //HFE
            new string [] {"Celsius", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown"}, //celsius
            new string [] {"µA", "µA", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown"}, //microamp
            new string [] {"mA", "mA", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown"}, //milliamp
            new string [] {"Unknown", "Amps", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown"}, //amp
            new string [] {"Unknown", "Ohms", "kOhms", "kOhms", "kOhms", "MOhms", "MOhms", "MOhms"}, //ohm (continuity)
            new string [] {"V", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown"}, //v (diode)
            new string [] {"Hz", "Hz", "kHz", "kHz", "kHz", "MHz", "MHz", "MHz"}, //hz
            new string [] {"Fahreinheit", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown"}, //fahrienheit
            new string [] {"Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown"}, //watts ### NOT RELEVANT ###
            new string [] {"%", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown", "Unknown"}, //%
        };

        static int[][] point = new int[][]
        {
            new int [] {3, 0, 0, 0, 0, 0, 0, 0}, //mvac
            new int [] {0, 1, 2, 3, 4, 0, 0, 0}, //vdc
            new int [] {0, 1, 2, 3, 4, 0, 0, 0}, //vac
            new int [] {3, 0, 0, 0, 0, 0, 0, 0}, //mvdc
            new int [] {0, 3, 1, 2, 3, 1, 2, 3}, //ohms
            new int [] {0, 2, 3, 1, 2, 3, 1, 2}, //HFE
            new int [] {4, 0, 0, 0, 0, 0, 0, 0}, //celsius
            new int [] {3, 4, 0, 0, 0, 0, 0, 0}, //microamp
            new int [] {2, 3, 0, 0, 0, 0, 0, 0}, //milliamp
            new int [] {0, 2, 0, 0, 0, 0, 0, 0}, //amp
            new int [] {0, 3, 1, 2, 3, 1, 2, 3}, //ohm (continuity)
            new int [] {0, 1, 2, 3, 4, 0, 0, 0}, //v (diode)
            new int [] {3, 0, 0, 0, 0, 0, 0, 0}, //hz
            new int [] {4, 0, 0, 0, 0, 0, 0, 0}, //fahrienheit
            new int [] {0, 0, 0, 0, 0, 0, 0, 0}, //watts :: NOT RELEVANT FOR TENMA 72-7730
            new int [] {3, 0, 0, 0, 0, 0, 0, 0}, //%
        };

        public Packet(Multimeter refer, DataHandler handler)
        {
            this.multimeter = refer;
            this.handler = handler;
            count = 0;
            data = new byte[11];
        }

        public void AddData(byte buffer)
        {
            if (count < 11)
            {
                data[count] = buffer;
                count++;
            }
        }

        public void Finalise(long elapsed, long previousPacket)
        {
            bool valid = true;
            one = (byte)((data[0] << 4) & 0XF0) >> 4;
            two = (byte)((data[1] << 4) & 0XF0) >> 4;
            three = (byte)((data[2] << 4) & 0XF0) >> 4;
            four = (byte)((data[3] << 4) & 0XF0) >> 4;
            five = (byte)((data[4] << 4) & 0XF0) >> 4;
            range = (byte)((data[5] << 4) & 0XF0) >> 4;
            type = (byte)((data[6] << 4) & 0XF0) >> 4;
            string t = Convert.ToString((byte)(data[7]), 2).PadLeft(8, '0');
            string m = Convert.ToString((byte)(data[8]), 2).PadLeft(8, '0');
            voltageType = (t.Substring(6, 1) == "1" && t.Substring(7, 1) == "1") ? "AC + DC" : (t.Substring(6, 1) == "1" && t.Substring(7, 1) != "1") ? "DC" : (t.Substring(6, 1) != "1" && t.Substring(7, 1) == "1") ? "AC" : "";
            prefix = (m.Substring(5, 1) == "1") ? "-" : "";
            mode = (m.Substring(6, 1) == "1") ? "MAN" : (m.Substring(7, 1) == "1") ? "AUTO" : "";

            //Optional message to display
            string message = "";

            //Determine value to display
            string numberValue = one.ToString() + two.ToString() + three.ToString() + four.ToString() + five.ToString();
            int decimalLocation = 1;
            if (type < 16 && range < 8)
            {
                decimalLocation = point[type][range];
            }
            else
            {
                valid = false;
                message += String.Format("Unexpected type <{0}> and range <{1}>. ", type.ToString(), range.ToString());
            }
            value = prefix + numberValue.Insert(decimalLocation, ".");

            //Determine packet timings
            packetTime = elapsed.ToString() + "ms";
            lastPacket = previousPacket.ToString() + "ms";

            //Determine units
            suffix = "Unknown";
            if (type < 16 && range < 8)
            {
                suffix = suffixes[type][range];
            }
            else
            {
                message += String.Format("Unexpected type <{0}> and range <{1}>. ", type.ToString(), range.ToString());
                valid = false;
            }

            if (count != 11)
            {
                message += String.Format("Invalid message count <{0}/11>. ", count.ToString());
                valid = false;
            }

            //Send data to LogData object
            DateTime currentTime = DateTime.Now;

            if (valid)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    multimeter.ChangeDisplay(value, suffix);
                }));
            }

            double v = double.Parse(value);

            if (!handler.IsRecording)
            {
                multimeter.CheckForAlarms(v);
            }

            if (handler.IsInitialising)
            {
                if (valid && handler.packets.Count > 1)
                {
                    handler.IsInitialising = false;
                }
                else if (!valid && handler.packets.Count > 1)
                {
                    multimeter.WriteConsolas("Invalid input buffer detected. Attempting to rectify.");
                    multimeter.SendRestart();
                }
            }
            else if ((handler.data.Count == 0 || multimeter.TimingInterval == 0 || CompareTimes(handler.data[handler.data.Count - 1].PacketTime, currentTime, multimeter.TimingInterval)) && handler.IsRecording)
            {
                if (handler.data.Count == 0)
                {
                    handler.avgStart = currentTime;
                }

                string average = "-";
                string aggregate = "-";
                if (CompareTimes(handler.avgStart, currentTime, multimeter.AverageInterval))
                {
                    average = (handler.average / handler.averageCount).ToString();
                    aggregate = Math.Sqrt(handler.aggregate / handler.averageCount).ToString();
                    handler.average = 0;
                    handler.averageCount = 0;
                    handler.aggregate = 0;
                    handler.avgStart = currentTime;
                }
                else if (valid)
                {
                    handler.aggregate += v * v;
                    handler.average += v;
                    handler.averageCount++;
                }

                if (valid)
                {
                    handler.GoodPackets++;
                    handler.ConsecutiveErrors = 0;
                    if (v > handler.MaximumValid)
                    {
                        handler.MaximumValid = v;
                    }
                    if (v < handler.MinimumValid)
                    {
                        handler.MinimumValid = v;
                    }
                    
                    if (handler.data.Count == 0)
                    {
                        handler.MaximumValid = v;
                        handler.MinimumValid = v;
                    }
                }
                else
                {
                    handler.InvalidPackets++;
                    handler.ConsecutiveErrors++;
                    if (handler.ConsecutiveErrors == 3)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => 
                        {
                            //new WPFMessageBox(multimeter.program, "Multimeter error", "It appears as if there have been 3 consecutive invalid packets logged. This is normally an indication that your device is not transmitting data correctly. Please try stopping and starting the logging again to see if that will rectify the issue. If that fails, try plugging your USB device into another USB port and trying again.").Display();
                            multimeter.WriteConsolas("Invalid readings detected. Attempting to fix.");
                            handler.RequestFix();
                        }));
                    }
                }

                message += multimeter.CheckForAlarms(v);
                logData = new LogData(v, suffix, elapsed, previousPacket, currentTime, message, handler.data.Count()+1, valid, average, aggregate);
                handler.data.Add(logData);
                handler.AddDataToTable(logData);
                handler.AddChartValues(logData.PacketTime, logData.Value);
                multimeter.VerticalTitle.Content = suffix;
                multimeter.overview.GoodPackets = handler.GoodPackets;
                multimeter.overview.InvalidPackets = handler.InvalidPackets;
                multimeter.overview.MaxValue = handler.MaximumValid;
                multimeter.overview.MinValue = handler.MinimumValid;
                multimeter.overview.CurrentValue = v;
                if (multimeter.overview.CurrentUnits != suffix)
                {
                    multimeter.overview.CurrentUnits = suffix;
                }

                if ((handler.GoodPackets+handler.InvalidPackets) % 50 == 0 && multimeter.AutoSave == 0)
                {
                    multimeter.MarshalSaveData(true);
                    multimeter.WriteConsolas("Autosave at " + (handler.GoodPackets+handler.InvalidPackets).ToString() + " packets");
                }
            }
        }

        public bool CompareTimes(DateTime start, DateTime end, double interval)
        {
            if (interval < 60)
            {
                if (start.AddSeconds(interval) < end)
                {
                    return true;
                }
                else if (start.Second != end.Second && start.AddSeconds(interval).Second == end.Second)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class LogData
    {
        public double Value { get; set; }
        public string Unit { get; set; }
        public long PacketTiming { get; set; }
        public long PacketInterval { get; set; }
        public DateTime PacketTime { get; set; }
        public string Message { get; set; }
        public int PacketNumber { get; set; }
        public bool Valid { get; set; }
        public string Average { get; set; }
        public string Aggregate { get; set; }

        public LogData(double value, string unit, long timing, long interval, DateTime packettime, String message, int packetNumber, bool valid, string average, string aggregate)
        {
            Value = value;
            Unit = unit;
            PacketTiming = timing;
            PacketInterval = interval;
            PacketTime = packettime;
            Message = message;
            PacketNumber = packetNumber;
            Valid = valid;
            Average = average;
            Aggregate = aggregate;
        }
    }
}
