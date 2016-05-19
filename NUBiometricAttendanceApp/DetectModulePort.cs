using System;
using System.IO.Ports;
using System.Threading;

/*
 Name:		DetectModulePort.cs
 Created:	4/19/2016 01:30:51 PM
 Author:	Siddhesh Nachane
*/

namespace NUBiometricAttendanceApp
{
    class DetectModulePort
    {
        static SerialPort currentPort;

        /// <summary>
        ///     Writes bytes 0x55 and 0xAA to every Serial Port.
        ///     Returns the one which reponds with "NUBiometric"
        /// </summary>
        ///      
        /// <returns>
        ///     String : ModulePort
        /// </returns>
    
        public static string DetectModule()
        {
            try
            {
                string attachedPort = String.Empty;
                string[] ports = SerialPort.GetPortNames();
                foreach (string port in ports)
                {
                    currentPort = new SerialPort(port, 9600);
                    if (DetectPort())
                    {
                        attachedPort = port;
                        break;
                    }
                }

                return attachedPort;
            }
            catch (Exception ex)
            {
                return String.Empty;
            }
        }

        private static bool DetectPort()
        {
            try
            {
                string returnMessage = String.Empty;
                byte[] buffer = new byte[2];
                buffer[0] = 0x55;
                buffer[1] = 0xAA;

                currentPort.Open();
                currentPort.Write(buffer, 0, buffer.Length);
                Thread.Sleep(1000);

                if (currentPort.BytesToRead > 0)
                    returnMessage = currentPort.ReadLine();

                currentPort.Close();

                if (returnMessage.Contains("NUBiometric"))
                {
                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
