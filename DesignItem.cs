using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace DesignSheet
{
    public class DesignItem : INotifyPropertyChanged
    {
        private string _retail;
        private string _oe;
        private string _customer;
        private string _serialNumber;
        private string _dayDue;
        private DateTime _dateDue;
        private string _status;
        private int _qty;
        private string _whatIsIt;
        private string _po;
        private string _whatAreWeDoing;
        private string _parts;
        private string _shaftType;
        private string _priority;
        private string _lastUser;

        public string Retail
        {
            get => _retail;
            set { _retail = value; OnPropertyChanged(); }
        }

        public string OE
        {
            get => _oe;
            set { _oe = value; OnPropertyChanged(); }
        }

        public string Customer
        {
            get => _customer;
            set { _customer = value; OnPropertyChanged(); }
        }

        public string SerialNumber
        {
            get => _serialNumber;
            set { _serialNumber = value; OnPropertyChanged(); }
        }

        public string DayDue
        {
            get => _dayDue;
            set { _dayDue = value; OnPropertyChanged(); }
        }

        public DateTime DateDue
        {
            get => _dateDue;
            set { _dateDue = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public int Qty
        {
            get => _qty;
            set { _qty = value; OnPropertyChanged(); }
        }

        public string WhatIsIt
        {
            get => _whatIsIt;
            set { _whatIsIt = value; OnPropertyChanged(); }
        }

        public string PO
        {
            get => _po;
            set { _po = value; OnPropertyChanged(); }
        }

        public string WhatAreWeDoing
        {
            get => _whatAreWeDoing;
            set { _whatAreWeDoing = value; OnPropertyChanged(); }
        }

        public string Parts
        {
            get => _parts;
            set { _parts = value; OnPropertyChanged(); }
        }

        public string ShaftType
        {
            get => _shaftType;
            set { _shaftType = value; OnPropertyChanged(); }
        }

        public string Priority
        {
            get => _priority;
            set { _priority = value; OnPropertyChanged(); }
        }

        public string LastUser
        {
            get => _lastUser;
            set { _lastUser = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        // CSV header (keep in sync with ToCsvRow / FromCsvLine)
        public static string CsvHeader =>
            "Retail,OE,Customer,SerialNumber,DayDue,DateDue,Status,Qty,WhatIsIt,PO,WhatAreWeDoing,Parts,ShaftType,Priority,LastUser";

        public string ToCsvRow()
        {
            // Date in dd/MM/yyyy
            string dateText = DateDue == default(DateTime)
                ? ""
                : DateDue.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

            return string.Join(",",
                Escape(Retail),
                Escape(OE),
                Escape(Customer),
                Escape(SerialNumber),
                Escape(DayDue),
                Escape(dateText),
                Escape(Status),
                Qty.ToString(CultureInfo.InvariantCulture),
                Escape(WhatIsIt),
                Escape(PO),
                Escape(WhatAreWeDoing),
                Escape(Parts),
                Escape(ShaftType),
                Escape(Priority),
                Escape(LastUser)
            );
        }

        public static DesignItem FromCsvLine(string line)
        {
            var cells = line.Split(',');
            if (cells.Length < 15)
            {
                Array.Resize(ref cells, 15);
            }

            var item = new DesignItem
            {
                Retail = Unescape(cells[0]),
                OE = Unescape(cells[1]),
                Customer = Unescape(cells[2]),
                SerialNumber = Unescape(cells[3]),
                DayDue = Unescape(cells[4]),
                Status = Unescape(cells[6]),
                WhatIsIt = Unescape(cells[8]),
                PO = Unescape(cells[9]),
                WhatAreWeDoing = Unescape(cells[10]),
                Parts = Unescape(cells[11]),
                ShaftType = Unescape(cells[12]),
                Priority = Unescape(cells[13]),
                LastUser = Unescape(cells[14])
            };

            // Date
            var dateText = cells[5];
            if (!string.IsNullOrWhiteSpace(dateText) &&
                DateTime.TryParseExact(
                    dateText.Trim(),
                    "dd/MM/yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
            {
                item.DateDue = dt;
            }
            else
            {
                item.DateDue = DateTime.Today;
            }

            // Qty
            if (int.TryParse(cells[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var q))
            {
                item.Qty = q;
            }

            return item;
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(",") || s.Contains("\""))
            {
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            }

            return s;
        }

        private static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            if (s.StartsWith("\"") && s.EndsWith("\""))
            {
                s = s.Substring(1, s.Length - 2).Replace("\"\"", "\"");
            }
            return s;
        }
    }
}
