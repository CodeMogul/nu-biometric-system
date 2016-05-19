using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace NUBiometricAttendanceApp
{
    /// <summary>
    ///     Each Slot represents a period in faculty's Timetable
    ///     Considers Day and Period Number are stored as integers in Database
    ///     Day: Sunday = 0 ... Saturday = 6
    ///     Period : 8:30 - 9:30 = 1 ... 4:30 - 5:30 = 9
    /// </summary>
    class TimeTable_Slot
    {
        public int Day { get; set; }
        public int Period { get; set; }
        public string Course { get; set; }
        public string Batch { get; set; }
        public string LectureType { get; set; }
        public CheckBox chckbox { get; set; }
        public bool check { get; set; }
    }
}
