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
using MahApps.Metro.Controls;
using MahApps;
using MahApps.Metro;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.Charts;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Win32;
using LibUsbDotNet.Main;
using LibUsbDotNet;


namespace jLogger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public Collection<Multimeter> multimeters;
        private Thread installDevices = null;
        public Collection<ObservableDataSource<DatePoint>> points;
        public Collection<LineGraph> graphs;

        private double minX = 0;
        private double maxX = 10;
        private double minY = 0;
        private double maxY = 10;

        public MainWindow()
        {
            InitializeComponent();

            multimeters = new Collection<Multimeter>();

            this.DataContext = this;

            points = new Collection<ObservableDataSource<DatePoint>>();
            graphs = new Collection<LineGraph>();

         }

        public void WriteConsolas(String output, String level = "Log")
        {
            consolas.AppendText("[" + DateTime.Now.ToString("HH:mm:ss.fff tt") + "] <" + level + "> " + output + "\r");
            /*if (ConsoleAutoScroll)
            {*/
                consolas.ScrollToEnd();
            //}
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(String prop)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }

        private void _AddNewTab(object sender, RoutedEventArgs e)
        {
            AddNewTab(MultimeterTabs.SelectedIndex);
        }

        private void AddNewTab(int index = 1)
        {
            TabItem t = new TabItem();
            MultimeterTabs.Items.Insert(index, t);
            Multimeter multimeter = new Multimeter(this, t, MultimeterTabs, multimeters.Count);
            multimeters.Add(multimeter);
            t.Content = multimeter;
            MultimeterTabs.SelectedIndex = index;

            HorizontalDateTimeAxis axis = dynamicChart.HorizontalAxis as HorizontalDateTimeAxis;
            ObservableDataSource<DatePoint> values = new ObservableDataSource<DatePoint>();
            values.SetYMapping(p => p.Y);
            values.SetXMapping(p => axis.ConvertToDouble(p.X));
            points.Add(values);
            graphs.Add(dynamicChart.AddLineGraph(values, 2, multimeter.handler.currentMeter));
            multimeter.graph = graphs.Last<LineGraph>();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RegistryKey v = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\jLogger");
            string FirstRun = "true";
            
            if (v == null)
            {
                FirstRun = "true";
            }
            else
            {
                FirstRun = v.GetValue("FirstRun", "null").ToString();
            }

            if (FirstRun == "null" || FirstRun == "true" || FirstRun == "false")
            {
                installDevices = new Thread(new ThreadStart(InstallDevices));
                installDevices.IsBackground = true;
                installDevices.Start();
            }

            RegistryKey r = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\jLogger");
            r.SetValue("FirstRun", "false");
            AddNewTab();
            AddNewTab(2);
            AddNewTab(3);
            AddNewTab(4);
            MultimeterTabs.SelectedIndex = 1;
        }

        public void InstallDevices()
        {
            if (System.Environment.Is64BitOperatingSystem == false)
            {
                if (File.Exists("C:/Windows/System32/libusb-1.0.dll") == false)
                {
                    byte[] dll = Properties.Resources.libusb1x86;
                    using (FileStream file = new FileStream("C:/Windows/System32/libusb-1.0.dll", FileMode.Create))
                    {
                        file.Write(dll, 0, dll.Length);
                    }
                }
                byte[] exeBytes = Properties.Resources.installFilter32;
                string exeToRun = "install-filter.exe";
                using (FileStream exeFile = new FileStream(exeToRun, FileMode.Create))
                {
                    exeFile.Write(exeBytes, 0, exeBytes.Length);
                }

                UsbRegDeviceList usbDevices = UsbDevice.AllDevices;
                for (int i = 0; i < usbDevices.Count; i++)
                {
                    UsbRegistry dev = usbDevices[i];
                    using (Process exeProcess = Process.Start(exeToRun, "-i --device=USB\\Vid_" + dev.Vid.ToString("X4") + ".Pid_" + dev.Pid.ToString("X4") + ".Rev_" + dev.Rev.ToString("X4")))
                    {
                        exeProcess.WaitForExit();
                    }
                }

                /*string[] devices = new string[] { "-i --device=USB\\Vid_1a86.Pid_e008.Rev_1200", "-i --device=USB\\Vid_04fa.Pid_2490.Rev_0000" };
                for (int i = 0; i < devices.Length; i++)
                {
                    if (installDevices.ThreadState == System.Threading.ThreadState.AbortRequested)
                    {
                        break;
                    }
                    else
                    {
                        using (Process exeProcess = Process.Start(exeToRun, devices[i]))
                        {

                            exeProcess.WaitForExit();
                        }
                    }
                }*/
                File.Delete(exeToRun);
            }
            else if (System.Environment.Is64BitOperatingSystem)
            {
                if (File.Exists("C:/Windows/SysWOW64/libusb-1.0.dll") == false)
                {
                    byte[] dll = Properties.Resources.libusb1amd64;
                    using (FileStream file = new FileStream("C:/Windows/SysWOW64/libusb-1.0.dll", FileMode.Create))
                    {
                        file.Write(dll, 0, dll.Length);
                    }
                }
                if (File.Exists("C:/Windows/System32/libusb-1.0.dll") == false)
                {
                    byte[] dll = Properties.Resources.libusb1x86;
                    using (FileStream file = new FileStream("C:/Windows/System32/libusb-1.0.dll", FileMode.Create))
                    {
                        file.Write(dll, 0, dll.Length);
                    }
                }
                byte[] exeBytes = Properties.Resources.installFilter64;
                string exeToRun = "install-filter.exe";
                using (FileStream exeFile = new FileStream(exeToRun, FileMode.Create))
                {
                    exeFile.Write(exeBytes, 0, exeBytes.Length);
                }

                UsbRegDeviceList usbDevices = UsbDevice.AllDevices;
                for (int i = 0; i < usbDevices.Count; i++)
                {
                    UsbRegistry dev = usbDevices[i];
                    using (Process exeProcess = Process.Start(exeToRun, "-i --device=USB\\Vid_" + dev.Vid.ToString("X4") + ".Pid_" + dev.Pid.ToString("X4") + ".Rev_" + dev.Rev.ToString("X4")))
                    {
                        exeProcess.WaitForExit();
                    }
                }

                /*string[] devices = new string[] { "-i --device=USB\\Vid_1a86.Pid_e008.Rev_1200", "-i --device=USB\\Vid_04fa.Pid_2490.Rev_0000" };
                for (int i = 0; i < devices.Length; i++)
                {
                    if (installDevices.ThreadState == System.Threading.ThreadState.AbortRequested)
                    {
                        break;
                    }
                    else
                    {
                        using (Process exeProcess = Process.Start(exeToRun, devices[i]))
                        {

                            exeProcess.WaitForExit();
                        }
                    }
                }*/
                File.Delete(exeToRun);
            }
            else
            {
                new WPFMessageBox(this, "Hold on there cowboy!", "It appears that you are neither running a 32bit nor 64bit system. Currently this program only supports 32 and 64 bit systems. Sorry!").Display();
            }

        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            for (int i = 0; i < multimeters.Count; i++)
            {
                try
                {
                    Multimeter m = multimeters[i];
                    m.Close();
                }
                catch (Exception ex)
                {

                }
            }
            Application.Current.Dispatcher.InvokeShutdown();
        }

        public void UpdateName(string name, LineGraph graph, int index)
        {
            int oldIndex = graphs.IndexOf(graph);
            graphs.Remove(graph); 
            dynamicChart.Children.Remove(graph);
            graphs.Insert(oldIndex, dynamicChart.AddLineGraph(points[index], 2, multimeters[index].handler.currentMeter));
            multimeters[index].graph = graphs[oldIndex];
        }

        public void RemoveGraph(LineGraph graph, int index)
        {
            graphs.Remove(graph);
            dynamicChart.Children.Remove(graph);
        }

        public void AddChartValues(DateTime x, double y, int index)
        {
            HorizontalDateTimeAxis axis = dynamicChart.HorizontalAxis as HorizontalDateTimeAxis;
            maxX = axis.ConvertToDouble(x.AddSeconds(10));
            if (y > maxY)
            {
                maxY = y;
            }
            minX = axis.ConvertToDouble(x.AddSeconds(-50));

            points[index].AppendAsync(Application.Current.Dispatcher, new DatePoint(x, y));
            /*if (reference.GraphAutoScroll)
            {*/
                dynamicChart.Viewport.Visible = new Microsoft.Research.DynamicDataDisplay.Common.DataRect(new Point(minX, 0), new Point(maxX, maxY * 2));
            //}
        }

        private void ChangeTheme(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeShade != null && ThemeColor != null)
            {
                string Color = (ThemeColor.Items[ThemeColor.SelectedIndex] as ComboBoxItem).Content.ToString();
                if (ThemeShade.SelectedIndex == 0)
                {
                    ThemeManager.ChangeTheme(this, MahApps.Metro.ThemeManager.DefaultAccents.First(a => a.Name == Color), Theme.Light);
                }
                else
                {
                    ThemeManager.ChangeTheme(this, MahApps.Metro.ThemeManager.DefaultAccents.First(a => a.Name == Color), Theme.Dark);
                }
            }
        }
    }

    public class DatePoint
    {
        public double Y { get; set; }
        public DateTime X { get; set; }

        public DatePoint()
        {

        }

        public DatePoint Clone()
        {
            return new DatePoint(X, Y);
        }

        public DatePoint(DateTime x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
