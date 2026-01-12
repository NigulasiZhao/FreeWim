using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FreeWim.Models.PmisAndZentao.Dto
{
    public class CommitOvertimeWorkOutput
    {
        public string? CheckInRule { get; set; }
        public DateTime AttendanceDate { get; set; }
        public int IsNextDayRest { get; set; }
        public string? Id { get; set; }
        public string? Project { get; set; }
        public string? TaskName { get; set; }
        public string? TaskDesc { get; set; }
        public string? ProjectCode { get; set; }
        public DateTime EstStarted { get; set; }
    }
}