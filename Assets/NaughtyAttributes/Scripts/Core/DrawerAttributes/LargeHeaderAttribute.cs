using System;

namespace NaughtyAttributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class LargeHeaderAttribute : DrawerAttribute
    {
        public string Name { get; private set; }
        public string MaxValueName { get; private set; }
        public EColor Color { get; private set; }

        public LargeHeaderAttribute(string name, float maxValue, EColor color = EColor.Blue)
        {
            Name = name;
            Color = color;
        }

        public LargeHeaderAttribute(string name, string maxValueName, EColor color = EColor.Blue)
        {
            Name = name;
            MaxValueName = maxValueName;
            Color = color;
        }

        public LargeHeaderAttribute(string maxValueName, EColor color = EColor.Blue)
            : this("", maxValueName, color)
        {
        }
    }
}