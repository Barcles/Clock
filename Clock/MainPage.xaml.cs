using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;   //Added as per serial lab
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading; //Added as per serial lab
using System.Threading.Tasks;   //Added as per serial lab
using Windows.Devices.Enumeration;  //Added as per serial lab
using Windows.Devices.SerialCommunication;  //Added as per serial lab
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;  //Added as per serial lab
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

// References:
// https://www.c-sharpcorner.com/article/create-an-analog-clock-for-windows-10-universal-application/
// https://docs.microsoft.com/en-us/dotnet/framework/wpf/graphics-multimedia/transforms-overview

namespace Clock
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        DispatcherTimer timer = new DispatcherTimer();  // Initializing Dispatch Timer

//--------------------------------------------------------------------------------------------------
        private SerialDevice serialPort = null;
        DataWriter dataWriterObject = null;
        DataReader dataReaderObject = null;
        private ObservableCollection<DeviceInformation> listOfDevices;
        private CancellationTokenSource ReadCancellationTokenSource;

        string recived = "";
//--------------------------------------------------------------------------------------------------

        public MainPage()
        {
            this.InitializeComponent();
            timer.Interval = TimeSpan.FromSeconds(1);   // Sets the amount between timer ticks based on 1000ms
            timer.Tick += Timer_Tick;
            timer.Start();

            listOfDevices = new ObservableCollection<DeviceInformation>();
            ListAvailablePorts();
        }
        
        private void Timer_Tick(object sender, object e)    // Takes the time from Windows and sets the position of the hands
        {
            secondHand.Angle = DateTime.Now.Second * 6; // 60 positions on the clock for seconds --> 360/60 = 6 
            minuteHand.Angle = DateTime.Now.Minute * 6; // 60 positions on the clock for minutes --> 360/60 = 6
            hourHand.Angle = DateTime.Now.Hour * 30;    // 12 positions on the clock for hours --> 360/12 = 30
        }

        private async void ListAvailablePorts()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();  // Variable created to hold available devices
                var dis = await DeviceInformation.FindAllAsync(aqs);    // Waits for all devices to be aquired

                for (int i = 0; i < dis.Count; i++)
                {
                    listOfDevices.Add(dis[i]);  // Adds devices to list
                }

                lstSerialDevices.ItemsSource = listOfDevices;   // Populates the list

                lstSerialDevices.SelectedIndex = -1;    // Prevents index from pointing to something at start
            }
            catch (Exception ex)    // Error reporting
            {
                txtMessage.Text = ex.Message;
            }
        }

        private void ButtonConnectToDevice_Click(object sender, RoutedEventArgs e)  // Calls function when "connect to device" button is clicked
        {
            SerialPortConfiguration();
        }

        private async void SerialPortConfiguration()    // Takes selection from list box 
        {
            var selection = lstSerialDevices.SelectedItems;

            if (selection.Count <= 0)   // Check to make sure something is selected
            {
                txtMessage.Text = "Select an object for serial connection!";    // Error reporting if nothing is selected
                return;
            }

            DeviceInformation entry = (DeviceInformation)selection[0];
            try
            {
                serialPort = await SerialDevice.FromIdAsync(entry.Id);  // Choose device based on ID
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(1000);  // Write timeout
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);   // Read timeout
                serialPort.BaudRate = 115200;
                serialPort.Parity = SerialParity.None;
                serialPort.StopBits = SerialStopBitCount.One;
                serialPort.DataBits = 8;
                serialPort.Handshake = SerialHandshake.None;
                txtMessage.Text = "Serial Port Correctly Configured!";

                ReadCancellationTokenSource = new CancellationTokenSource();

                Listen();   // If there is a serial port device, function call Listen()
            }

            catch (Exception ex)    // Error reporting
            {
                txtMessage.Text = ex.Message;
            }
        }

        private async void Listen()
        {
            try
            {
                if (serialPort != null) // Check to see if there is serial port device
                {
                    dataReaderObject = new DataReader(serialPort.InputStream);

                    while (true)    // Infinite loop to read data continuously
                    {
                        await ReadData(ReadCancellationTokenSource.Token);  // Runs continuously in background
                    }
                }
            }
            catch (Exception ex)
            {
                txtMessage.Text = ex.Message;

                // if(ex.GetType.Name == "TaskCancelledException")
            }
            finally
            {

            }
        }

        private async Task ReadData(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            int calChkSum = 0;

            uint ReadBufferLength = 1;

            cancellationToken.ThrowIfCancellationRequested();

            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);

            UInt32 bytesRead = await loadAsyncTask;

            if (bytesRead > 0)
            {
                recived += dataReaderObject.ReadString(bytesRead);  // Allows user to see data being received from the stream
                // txtRecieved.Text = recived;
                if (recived[0] == '#')
                {
                    if (recived.Length > 3)
                    {
                        if (recived[2] == '#')
                        {
                            //txtRecieved.Text = recived;
                            if (recived.Length > 42)
                            {
                                txtRecieved.Text = recived + txtRecieved.Text;  // Adds new text to the top of received buffer
                                // add parse code 
                                txtPacketNum.Text = recived.Substring(3, 3);
                                txtAN0.Text = recived.Substring(6, 4);
                                txtAN1.Text = recived.Substring(10, 4);
                                txtAN2.Text = recived.Substring(14, 4);
                                txtAN3.Text = recived.Substring(18, 4);
                                txtAN4.Text = recived.Substring(22, 4);
                                txtAN5.Text = recived.Substring(26, 4);
                                txtBinOut.Text = recived.Substring(30, 8);
                                txtCalChkSum.Text = recived.Substring(38, 3);

                                for (int i = 3; i < 38; i++)
                                {
                                    calChkSum += (byte)recived[i];
                                }
                                txtCalChkSum.Text = Convert.ToString(calChkSum);
                                int an0 = Convert.ToInt32(recived.Substring(6, 4));
                                int an1 = Convert.ToInt32(recived.Substring(10, 4));
                                int an2 = Convert.ToInt32(recived.Substring(14, 4));
                                int an3 = Convert.ToInt32(recived.Substring(18, 4));
                                int an4 = Convert.ToInt32(recived.Substring(22, 4));
                                int an5 = Convert.ToInt32(recived.Substring(26, 4));

                                int recChkSum = Convert.ToInt32(recived.Substring(38, 3));
                                calChkSum %= 1000;
                                if (recChkSum == calChkSum) // Packet is sent
                                {

                                }
                                recived = "";   // clears out received buffer
                            }
                        }
                        else
                        {
                            recived = "";   // clears out received buffer
                        }

                    }

                }
                else
                {
                    recived = "";   // clears out received buffer
                }
            }
        }

        //private async void ButtonWrite_Click(object sender, RoutedEventArgs e)  // Sends out data on button click
        //{
        //    if (serialPort != null) // Check to see if there is something in serial port
        //    {
        //        var dataPacket = txtSend.Text.ToString();   // Converts data from text block to string
        //        dataWriterObject = new DataWriter(serialPort.OutputStream);
        //        await SendPacket(dataPacket);

        //        if (dataWriterObject != null)   // Handles situation if problem arrises
        //        {
        //            dataWriterObject.DetachStream();
        //            dataWriterObject = null;
        //        }

        //    }
        //}

        //private async Task SendPacket(string value)
        //{
        //    var dataPacket = value;
        //    Task<UInt32> storeAsyncTask;
        //    if (dataPacket.Length != 0) // If datapacket length is not = 0, then we can send it
        //    {
        //        dataWriterObject.WriteString(dataPacket);   // Writes datapacket to a string

        //        storeAsyncTask = dataWriterObject.StoreAsync().AsTask();

        //        UInt32 bytesWritten = await storeAsyncTask; // Variable created to see how many bytes are written
        //        if (bytesWritten > 0)   // If there are available writes
        //        {
        //            txtMessage.Text = "Values Set Correctly";   // Reports that code was executed
        //        }
        //    }
        //    else   // Error Reporting
        //    {
        //        txtMessage.Text = "No Value Sent";
        //    }
        //}
    }
}
