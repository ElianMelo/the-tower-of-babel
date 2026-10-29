using System;

namespace NaughtyAttributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class MinValueAttribute : ValidatorAttribute
    {
        public float MinValue { get; private set; }

        public MinValueAttribute(float minValue)
        {
            MinValue = minValue;
        }

        /// <summary>Name of a field, property or parameterless method on the owner that supplies the bound.</summary>
        public string MinValueName { get; private set; }

        public MinValueAttribute(string minValueName)
        {
            MinValueName = minValueName;
        }

        public MinValueAttribute(int minValue)
        {
            MinValue = minValue;
        }
    }
}
