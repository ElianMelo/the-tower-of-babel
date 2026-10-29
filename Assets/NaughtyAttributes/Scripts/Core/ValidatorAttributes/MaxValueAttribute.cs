using System;

namespace NaughtyAttributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class MaxValueAttribute : ValidatorAttribute
    {
        public float MaxValue { get; private set; }

        public MaxValueAttribute(float maxValue)
        {
            MaxValue = maxValue;
        }

        /// <summary>Name of a field, property or parameterless method on the owner that supplies the bound.</summary>
        public string MaxValueName { get; private set; }

        public MaxValueAttribute(string maxValueName)
        {
            MaxValueName = maxValueName;
        }

        public MaxValueAttribute(int maxValue)
        {
            MaxValue = maxValue;
        }
    }
}
