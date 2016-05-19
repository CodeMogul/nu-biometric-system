using System;
using System.Collections.Generic;
using System.Data.SqlServerCe;
using System.IO.Ports;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Linq;
using System.Windows.Controls;
using Excel = Microsoft.Office.Interop.Excel;
using System.Text;
using System.IO;
using System.Windows.Threading;
using System.Diagnostics;

/*
 Name:		NUBiometricAttendanceApp.MainWindow.xaml.cs
 Created:	4/19/2016 01:30:51 PM
 Author:	Siddhesh Nachane (NIIT University)
*/

// Summary :
//      This App interacts with the Fingerprint Module as well as
//      generates Excel Sheets similar to those uploadable to NU-ERP
//      Terminologies :
//          1. Fingerprint Module (Arduino Nano + GT-511C3) => FPM
//          2. Fingerprint Template (Data of 504 bytes) => FPT
//          3. Fingerprint Sensor (GT-511C3) => FPS

//  Technical Specs:
//      This App uses SQL Server Compact Edition 4.0 to Locally Store Data (Student_Data.sdf)
//      Communication Baudrate with Arduino : 9600bps


namespace NUBiometricAttendanceApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Command Codes are sent from App to FPM
        enum CommandCode : byte
        {
            ATTENDANCE_REQ = 0x11,

            FPT_TRANSFER_REQ = 0x21,

            REGISTER_REQ = 0x30
        }

        // FPM responds using any of the bwlow Reponse Codes
        enum ResponseCode : byte
        {
            TRANSFERING_ATTENDANCE = 0X12,
            NO_ATTENDANCE_TO_TRANSFER = 0x13,
      
            START_FPT_TRANSFER = 0x22,
            FPT_TRANSFER_FAILED = 0X23,

            PRESS_FINGER = 0x31,
            REMOVE_FINGER = 0x32,

            ENROLL_SUCCESS = 0x41,
            ENROLL_FAILED = 0x42,	

            FPS_ERR = 0x65,		   
            ERROR = 0x66 
        }

        // Global variables (Scope : class MainWindow)
        String ModulePort;
        SerialPort myPort;

        List<FingerPrintTemplate> fingerPrintTemplates;
        List<int[]> classes;                                    //stores integer IDs (Serial Numbers) of Students of different classes 
        List<TimeTable_Slot> timeTable;
        

        byte[] transferRequest;
        int templateBeingWritten;

        public MainWindow()
        {
           InitializeComponent();
        }

        // Window Onload Initializer
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            checkConnectionStatus();
            fillTimeTable();
            createDirectories();
        }

        /// <summary>
        /// Checks if FPM is Connected using Detect Module Method
        /// </summary>
        bool checkConnectionStatus()
        {
            ModulePort = NUBiometricAttendanceApp.DetectModulePort.DetectModule();
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

        /// <summary>
        /// Creates Directories for each Course, Batch and Lecture Type taught by the Faculty.
        /// Main Directory : NU_Biometric in User's Documents directory.
        /// </summary>
        private void createDirectories()
        {
            string userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string NUBioDirectory = userDirectory + "\\Documents\\NU_Biometric";

            if (!System.IO.Directory.Exists(NUBioDirectory))
            {
                try
                {
                    SqlCeConnection con = new SqlCeConnection(@"Data Source=|DataDirectory|\Student_Data.sdf");
                    con.Open();

                    string query = "SELECT DISTINCT COURSE_CODE,BATCH,LECTURE_TYPE FROM TIME_TABLE";
                    SqlCeCommand comm = new SqlCeCommand(query, con);
                    SqlCeDataReader reader = comm.ExecuteReader();
                    while (reader.Read())
                    {
                        string course = reader.GetString(0);
                        string batch = reader.GetString(1);
                        string lec_type = reader.GetString(2);
                        string directory = NUBioDirectory + '\\' + course + '\\' + batch + '_' + lec_type;
                        if (!System.IO.Directory.Exists(directory))
                            System.IO.Directory.CreateDirectory(directory);
                    }
                }

                catch (SqlCeException _sqlexception)
                {
                    MessageBox.Show("Sorry the following Error Occured : " + _sqlexception.Message);
                }
            }
        }

        /// <summary>
        ///     Fills Grid (timeTableGrid) with the Faculty's Time Table.    
        /// </summary>
        private void fillTimeTable()
        {
            // class TimeTable_Slot defined in Parent Namespace
            timeTable = new List<TimeTable_Slot>();

            try
            {
                SqlCeConnection con = new SqlCeConnection(@"Data Source=|DataDirectory|\Student_Data.sdf");
                con.Open();

                SqlCeCommand comm = new SqlCeCommand("SELECT DAY_OF_WEEK, PERIOD, COURSE_CODE, BATCH, LECTURE_TYPE FROM TIME_TABLE ORDER BY DAY_OF_WEEK, PERIOD", con);
                SqlCeDataReader reader = comm.ExecuteReader();
                while (reader.Read())
                {
                    int day = reader.GetInt32(0);
                    int period = reader.GetInt32(1);
                    string course = reader.GetString(2);
                    string batch = reader.GetString(3);
                    string lectureType = reader.GetString(4);

                    timeTable.Add(new TimeTable_Slot() { Day = day, Period = period, Course = course, Batch = batch, LectureType = lectureType });
                }
                con.Close();
            }

            catch (SqlCeException _sqlexception)
            {
                MessageBox.Show("Sorry the following Error Occured : " + _sqlexception.Message);
            }

            if(timeTable.Count > 0)
            {
                foreach (var slot in timeTable)
                {
                    slot.chckbox = new CheckBox();
                    slot.chckbox.HorizontalAlignment = HorizontalAlignment.Center;
                    slot.chckbox.VerticalAlignment = VerticalAlignment.Center;

                    var txtblock = new TextBlock();
                    txtblock.TextWrapping = TextWrapping.Wrap;
                    txtblock.Text = slot.Course + System.Environment.NewLine + slot.Batch + " " + slot.LectureType;

                    slot.chckbox.Content = txtblock;
                    slot.chckbox.Foreground = new SolidColorBrush(Colors.Black);

                    slot.chckbox.BorderBrush = new SolidColorBrush(Colors.Black);
                    slot.chckbox.BorderThickness = new Thickness(2);
                    slot.chckbox.SetValue(Grid.ColumnProperty, slot.Period);
                    slot.chckbox.SetValue(Grid.RowProperty, slot.Day);
                    timeTableGrid.Children.Add(slot.chckbox);
                }
            }
        }

        /// <summary>
        ///     Sends command to FPM to begin Enrolment of Faculty at Location = 0 of FPS.
        ///     And adds register Event Handler to DataRecieved event of Serial Port
        /// </summary>
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            if (checkConnectionStatus())
            {
                transferRequest = new byte[2];
                transferRequest[0] = (byte)CommandCode.REGISTER_REQ;
                transferRequest[1] = 0x00;

                myPort = new SerialPort(ModulePort, 9600);
                myPort.Open();
                myPort.DataReceived += register_EventHandler;

                myPort.Write(transferRequest, 0, transferRequest.Length);
            }
        }

        /// <summary>
        ///     Changes connStatuBlock's Message according to responses from FPM.
        ///     Event raised whenever a Byte is recieved
        /// </summary>
        /// <param name="sender">Serial Port which flagged the event</param>
        /// <param name="args">Serial Data Recieved Event Arguements</param>
        void register_EventHandler(object sender, SerialDataReceivedEventArgs args)
        {
            SerialPort port = sender as SerialPort;
            byte rx_data = (byte)port.ReadByte();

            string msg = String.Empty;
            Action act = () => { ConnStatusBlock.Text = msg; };

            if (rx_data == (byte)ResponseCode.PRESS_FINGER)
            {
                msg = "Press Your Finger";
                ConnStatusBlock.Dispatcher.Invoke(act);
            }

            if (rx_data == (byte)ResponseCode.REMOVE_FINGER)
            {
                msg = "Remove Finger";
                ConnStatusBlock.Dispatcher.Invoke(act);
            }

            if (rx_data == (byte)ResponseCode.ENROLL_SUCCESS)
            {
                msg = "Enrolment Succesful";
                ConnStatusBlock.Dispatcher.Invoke(act);
                port.Close();
            }

            if (rx_data == (byte)ResponseCode.ENROLL_FAILED)
            {
                msg = "Enrolment Failed!! ";
                ConnStatusBlock.Dispatcher.Invoke(act);
                port.Close();
            }

            if (rx_data == (byte)ResponseCode.ERROR)
            {
                msg = "Fingerprint Scanner Error ";
                ConnStatusBlock.Dispatcher.Invoke(act);
                port.Close();
            }
        }

        /// <summary>
        ///     Sends command to FPM to begin transfer FPT data of first student.
        ///     And adds transferFingerprintTemplate Event Handler to DataRecieved event of Serial Port
        /// </summary>
        private void Transfer_MyData_Click(object sender, RoutedEventArgs e)
        {
            if (checkConnectionStatus() && getFingerPrintTemplates())
            {
                    templateBeingWritten = 0;

                    transferRequest = new byte[2];
                    transferRequest[0] = (byte)CommandCode.FPT_TRANSFER_REQ;
                    transferRequest[1] = (byte)fingerPrintTemplates[templateBeingWritten].Serial;

                    myPort = new SerialPort(ModulePort, 9600);
                    myPort.Open();
                    myPort.DataReceived += transferFingerprintTemplate_EventHandler;

                    myPort.Write(transferRequest, 0, transferRequest.Length);
            }
        }

        /// <summary>
        ///     Retrives Student Serial, FPT from database into List : fingerprintTemplates
        /// </summary>
        /// <returns>
        /// True => if retrieved count > 0
        /// False => Otherwise
        /// </returns>
        bool getFingerPrintTemplates()
        {
            fingerPrintTemplates = new List<FingerPrintTemplate>();
            byte[] template = new byte[504];

            try
            {
                SqlCeConnection con = new SqlCeConnection(@"Data Source=|DataDirectory|\Student_Data.sdf");
                con.Open();

                SqlCeCommand comm = new SqlCeCommand("SELECT * FROM STUD_DATA", con);
                SqlCeDataReader reader = comm.ExecuteReader();
                while (reader.Read())
                {
                    int serial = reader.GetInt32(0);
                    template = (byte[])reader.GetValue(2);
                    fingerPrintTemplates.Add(new FingerPrintTemplate() { Serial = serial, Template = template });
                }
                if (fingerPrintTemplates.Count > 0)
                    return true;
                else
                    return false;
            }

            catch (SqlCeException _sqlexception)
            {
                MessageBox.Show("Sorry the following Error Occured : " + _sqlexception.Message);
                return false;
            }
        }

        /// <summary>
        ///     Transfers FPT one at a time according to responses from FPM.
        ///     Changes connStatuBlock's Message according to responses from FPM. 
        ///     Event raised whenever a Byte is recieved
        /// </summary>
        /// <param name="sender">Serial Port which flagged the event</param>
        /// <param name="args">Serial Data Recieved Event Arguements</param>
        void transferFingerprintTemplate_EventHandler(object sender, SerialDataReceivedEventArgs args)
        {
            SerialPort port = sender as SerialPort;
            byte rx_data = (byte)port.ReadByte();

            string msg = String.Empty;
            Action act = () => { ConnStatusBlock.Text = msg; };

            if (rx_data == (byte)ResponseCode.START_FPT_TRANSFER)
            {
                port.Write(fingerPrintTemplates[templateBeingWritten].Template, 0, fingerPrintTemplates[templateBeingWritten].Template.Length);
                Thread.Sleep(200);

                templateBeingWritten++;
                
                if (templateBeingWritten < fingerPrintTemplates.Count)
                {
                    transferRequest[1] = (byte)fingerPrintTemplates[templateBeingWritten].Serial;
                    transferRequest[0] = (byte)CommandCode.FPT_TRANSFER_REQ;
                    port.Write(transferRequest, 0, transferRequest.Length);
                }
                else
                {
                    fingerPrintTemplates = null;
                    port.Close();
                    msg = "Transfer Successful.";
                    ConnStatusBlock.Dispatcher.Invoke(act);
                }
            }
            
            if(rx_data == (byte)ResponseCode.FPT_TRANSFER_FAILED)
            {
                fingerPrintTemplates = null;
                port.Close();
                msg = "Erorr!! Transfer Failed..";
                ConnStatusBlock.Dispatcher.Invoke(act);
            }
        }

        /// <summary>
        ///     Opens NU_Biometric Directory to which Faculty must Add his/her Excel Sheets in respective directories
        /// </summary>
        private void Add_Excel_Click(object sender, RoutedEventArgs e)
        {
            string userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string NUBioDirectory = userDirectory + "\\Documents\\NU_Biometric";
            Process.Start(NUBioDirectory);
        }

        /// <summary>
        ///     Sends Request to FPM to transfer Attendance IDs stored in it.
        ///     And adds transferAttendance Event Handler to DataRecieved event of Serial Port     
        /// </summary>
        private void Get_Attendance_Click(object sender, RoutedEventArgs e)
        {
            if (checkConnectionStatus())
            {
                transferRequest = new byte[2];
                transferRequest[0] = (byte)CommandCode.ATTENDANCE_REQ;
                transferRequest[1] = 0x00;

                myPort = new SerialPort(ModulePort, 9600);
                myPort.Open();
                myPort.DataReceived += transferAttendance_EventHandler;

                myPort.Write(transferRequest, 0, transferRequest.Length);
            }
        }

        /// <summary>
        ///     Recieves Attendance IDs into a Byte Array fro FPM.
        ///     Changes connStatuBlock's Message according to responses from FPM. 
        ///     Event raised whenever a Byte is recieved
        /// </summary>
        /// <param name="sender">Serial Port which flagged the event</param>
        /// <param name="args">Serial Data Recieved Event Arguements</param>
        void transferAttendance_EventHandler(object sender, SerialDataReceivedEventArgs args)
        {
            SerialPort port = sender as SerialPort;
            byte rx_data = (byte)port.ReadByte();

            string msg = String.Empty;
            Action act = () => { Class_Count.Text = msg; };

            if (rx_data == (byte)ResponseCode.TRANSFERING_ATTENDANCE)
            {
                Thread.Sleep(50);
                rx_data = (byte)myPort.ReadByte();

                int noOfRecords = (int)rx_data;
                byte[] byteStudentID = new byte[noOfRecords]; ;

                while (port.BytesToRead < noOfRecords)
                    Thread.Sleep(50);
                    //System.Threading.Tasks.Task.Delay(50);

                port.Read(byteStudentID, 0, byteStudentID.Length);
                port.Close();

                splitAttendanceToClasses(byteStudentID);

                msg = "No. of Classes Left to be Marked : " + classes.Count;
                Class_Count.Dispatcher.Invoke(act);
            }

            if(rx_data == (byte)ResponseCode.NO_ATTENDANCE_TO_TRANSFER)
            {             
                msg = "Module Empty!! No Attendance to Transfer!";
                Class_Count.Dispatcher.Invoke(act);
            }
        }

        /// <summary>
        ///     Byte array is converted to a integer array
        ///     Split into multiple Arrays, each containing ID's of Students of one class
        ///     Faculty Scans his/her print at the START AND END of EVERY class.
        ///     Thus "0" marks the start of one class and next "0" marks the end of that class
        ///     Ex: Input : 0,1,5,8,9,0,0,2,4,7,0,0,6,11,15,5,0
        ///     Ouptut = List of Three arrays => 1,5,8,9 && 2,4,7 && 6,11,15,5
        /// </summary>
        /// <param name="_studentIDs">Byte array revcieved from FPM</param>
        private void splitAttendanceToClasses(byte[] _studentIDs)
        {
            int prev = 1;
            classes = new List<int[]>();

            int[] intStudentID = Array.ConvertAll(_studentIDs, c => (int)c);

            for (int i = 1; i < intStudentID.Length; i++)
            {
                if (intStudentID[i] == 0)
                {
                    int[] arr = new int[i - prev];
                    Array.Copy(intStudentID, prev, arr, 0, (i - prev));
                    prev = i + 2;
                    i++;
                    classes.Add(arr.Distinct().ToArray());
                }
            }
        }

        /// <summary>
        ///     Calls CreateExcel Method for each class in classes as a new Thread
        /// </summary>
        private void Mark_Attendance_Click(object sender, RoutedEventArgs e)
        {
            progressBar.Value += 5;
            
            if (classes != null || classes.Count != 0)
            {
                foreach (var slot in timeTable)
                {
                    slot.check = (bool)slot.chckbox.IsChecked;
                }
                Thread thread = new Thread(new ThreadStart(CreateExcel));
                thread.Start();
            }

            else
                MessageBox.Show("No Attendances to Mark");

        }

        /// <summary>
        ///     Thread to Mark Attendances in Excel Sheet and Save in NU_Biometric Directory
        /// </summary>
        private void CreateExcel()
        {
            string userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string NUBioDirectory = userDirectory + "\\Documents\\NU_Biometric\\";
            float progressClass = (float)95 / classes.Count;

            foreach (var slot in timeTable)
            {
                //if ((bool)slot.chckbox.IsChecked)
                if (slot.check)
                {
                    try
                    {
                        List<string> enrolNos = GetEnrolmentNoFromDB(classes[0]);
                    float progressStudent = (float)progressClass / enrolNos.Count;

                    string path = NUBioDirectory + slot.Course + '\\' + slot.Batch + '_' + slot.LectureType + '\\';
                    string fileName = Directory.GetFiles(path, (slot.Course + '*'))[0];

                    Excel.Application xlApp = new Excel.Application();
                    Excel.Workbook xlWorkbook = xlApp.Workbooks.Open(fileName);
                    Excel._Worksheet xlWorksheet = xlWorkbook.Sheets[1];
                    Excel.Range xlRange = xlWorksheet.UsedRange;

                    int rowCount = xlRange.Rows.Count;

                    int diff = (int)DateTime.Now.DayOfWeek - slot.Day;
                    string classDate = DateTime.Now.Date.AddDays(-diff).ToString("dd-MMM-yy");
                    xlWorksheet.Cells[1, 6] = classDate;
                    xlWorksheet.Cells[2, 6] = slot.Period;

                    string[] tsReqs = new string[rowCount];


                    foreach (var id in enrolNos)
                    {
                        for (int i = 1; i <= rowCount; i++)
                        {
                            string str = xlRange.Cells[i, 4].Text;
                            if (id.Equals(str))
                            {
                                xlWorksheet.Cells[i, 6] = "P";
                                this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
                                {
                                    this.progressBar.Value += progressStudent; 
                                }));
                                break;
                            }
                        }
                    }

                    for (int i = 3; i <= rowCount; i++)
                    {
                        string str = xlRange.Cells[i, 6].Text;
                        if (!(str.Equals("P")))
                        {
                            xlWorksheet.Cells[i, 6] = "A";
                        }
                    }

                    StringBuilder saveLocation = new StringBuilder();
                    saveLocation.Append(NUBioDirectory);
                    saveLocation.Append(@"Attendance\");
                    saveLocation.Append(classDate);
                    saveLocation.Append('_');
                    saveLocation.Append(slot.Batch);
                    saveLocation.Append('_');
                    saveLocation.Append(slot.LectureType);

                    if (!System.IO.Directory.Exists(saveLocation.ToString()))
                        System.IO.Directory.CreateDirectory(saveLocation.ToString());

                    saveLocation.Append(@"\");

                    xlWorkbook.SaveAs(saveLocation.ToString() + fileName.Remove(0,path.Length));
                    xlWorkbook.Close();
                    xlApp.Quit();
                    classes.RemoveAt(0);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
            }
            this.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate()
            {
                this.progressBar.Value = 0;
                this.Class_Count.Text = "No. of Classes Left to be Marked : " + classes.Count;
            }));

        }

        /// <summary>
        ///     Gets List of Enrolment Numbers of Students Corresponding to Serial Numbers
        /// </summary>
        /// <param name="_serialNos">Array of Serial Numbers (ID) of students of one class(period/batch)</param>
        /// <returns>List of Enrolment Numbers(string)</returns>
        private List<string> GetEnrolmentNoFromDB(int[] _serialNos)
        {            
            StringBuilder inClause = new StringBuilder();
            List<string> enrolNos = new List<string>();
 
            foreach (var student in _serialNos)
            {
                inClause.Append('\'');
                inClause.Append(student);
                inClause.Append('\'');
                inClause.Append(',');
            }
            inClause.Length -= 1;
            
            try
            {
                SqlCeConnection con = new SqlCeConnection(@"Data Source=|DataDirectory|\Student_Data.sdf");
                con.Open();

                string query = "SELECT ADMISSION_NO FROM STUD_DATA WHERE STUD_DATA.SERIAL IN (" + inClause.ToString() + ')';
                SqlCeCommand comm = new SqlCeCommand(query, con);
                SqlCeDataReader reader = comm.ExecuteReader();
                while (reader.Read())
                {
                    string enrolNo = reader.GetString(0);
                    enrolNos.Add(enrolNo);
                }
                con.Close();
            }

            catch (SqlCeException _sqlexception)
            {
                MessageBox.Show("Sorry the following Error Occured : " + _sqlexception.Message);
                return null;
            }

            return enrolNos;
        }
    }
}
