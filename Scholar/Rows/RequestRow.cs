using System;

namespace Scholar.Rows
{
    public class RequestRow
    {
        public Guid SessionId { get; set; }
        public string Search { get; set; }
        public string ProcessedPercent { get; set; }
        public int ProcessedPages { get; set; }
        public int PageLimit { get; set; }
        public int Results { get; set; }
        public string StartTime { get; set; }

        public bool CanStop
        {
            get { return (PageLimit / 100) - ProcessedPages > 1; }
        }
    }
}
