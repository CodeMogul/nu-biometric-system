
namespace NUBiometricAttendanceApp
{
    /// <summary>
    ///     Holds a Student's data : Serial number, FPT
    /// </summary>
    class FingerPrintTemplate
    {
        public int Serial { get; set; } 
        public byte[] Template { get; set; }
    }
}
