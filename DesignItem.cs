using System;
using System.ComponentModel;
using System.Globalization;

namespace DesignSheet
{
    public class DesignItem : INotifyPropertyChanged
    {
        // True for the non-editable blank rows around each date group
        public bool IsSeparator { get; set; }

        // ✅ Alias used by MainWindow.xaml.cs / XAML
        public bool IsSpacer
        {
            get => IsSeparator;
            set => IsSeparator = value;
        }

        public string? Retail { get; set; }
        public string? OE { get; set; }
        public string? Customer { get; set; }
        public string? SerialNumber { get; set; }
        public string? DayDue { get; set; }

        private string _dateDueText = string.Empty;
        public string DateDueText
        {
            get => _dateDueText;
            set
            {
                if (_dateDueText != value)
                {
                    _dateDueText = value;
                    OnPropertyChanged(nameof(DateDueText));
                    OnPropertyChanged(nameof(DateDue));
                }
            }
        }

        public DateTime? DateDue
        {
            get
            {
                if (DateTime.TryParseExact(
                        _dateDueText,
                        "dd/MM/yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var dt))
                {
                    return dt;
                }
                return null;
            }
            set
            {
                _dateDueText = value?.ToString("dd/MM/yyyy") ?? string.Empty;
                OnPropertyChanged(nameof(DateDueText));
                OnPropertyChanged(nameof(DateDue));
            }
        }

        public string? Status { get; set; }
        public string? Qty { get; set; }
        public string? WhatIsIt { get; set; }
        public string? PO { get; set; }
        public string? WhatAreWeDoing { get; set; }
        public string? Parts { get; set; }
        public string? ShaftType { get; set; }
        public string? Priority { get; set; }
        public string? LastUser { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public DesignItem Clone()
        {
            return new DesignItem
            {
                IsSeparator = IsSeparator,
                Retail = Retail,
                OE = OE,
                Customer = Customer,
                SerialNumber = SerialNumber,
                DayDue = DayDue,
                DateDueText = DateDueText,
                Status = Status,
                Qty = Qty,
                WhatIsIt = WhatIsIt,
                PO = PO,
                WhatAreWeDoing = WhatAreWeDoing,
                Parts = Parts,
                ShaftType = ShaftType,
                Priority = Priority,
                LastUser = LastUser
            };
        }
    }
}
