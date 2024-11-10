namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Interface | AttributeTargets.Delegate, Inherited = false)]
    public sealed class NullableContextAttribute : Attribute
    {
        /// <summary>Flag specifying metadata related to nullable reference types.</summary>
        public readonly byte Flag;

        /// <summary>Initializes the attribute.</summary>
        /// <param name="value">The flag value.</param>
        public NullableContextAttribute(byte value)
        {
            Flag = value;
        }
    }
}
