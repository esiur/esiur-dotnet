namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, Inherited = false)]
    public sealed class NullableAttribute : Attribute
    {
        /// <summary>Flags specifying metadata related to nullable reference types.</summary>
        public readonly byte[] NullableFlags;

        /// <summary>Initializes the attribute.</summary>
        /// <param name="value">The flags value.</param>
        public NullableAttribute(byte value)
        {
            NullableFlags = new[] { value };
        }

        /// <summary>Initializes the attribute.</summary>
        /// <param name="value">The flags value.</param>
        public NullableAttribute(byte[] value)
        {
            NullableFlags = value;
        }
    }
}
