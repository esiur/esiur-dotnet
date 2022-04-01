namespace System.Runtime.CompilerServices
{
    [AttributeUsage(
        AttributeTargets.Class |
        AttributeTargets.Event |
        AttributeTargets.Field |
        AttributeTargets.GenericParameter |
        AttributeTargets.Parameter |
        AttributeTargets.Property |
        AttributeTargets.ReturnValue,
        AllowMultiple = false,
        Inherited = false)]
    public sealed class NullableAttribute : Attribute
    {
        public readonly byte[] Flags;
        public readonly byte Flag;

        public NullableAttribute(byte flag)
        {
            Flag = flag;// new byte[] { flag };
        }
        public NullableAttribute(byte[] flags)
        {
            Flags = flags;
        }
    }
}
