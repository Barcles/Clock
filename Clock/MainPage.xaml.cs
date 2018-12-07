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

        string received = "";
        bool DataState = false; //Setting state for debugging button
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

            // Moving 30 degrees for each hour + 0.5 degrees to account for minute hand moving
            hourHand.Angle = DateTime.Now.Hour * 30 + DateTime.Now.Minute * 0.5;    
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
                received += dataReaderObject.ReadString(bytesRead);  // Allows user to see data being received from the stream
                // txtRecieved.Text = recived;
                if (received[0] == '#')
                {
                    if (received.Length > 3)
                    {
                        if (received[2] == '#')
                        {
                            //txtRecieved.Text = recived;
                            if (received.Length > 45 )//&& received.Length < 45)
                            {
                                txtRecieved.Text = received + txtRecieved.Text;  // Adds new text to the top of received buffer
                                txtSend.Text = received.Substring(41,2);    // Temperature extracted from string sent by arduino
                                txtSend.Text += " ";
                                txtSend.Text += received.Substring(43, 2);  // Humidity extracted from string sent by arduino

                                var tempMath = 20/3;
                                var tempPos = Convert.ToInt16(received.Substring(41, 2));   //Converting received temperature string to int
                                TempRect.Height = tempPos * tempMath;   //Aligns temperature with thermometer
                                tempRead.Text = string.Format("{0}°C", tempPos);    //Prints temperature reading to textblock

                                var angle0 = 193;   //Zero point for the humidity display
                                var math = (326 / 100); //326 degrees (useable circle / 100 points on circle)
                                var HumidPos = Convert.ToInt16(received.Substring(43, 2));  //Converting received humidity string to int
                                //Converting humidity int to angle for the needle, 163 degrees is half of useable circle
                                HumidityHand.Angle = (HumidPos) * math - 163;
                                HumidRead.Text = string.Format("{0}%", HumidPos);  //Prints humidity reading to textblock

                                // add parse code 
                                //txtPacketNum.Text = received.Substring(3, 3);
                                //txtAN0.Text = received.Substring(6, 4);
                                //txtAN1.Text = received.Substring(10, 4);
                                //txtAN2.Text = received.Substring(14, 4);
                                //txtAN3.Text = received.Substring(18, 4);
                                //txtAN4.Text = received.Substring(22, 4);
                                //txtAN5.Text = received.Substring(26, 4);
                                //txtBinOut.Text = received.Substring(30, 8);
                                //txtCalChkSum.Text = received.Substring(38, 3);

                                //                for (int i = 3; i < 38; i++)
                                //                {
                                //                    calChkSum += (byte)received[i];
                                //                }
                                //                txtCalChkSum.Text = Convert.ToString(calChkSum);
                                //                int an0 = Convert.ToInt32(received.Substring(6, 4));
                                //                int an1 = Convert.ToInt32(received.Substring(10, 4));
                                //                int an2 = Convert.ToInt32(received.Substring(14, 4));
                                //                int an3 = Convert.ToInt32(received.Substring(18, 4));
                                //                int an4 = Convert.ToInt32(received.Substring(22, 4));
                                //                int an5 = Convert.ToInt32(received.Substring(26, 4));

                                //                int recChkSum = Convert.ToInt32(received.Substring(38, 3));
                                //                calChkSum %= 1000;
                                //                if (recChkSum == calChkSum) // Packet is sent
                                //                {

                                //                }
                                received = "";   // clears out received buffer
                            }
                        }
                        else
                        {
                            received = "";   // clears out received buffer
                        }

                    }

                }
                else
                {
                    received = "";   // clears out received buffer
                }
            }
        }

        private async void ButtonWrite_Click(object sender, RoutedEventArgs e)  // Sends out data on button click
        {
            if (serialPort != null) // Check to see if there is something in serial port
            {
                var dataPacket = txtSend.Text.ToString();   // Converts data from text block to string
                dataWriterObject = new DataWriter(serialPort.OutputStream);
                await SendPacket(dataPacket);

                if (dataWriterObject != null)   // Handles situation if problem arrises
                {
                    dataWriterObject.DetachStream();
                    dataWriterObject = null;
                }

            }
        }

        private async Task SendPacket(string value)
        {
            var dataPacket = value;
            Task<UInt32> storeAsyncTask;
            if (dataPacket.Length != 0) // If datapacket length is not = 0, then we can send it
            {
                dataWriterObject.WriteString(dataPacket);   // Writes datapacket to a string

                storeAsyncTask = dataWriterObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask; // Variable created to see how many bytes are written
                if (bytesWritten > 0)   // If there are available writes
                {
                    txtMessage.Text = "Values Set Correctly";   // Reports that code was executed
                }
            }
            else   // Error Reporting
            {
                txtMessage.Text = "No Value Sent";
            }
        }

        private void DataReceived_Click(object sender, RoutedEventArgs e)   //Hides debugging menu
        {
            DataState = !DataState;
            if(DataState == true)
            {
                txtRecieved.Visibility = Visibility.Visible;
                txtSend.Visibility = Visibility.Visible;
                Packets.Visibility = Visibility.Visible;
            }
            else if (DataState == false)
            {
                txtRecieved.Visibility = Visibility.Collapsed;
                txtSend.Visibility = Visibility.Collapsed;
                Packets.Visibility = Visibility.Collapsed;
            }

        }
    }
}
