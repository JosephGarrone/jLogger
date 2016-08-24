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

namespace jLogger
{
    /// <summary>
    /// Interaction logic for WPFPrompt.xaml
    /// </summary>
    public partial class WPFMessageBox : MetroWindow, INotifyPropertyChanged
    {
        private String caption;
        public String Caption { get { return caption; } set { caption = value; OnPropertyChanged("Caption"); } }
        private String negativeText;
        public String NegativeText { get { return negativeText; } set { negativeText = value; OnPropertyChanged("NegativeText"); } }
        private String affirmativeText;
        public String AffirmativeText { get { return affirmativeText; } set { affirmativeText = value; OnPropertyChanged("AffirmativeText"); } }
        private String message;
        public String Message { get { return message; } set { message = value; OnPropertyChanged("Message"); } }
        private String result;
        public String Result { get { return result; } set { result = value; OnPropertyChanged("Result"); } }

        public MessageType Type;

        public WPFMessageBox()
        {
            InitializeComponent();
        }

        public WPFMessageBox(Window Owner, String Title, String Message, String AffirmativeText = "OK", String NegativeText = "Cancel", MessageType Type = MessageType.Alert)
        {
            InitializeComponent();

            this.Caption = Title;
            this.AffirmativeText = AffirmativeText;
            this.NegativeText = NegativeText;
            this.Message = Message;

            this.Type = Type;
            this.Owner = Owner;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(String prop)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }

        public void Display()
        {
            switch (Type)
            {
                case MessageType.Alert:
                    Negative.Visibility = System.Windows.Visibility.Collapsed;
                    Input.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case MessageType.Confirm:
                    Input.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case MessageType.Prompt:

                    break;
            }
            ShowDialog();
        }

        private void AffirmativeClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void NegativeClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Result = null;
            this.Close();
        }
    }

    public enum MessageType
    {
        Alert,
        Prompt,
        Confirm
    }
}
