using System;
using System.Data;
using System.Data.SqlClient;
using System.IO.Ports;
using System.Windows;
using System.Windows.Media;

/*
 Name:		NUBiometricEnrolment.MainWindow.xaml.cs
 Created:	2/10/2016 03:15:34 AM
 Author:	Siddhesh Nachane (NIIT University)
*/

// Summary :
//      This App interacts with the Fingerprint Module for Enrolment Purpose
//      Terminologies :
//          1. Fingerprint Module (Arduino Nano + GT-511C3) => FPM
//          2. Fingerprint Template (Data of 504 bytes) => FPT
//          3. Fingerprint Sensor (GT-511C3) => FPS

//  Technical Specs:
//      This App uses SQL Server Local DB Store Data
//      Communication Baudrate with Arduino : 15200bps

//  Work Left :
//      Database has to Ported to SQL Server Compact Edition 4.0 (Add Required References)
//      Validation/Verification of Enrolment Number
//      Complete Makeover of UI
//      Error Handling / Try-Catch blocks wherever required

namespace NUBiometricEnrolment
{
   public partial class MainWindow : Window
    {
        String ModulePort;
        SerialPort myPort;
        enum CommunicationCodes : byte
        {
            //PC -> FPM (Command Code)
            START_ENROLL = 0x10,

            // FPM -> PC (Response Codes)
            ENROLL_SUCCESS = 0x09,      
            ENROLL_FAIL = 0x19,

            PRESS_FINGER = 0x03,
            REMOVE_FINGER = 0x13,

            NEXT_IS_TEMPLATE = 0x14,

            FPS_ERR = 0x39
        };

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            checkConnectionStatus();
        }

        /// <summary>
        /// Checks if FPM is Connected using Detect Module Method
        /// </summary>
        bool checkConnectionStatus()
        {
            ModulePort = NUBiometricEnrolment.DetectModulePort.DetectModule();
            if (ModulePort == String.Empty)
            {
                ConnStatusBlock.Text = "Module Not Found";
                ConnStatusBlock.Foreground = Brushes.Red;
                MessageBox.Show("Module Not Connected!");
                return false;
            }
            else
            {
                ConnStatusBlock.Text = "Module Connected";
                ConnStatusBlock.Foreground = Brushes.Green;
                return true;
            }
        }

        private void Enroll_Click(object sender, RoutedEventArgs e)
        {
            if(checkConnectionStatus())
                StartEnroll(ModulePort);
        }

        public void StartEnroll(String PortName)
        {
            myPort = new SerialPort(PortName, 115200);
            byte[] start = { (byte)CommunicationCodes.START_ENROLL };
            byte[] template = new byte[1000];

            myPort.Open();
            myPort.Write(start, 0, 1);
            myPort.DataReceived += OnScan;
        }

        void OnScan(object sender, SerialDataReceivedEventArgs args)
        {
            SerialPort port = sender as SerialPort;
            byte rx_data = (byte)port.ReadByte();
            byte[] template = new byte[504];

            string msg = String.Empty;
            Action act = () => { StatusBox.Text = msg; };

            if (rx_data == (byte)CommunicationCodes.PRESS_FINGER)
            {
                msg = "Press Your Finger..";
                StatusBox.Dispatcher.Invoke(act);
            }

            if (rx_data == (byte)CommunicationCodes.REMOVE_FINGER)
            {
                msg = "Remove Your Finger..";
                StatusBox.Dispatcher.Invoke(act);
            }

            if (rx_data == (byte)CommunicationCodes.NEXT_IS_TEMPLATE)                     // Check this Code :: What if 504 Bytes do not come 
                {
                    //msg = "Recieving Data.. Please Wait.. ";
                    //StatusBox.Dispatcher.Invoke(act);

                    //int wait = 0;

                    //while (port.BytesToRead < 504 && wait < 50)
                    //{
                    //    System.Threading.Tasks.Task.Delay(50);
                    //    wait++;
                    //}
                    //if (wait < 50)
                    //{
                    //    port.Read(template, 0, 504);
                    //}
                    //else
                    //{
                    //    msg = "Communication Error ";
                    //    StatusBox.Dispatcher.Invoke(act);
                    //    port.Close();
                    //}

                    while (port.BytesToRead < 504)
                        System.Threading.Tasks.Task.Delay(50);

                    port.Read(template, 0, 504);
                
                    InsertToSql(template);
                }

            if (rx_data == (byte)CommunicationCodes.ENROLL_SUCCESS)
            {
                msg = "Enrolment Succesful";
                StatusBox.Dispatcher.Invoke(act);
                port.Close();
            }

            if (rx_data == (byte)CommunicationCodes.ENROLL_FAIL)
            {
                msg = "Enrolment Failed!! ";
                StatusBox.Dispatcher.Invoke(act);
                port.Close();
            }

            if (rx_data == (byte)CommunicationCodes.FPS_ERR)
            {
                msg = "Fingerprint Scanner Error ";
                StatusBox.Dispatcher.Invoke(act);
                port.Close();
            }
        }

       /// <summary>
       /// Inserts Data(Enrolment_No, FPT) into SQL Server LocalDb
       /// </summary>
       /// <param name="fpt">Byte array containing FPT of Student</param>
        private void InsertToSql(byte[] fpt)
        {
            string Enrol_No = String.Empty;
            Action act = () => {Enrol_No = Enrolment_Number.Text;};

            try { 
                Enrolment_Number.Dispatcher.Invoke(act);
                var insertSQL = "INSERT INTO STUD_DATA(ADMISSION_NO, STUD_FPT) VALUES (@ADMISSION_NO, @STUD_FPT)";

                string connectionString = @"Data Source=(LocalDB)\v11.0;AttachDbFilename=C:\Users\Siddhesh\Documents\FPS.mdf;Integrated Security=True;Connect Timeout=30";

                using (var cn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand(insertSQL, cn))
                {
                    cn.Open();

                    cmd.Parameters.Add("@ADMISSION_NO", SqlDbType.NVarChar);
                    cmd.Parameters.Add("@STUD_FPT", SqlDbType.VarBinary,fpt.Length);

                    cmd.Parameters["@ADMISSION_NO"].Value = Enrol_No;
                    cmd.Parameters["@STUD_FPT"].Value = fpt;
                    cmd.ExecuteNonQuery();
                    cn.Close();
                }   
            }
            catch
            {}
       }
    }
}
