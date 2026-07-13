using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    [AttributeUsage(
            AttributeTargets.Class |
            AttributeTargets.Struct |
            AttributeTargets.Interface |
            AttributeTargets.Enum |
            AttributeTargets.Delegate |
            AttributeTargets.Method |
            AttributeTargets.Property |
            AttributeTargets.Field |
            AttributeTargets.Event |
            AttributeTargets.Parameter |
            AttributeTargets.ReturnValue,
            AllowMultiple = false,
            Inherited = true)]
    public sealed class UsageAttribute : Attribute
    {
        public string Value { get; }

        public UsageAttribute(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Usage text cannot be null or empty.",
                    nameof(value));

            Value = value;
        }
    }
}
