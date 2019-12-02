using System;
using System.Collections.Generic;
using System.Text;

namespace Esyur.Data
{
    public class PropertyValue
    {
        /// <summary>
        /// Get or set the value.
        /// </summary>
        public object Value { get; set; }
        /// <summary>
        /// Get or set date of modification or occurrence.
        /// </summary>
        public DateTime Date { get; set; }
        /// <summary>
        /// Get or set property age.
        /// </summary>
        public ulong Age { get; set; }

        /// <summary>
        /// Create an instance of PropertyValue.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="age">Age.</param>
        /// <param name="date">Date.</param>
        public PropertyValue(object value, ulong age, DateTime date)
        {
            Value = value;
            Age = age;
            Date = date;
        }
    }
}
